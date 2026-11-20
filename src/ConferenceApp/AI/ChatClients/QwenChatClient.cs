#if WINDOWS
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace ConferenceApp.AI;

// A local Qwen3-0.6B client using Qwen's native tool-call format.
// Shares one lazily loaded native model per process (clients are transient) and streams tokens off the UI thread.
public sealed class QwenChatClient(ILogger<QwenChatClient> logger) : IChatClient
{
    private static readonly SemaphoreSlim extractGate = new(1, 1);
    private static readonly SemaphoreSlim initGate = new(1, 1);
    private static Model? model;
    private static Tokenizer? tokenizer;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (options?.ResponseFormat is ChatResponseFormatJson)
            throw new NotSupportedException("The Qwen client does not guarantee structured output. Send tool-free structured requests to the platform model.");

        var functions = options?.ToolMode == ChatToolMode.None
            ? [] : options?.Tools?.OfType<AIFunctionDeclaration>().ToArray() ?? [];
        if (options?.Tools?.Count > functions.Length && options.ToolMode != ChatToolMode.None)
            throw new NotSupportedException("The Qwen client supports function tools only.");

        var history = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var messagesJson = MessagesJson(history, options);
        var toolsJson = functions.Length > 0 ? ToolsJson(functions) : null;
        var maxOutputTokens = Math.Clamp(options?.MaxOutputTokens ?? 512, 1, 2048);
        var requireToolCall = options?.ToolMode is RequiredChatToolMode;

        var channel = Channel.CreateUnbounded<ChatResponseUpdate>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true
        });

        var producer = Task.Run(async () =>
        {
            try
            {
                var (loadedModel, loadedTokenizer) = await EnsureModelAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                var responseId = Guid.NewGuid().ToString("N");
                var messageId = Guid.NewGuid().ToString("N");
                var (text, streamingText) = Generate(toolsJson);
                var message = ParseResponse(text, functions);
                // The small model can re-request a call it already has results for until the iteration cap.
                if (!requireToolCall && IsRepeatedCall(message, history))
                {
                    logger.LogWarning("[AI Tool] ONNX repeated an answered tool call; answering from the existing results.");
                    (text, streamingText) = Generate(null);
                    message = ParseResponse(text, []);
                }

                if (options?.ToolMode is RequiredChatToolMode required
                    && !message.Contents.OfType<FunctionCallContent>().Any(call =>
                        required.RequiredFunctionName is null || call.Name == required.RequiredFunctionName))
                    throw new InvalidOperationException("The ONNX model did not return the required tool call.");

                var toolCount = message.Contents.OfType<FunctionCallContent>().Count();
                logger.LogInformation("[AI Tool] ONNX returned {ToolCount} tool calls.", toolCount);

                if (streamingText)
                {
                    channel.Writer.TryWrite(new ChatResponseUpdate
                    {
                        Role = ChatRole.Assistant,
                        ResponseId = responseId,
                        MessageId = messageId,
                        ModelId = "Qwen3-0.6B",
                        FinishReason = ChatFinishReason.Stop
                    });
                }
                else
                {
                    channel.Writer.TryWrite(new ChatResponseUpdate(ChatRole.Assistant, message.Contents)
                    {
                        ResponseId = responseId,
                        MessageId = messageId,
                        ModelId = "Qwen3-0.6B",
                        FinishReason = toolCount > 0 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop
                    });
                }

                channel.Writer.TryComplete();

                (string Text, bool StreamedText) Generate(string? tools)
                {
                    var prompt = loadedTokenizer.ApplyChatTemplate(null!, messagesJson, tools!, true)
                        + "<think>\n\n</think>\n\n";
                    using var tokens = loadedTokenizer.Encode(prompt);
                    using var parameters = new GeneratorParams(loadedModel);
                    parameters.SetSearchOption("max_length", (int)tokens[0].Length + maxOutputTokens);
                    parameters.SetSearchOption("do_sample", false);
                    using var generator = new Generator(loadedModel, parameters);
                    generator.AppendTokenSequences(tokens);
                    using var stream = loadedTokenizer.CreateStream();

                    var buffer = new StringBuilder();
                    var streamingText = false;
                    var generatedTokens = 0;

                    while (!generator.IsDone() && generatedTokens < maxOutputTokens)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        generator.GenerateNextToken();
                        var sequence = generator.GetSequence(0);
                        // EOS may finish generation without appending another token.
                        if (sequence.Length <= tokens[0].Length + generatedTokens)
                            break;

                        generatedTokens++;
                        var piece = stream.Decode(sequence[^1]);
                        if (string.IsNullOrEmpty(piece))
                            continue;

                        buffer.Append(piece);
                        if (streamingText)
                        {
                            channel.Writer.TryWrite(new ChatResponseUpdate(ChatRole.Assistant, piece)
                            {
                                ResponseId = responseId,
                                MessageId = messageId,
                                ModelId = "Qwen3-0.6B"
                            });
                            continue;
                        }

                        if (requireToolCall)
                            continue;

                        var candidate = StripThinkingPrefix(buffer.ToString().AsSpan());
                        if (candidate.IsEmpty
                            || StartsWithOrIsPrefixOf(candidate, "<tool_call>")
                            || StartsWithOrIsPrefixOf(candidate, "<think>"))
                            continue;

                        streamingText = true;
                        channel.Writer.TryWrite(new ChatResponseUpdate(ChatRole.Assistant, candidate.ToString())
                        {
                            ResponseId = responseId,
                            MessageId = messageId,
                            ModelId = "Qwen3-0.6B"
                        });
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!generator.IsDone())
                        throw new InvalidOperationException("The ONNX tool response exceeded its output limit. No partial tool call was executed.");
                    return (buffer.ToString(), streamingText);
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    logger.LogError(ex, "[AI Tool] ONNX tool request failed; not falling back to another provider.");
                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return update;

        await producer.ConfigureAwait(false);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    private async ValueTask<(Model Model, Tokenizer Tokenizer)> EnsureModelAsync(CancellationToken cancellationToken)
    {
        if (model is not null && tokenizer is not null)
            return (model, tokenizer);

        await initGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (model is null || tokenizer is null)
            {
                var path = await PrepareModelAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("[AI Tool] Loading ONNX Qwen3-0.6B.");
                var loaded = new Model(path);
                try
                {
                    tokenizer = new Tokenizer(loaded);
                    model = loaded;
                }
                catch
                {
                    loaded.Dispose();
                    throw;
                }
            }

            return (model, tokenizer);
        }
        finally
        {
            initGate.Release();
        }
    }

    private static ReadOnlySpan<char> StripThinkingPrefix(ReadOnlySpan<char> span)
    {
        var trimmed = span.TrimStart();
        if (!trimmed.StartsWith("<think>", StringComparison.Ordinal))
            return trimmed;

        var end = trimmed.IndexOf("</think>", StringComparison.Ordinal);
        return end < 0 ? ReadOnlySpan<char>.Empty : trimmed[(end + "</think>".Length)..].TrimStart();
    }

    private static bool StartsWithOrIsPrefixOf(ReadOnlySpan<char> value, string target) =>
        value.Length <= target.Length
            ? target.AsSpan().StartsWith(value, StringComparison.Ordinal)
            : value.StartsWith(target, StringComparison.Ordinal);

    private static async Task<string> PrepareModelAsync(CancellationToken cancellationToken)
    {
        const string folder = "Qwen3-0.6B";
        string[] names = ["model.onnx", "model.onnx.data", "tokenizer.json", "tokenizer_config.json", "chat_template.jinja", "genai_config.json"];
        var destination = Path.Combine(FileSystem.AppDataDirectory, "models", folder);
        // Include the source revision so a same-version redeploy cannot reuse another model.
        var version = Path.Combine(destination, "version.txt");
        var expectedVersion = AppInfo.VersionString + "+" + AppInfo.BuildString
            + ":c1899de289a04d12100db370d81485cdf75e47ca:k_quant";

        await extractGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(version) && await File.ReadAllTextAsync(version, cancellationToken) == expectedVersion
                && names.All(name => File.Exists(Path.Combine(destination, name))))
                return destination;

            Directory.CreateDirectory(destination);
            foreach (var name in names)
            {
                await using var source = await FileSystem.OpenAppPackageFileAsync($"models/chat/{folder}/{name}");
                var target = Path.Combine(destination, name);
                var temporary = target + ".partial";
                try
                {
                    await using (var output = File.Create(temporary))
                        await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    File.Move(temporary, target, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
            await File.WriteAllTextAsync(version, expectedVersion, cancellationToken);
            return destination;
        }
        finally
        {
            extractGate.Release();
        }
    }

    // The shared native model lives for the process, so disposing one transient client must not unload it.
    public void Dispose()
    {
    }

    internal static string MessagesJson(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var result = new JsonArray();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
            result.Add(new JsonObject { ["role"] = "system", ["content"] = options.Instructions });

        foreach (var message in messages)
        {
            if (message.Contents.Any(c => c is not TextContent and not FunctionCallContent and not FunctionResultContent))
                throw new NotSupportedException("The ONNX tool model accepts text and function messages only.");

            var toolResults = message.Contents.OfType<FunctionResultContent>().ToArray();
            if (toolResults.Length > 0)
            {
                foreach (var toolResult in toolResults)
                    result.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["content"] = toolResult.Result is string text ? text : JsonValueFor(toolResult.Result)?.ToJsonString() ?? "null"
                    });
                continue;
            }

            var item = new JsonObject { ["role"] = message.Role.Value, ["content"] = message.Text };
            var calls = message.Contents.OfType<FunctionCallContent>().ToArray();
            if (calls.Length > 0)
                item["tool_calls"] = new JsonArray([.. calls.Select(call => (JsonNode)new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = call.Name,
                        ["arguments"] = ArgumentsJson(call.Arguments)
                    }
                })]);
            result.Add(item);
        }
        return result.ToJsonString();
    }

    internal static bool IsRepeatedCall(ChatMessage response, IEnumerable<ChatMessage> history)
    {
        var calls = response.Contents.OfType<FunctionCallContent>().ToArray();
        if (calls.Length == 0)
            return false;

        var contents = history.SelectMany(message => message.Contents).ToArray();
        var answered = contents.OfType<FunctionResultContent>().Select(result => result.CallId).ToHashSet();
        var previous = contents.OfType<FunctionCallContent>()
            .Where(call => answered.Contains(call.CallId))
            .Select(Key)
            .ToHashSet();
        return calls.All(call => previous.Contains(Key(call)));

        static string Key(FunctionCallContent call) => call.Name + ArgumentsJson(call.Arguments).ToJsonString();
    }

    internal static string ToolsJson(IEnumerable<AIFunctionDeclaration> tools) =>
        new JsonArray([.. tools.Select(tool => (JsonNode)new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = JsonNode.Parse(tool.JsonSchema.GetRawText())
            }
        })]).ToJsonString();

    internal static ChatMessage ParseResponse(string text, IReadOnlyList<AIFunctionDeclaration> tools)
    {
        const string open = "<tool_call>";
        const string close = "</tool_call>";
        var result = new ChatMessage(ChatRole.Assistant, new List<AIContent>());
        var remaining = text.AsSpan().Trim();
        if (remaining.StartsWith("<think>", StringComparison.Ordinal))
        {
            var endThinking = remaining.IndexOf("</think>", StringComparison.Ordinal);
            if (endThinking < 0)
                throw new InvalidDataException("The ONNX model returned unfinished reasoning.");
            remaining = remaining[(endThinking + "</think>".Length)..].Trim();
        }

        if (!remaining.Contains("<tool_call", StringComparison.Ordinal))
        {
            if (remaining.IsEmpty || remaining.Contains("</tool_call", StringComparison.Ordinal)
                || remaining.Contains("<think", StringComparison.Ordinal)
                || remaining.Contains("</think", StringComparison.Ordinal))
                throw new InvalidDataException("The ONNX model returned empty or malformed output.");
            return new ChatMessage(ChatRole.Assistant, remaining.ToString());
        }

        // Validate every call before returning any of them to the invocation middleware.
        while (!remaining.IsEmpty)
        {
            if (!remaining.StartsWith(open, StringComparison.Ordinal))
                throw new InvalidDataException("The ONNX model mixed tool calls with unexpected text.");
            var end = remaining.IndexOf(close, StringComparison.Ordinal);
            if (end < 0)
                throw new InvalidDataException("The ONNX model returned an incomplete tool call.");
            using var document = JsonDocument.Parse(remaining[open.Length..end].ToString());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The ONNX model returned an invalid function call.");
            var fields = root.EnumerateObject().Select(property => property.Name).ToArray();
            if (fields.Length != 2 || fields.Distinct(StringComparer.Ordinal).Count() != 2)
                throw new InvalidDataException("The ONNX model returned unexpected or duplicate function fields.");

            var tool = tools.FirstOrDefault(tool => tool.Name == name.GetString())
                ?? throw new InvalidDataException($"The ONNX model requested unknown tool '{name.GetString()}'.");
            ValidateArguments(arguments, tool.JsonSchema);
            result.Contents.Add(new FunctionCallContent(
                Guid.NewGuid().ToString("N"), tool.Name,
                arguments.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())));
            remaining = remaining[(end + close.Length)..].Trim();
        }
        return result;
    }

    private static void ValidateArguments(JsonElement arguments, JsonElement schema)
    {
        if (schema.TryGetProperty("required", out var required))
            foreach (var property in required.EnumerateArray())
                if (!arguments.TryGetProperty(property.GetString()!, out _))
                    throw new InvalidDataException($"The ONNX tool call omitted '{property.GetString()}'.");

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var argument in arguments.EnumerateObject())
        {
            if (!names.Add(argument.Name))
                throw new InvalidDataException($"The ONNX tool call duplicated '{argument.Name}'.");
            if (!schema.TryGetProperty("properties", out var properties)
                || !properties.TryGetProperty(argument.Name, out var propertySchema))
                throw new InvalidDataException($"The ONNX tool call supplied unknown argument '{argument.Name}'.");
            // Conference tools deliberately expose only string parameters.
            if (!propertySchema.TryGetProperty("type", out var type) || type.GetString() != "string"
                || argument.Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"The ONNX tool call supplied an unsupported value for '{argument.Name}'.");
        }
    }

    private static JsonObject ArgumentsJson(IDictionary<string, object?>? arguments)
    {
        var result = new JsonObject();
        if (arguments is not null)
            foreach (var argument in arguments)
                result[argument.Key] = JsonValueFor(argument.Value);
        return result;
    }

    private static JsonNode? JsonValueFor(object? value) => value switch
    {
        null => null,
        JsonElement element => JsonNode.Parse(element.GetRawText()),
        string text => JsonValue.Create(text),
        bool boolean => JsonValue.Create(boolean),
        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        _ => throw new NotSupportedException($"ONNX tool messages cannot serialize {value.GetType().Name}.")
    };
}
#endif
