using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ConferenceApp.Services;

public sealed class CloudRoutingService(IEmbeddingGenerator<string, Embedding<float>> embeddings)
{
    public const string DefaultEndpoint = "https://models.github.ai/inference";
    public const string DefaultModel = "openai/gpt-4.1-mini";

    public RoutingSession Create(IChatClient local, IServiceProvider services, string key, string endpoint, string model, Action<string> selected)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("The cloud endpoint must be an HTTPS URL.");
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var openai = new OpenAIClient(new ApiKeyCredential(key), new OpenAIClientOptions
        {
            Endpoint = uri,
            RetryPolicy = new ClientRetryPolicy(0)
        });
        var cloud = new FunctionInvokingChatClient(openai.GetChatClient(model).AsIChatClient(), functionInvocationServices: services)
        {
            MaximumConsecutiveErrorsPerRequest = 0,
            AllowConcurrentInvocation = false,
            TerminateOnUnknownCalls = true
        };
        var localRoute = new RouteClient(local, "Apple Intelligence (on device)", selected, false);
        var cloudRoute = new RouteClient(cloud, $"GitHub Models: {model}", selected, true);
#pragma warning disable MEAI001
        var router = new SemanticRoutingChatClient(embeddings, new Dictionary<IChatClient, IReadOnlyList<string>>
        {
            [localRoute] = [
                "When does the conference start?",
                "Where is the venue?",
                "Who is speaking at the keynote?",
                "Show my favourite sessions.",
                "What talks are on after lunch?"
            ],
            [cloudRoute] = [
                "Plan my day around AI agents and data talks with no clashes.",
                "Build a conflict-free itinerary matching my interests and departure time.",
                "Compare sessions across tracks and create a balanced schedule.",
                "Analyze the agenda and recommend an itinerary with no overlapping talks.",
                "Find relevant talks, resolve scheduling conflicts, and add the best ones to my favourites."
            ]
        }, defaultClient: localRoute, scoreThreshold: 0.3f, leaveOpen: true);
#pragma warning restore MEAI001
        return new RoutingSession(router, cloud);
    }

    public sealed class RoutingSession(IChatClient client, IChatClient cloud) : IDisposable
    {
        public IChatClient Client { get; } = client;
        public void Dispose()
        {
            Client.Dispose();
            cloud.Dispose();
        }
    }

    private sealed class RouteClient(IChatClient inner, string name, Action<string> selected, bool cloud) : DelegatingChatClient(inner)
    {
        private ChatOptions? Prepare(ChatOptions? options)
        {
            selected(name);
            if (!cloud || options is null) return options;
            var copy = options.Clone();
            copy.AllowMultipleToolCalls = false;
            return copy;
        }

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            base.GetResponseAsync(messages, Prepare(options), cancellationToken);

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, Prepare(options), cancellationToken))
                yield return update;
        }

    }
}
