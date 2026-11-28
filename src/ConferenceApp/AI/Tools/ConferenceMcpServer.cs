#if MACCATALYST || WINDOWS
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ConferenceApp.Data;
using ConferenceApp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ConferenceApp.AI;

// Exposes the in-app conference tools as a local MCP server (Streamable HTTP, stateless)
// so desktop clients such as VS Code can search sessions, manage favourites, and drive navigation.
// ASP.NET Core isn't available in MAUI apps, so HttpListener hosts the SDK's transport directly.
public sealed class ConferenceMcpServer(
    IServiceProvider services,
    ILoggerFactory loggerFactory,
    ConferenceDbProvider dbProvider,
    UserPreferencesService preferences,
    ILogger<ConferenceMcpServer> logger)
    : BaseBackgroundService(dbProvider, preferences, logger)
{
    public const string Url = "http://localhost:5299/mcp/";

    private static readonly JsonTypeInfo<JsonRpcMessage> MessageTypeInfo =
        (JsonTypeInfo<JsonRpcMessage>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcMessage));

    private readonly McpServerTool[] tools =
    [
        .. ConferenceDataToolContext.Default.Tools
            .Concat(FavoritesToolContext.Default.Tools)
            .Concat(NavigationToolContext.Default.Tools)
            .OfType<AIFunction>()
            .Select(function => McpServerTool.Create(function))
    ];

    protected override async Task ExecuteAsync()
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(Url);
        listener.Start();
        Logger.LogInformation("[AI MCP] Serving {ToolCount} tools at {Url}", tools.Length, Url);

        while (listener.IsListening)
        {
            var context = await listener.GetContextAsync();
            _ = HandleRequestAsync(context);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            // Stateless mode only needs POST; there's no server-initiated stream or session to delete.
            if (context.Request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var message = await JsonSerializer.DeserializeAsync(context.Request.InputStream, MessageTypeInfo);
            if (message is null)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            if (context.Request.Headers["MCP-Protocol-Version"] is { Length: > 0 } protocolVersion)
            {
                message.Context ??= new();
                message.Context.ProtocolVersion = protocolVersion;
            }

            Logger.LogInformation("[AI MCP] {Method}", (message as JsonRpcRequest)?.Method ?? message.GetType().Name);

            using var cts = new CancellationTokenSource();
            var transport = new StreamableHttpServerTransport(loggerFactory) { Stateless = true };
            var options = new McpServerOptions
            {
                ServerInfo = new() { Name = "conference-app", Version = "1.0.0" },
                ScopeRequests = false,
                ToolCollection = [.. tools]
            };
            var server = McpServer.Create(transport, options, loggerFactory, services);
            var run = server.RunAsync(cts.Token);

            try
            {
                response.ContentType = "text/event-stream";
                response.Headers["Cache-Control"] = "no-cache,no-store";
                if (!await transport.HandlePostRequestAsync(message, response.OutputStream, cts.Token))
                {
                    response.ContentType = null;
                    response.StatusCode = (int)HttpStatusCode.Accepted;
                }
            }
            finally
            {
                await transport.DisposeAsync();
                await cts.CancelAsync();
                try { await run; } catch (OperationCanceledException) { }
                await server.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "[AI MCP] Request failed.");
        }
        finally
        {
            response.Close();
        }
    }
}
#endif
