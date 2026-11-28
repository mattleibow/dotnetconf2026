using Azure.Provisioning.CognitiveServices;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

ReferenceExpression modelApiBase;
string localModel;
Action<IResourceBuilder<ContainerResource>> waitForModel;

// "ModelRunner" (default) uses Docker Model Runner for local runs; "Ollama" uses the Ollama container.
// Use the "ollama" launch profile (`aspire start -lp ollama`) or `aspire start -- --LocalModelRuntime Ollama`.
var useModelRunner = builder.ExecutionContext.IsRunMode &&
    !string.Equals(builder.Configuration["LocalModelRuntime"], "Ollama", StringComparison.OrdinalIgnoreCase);
if (useModelRunner)
{
    // Docker Model Runner executes llama.cpp natively on Apple Silicon, giving it Metal GPU access.
    const string model = "hf.co/Qwen/Qwen2.5-3B-Instruct-GGUF";
    localModel = "openai/huggingface.co/qwen/qwen2.5-3b-instruct-gguf:latest";
    var enableModelRunner = builder.AddExecutable("enable-model-runner", "docker", ".", "desktop", "enable",
        "model-runner", "--tcp=12434");
    var pullLocalModel = builder.AddExecutable("pull-local-model", "docker", ".", "model", "pull", model)
        .WaitForCompletion(enableModelRunner);
    var loadLocalModel = builder.AddExecutable("load-local-model", "docker", ".", "model", "run", "--detach", model)
        .WaitForCompletion(pullLocalModel);
    var modelRunner = builder.AddExternalService("model-runner", "http://localhost:12434/")
        .WithHttpHealthCheck("/engines/v1/models");
    builder.AddContainer("open-webui", "ghcr.io/open-webui/open-webui", "0.11.4")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithEnvironment("OLLAMA_BASE_URL", "http://host.docker.internal:12434")
        .WithEnvironment("WEBUI_AUTH", "false")
        .WithVolume("open-webui-data", "/app/backend/data")
        .WaitForCompletion(loadLocalModel)
        .WaitFor(modelRunner);
    modelApiBase = ReferenceExpression.Create($"http://model-runner.docker.internal/engines/v1");
    waitForModel = gateway => gateway.WaitForCompletion(loadLocalModel).WaitFor(modelRunner);
}
else
{
    // Ollama in a container works anywhere Docker does, and is the deployable path; on macOS it runs on the CPU.
    var ollama = builder.AddOllama("ollama")
        .WithDataVolume()
        .WithOpenWebUI();
    ollama.AddModel("ollama-chat", "qwen2.5:3b");
    localModel = "openai/qwen2.5:3b";
    modelApiBase = ReferenceExpression.Create($"{ollama.GetEndpoint("http")}/v1");
    waitForModel = gateway => gateway.WaitFor(ollama);
}

// Cloud AI: Aspire provisions an Azure AI Foundry account and model deployment on first run.
// API keys are enabled so the LiteLLM container can call it; paste the key into the "azure-openai-key" parameter.
var foundry = builder.AddFoundry("foundry")
    .ConfigureInfrastructure(infra =>
    {
        var cognitiveAccount = infra.GetProvisionableResources().OfType<CognitiveServicesAccount>().Single();
        cognitiveAccount.Properties.DisableLocalAuth = false;
    });
foundry.AddDeployment("azure-chat", "gpt-4.1-mini", "2025-04-14", "OpenAI")
    .WithProperties(d =>
    {
        d.SkuName = "GlobalStandard";
        d.SkuCapacity = 50;
    });

// LiteLLM is the AI gateway: models and fallback come from litellm-config.yaml; virtual keys, guardrails and
// spend logs are managed in its Admin UI (/ui) and stored in Postgres.
var postgresPassword = builder.AddParameter("postgres-password", "conference-demo-postgres", secret: true);
var litellmDb = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume()
    .AddDatabase("litellm");

var azureKey = builder.AddParameter("azure-openai-key", secret: true);
var masterKey = builder.AddParameter("litellm-master-key", "sk-conference-demo-master", secret: true);
var gateway = builder.AddContainer("ai-gateway", "docker.litellm.ai/berriai/litellm", "latest")
    .WithHttpEndpoint(targetPort: 4000, name: "http")
    .WithExternalHttpEndpoints()
    .WithBindMount("litellm-config.yaml", "/app/config.yaml", isReadOnly: true)
    .WithArgs("--config", "/app/config.yaml", "--port", "4000")
    .WithEnvironment("LITELLM_MASTER_KEY", masterKey)
    .WithEnvironment("LITELLM_SALT_KEY", masterKey)
    .WithEnvironment("DATABASE_URL", litellmDb.Resource.UriExpression)
    .WithEnvironment("STORE_MODEL_IN_DB", "True")
    .WithEnvironment("AZURE_API_BASE", foundry.Resource.Endpoint)
    .WithEnvironment("AZURE_API_KEY", azureKey)
    .WithEnvironment("OLLAMA_API_BASE", modelApiBase)
    .WithEnvironment("OLLAMA_MODEL", localModel)
    .WithHttpHealthCheck("/health/liveliness")
    .WaitFor(litellmDb);
waitForModel(gateway);

if (builder.ExecutionContext.IsRunMode)
{
    // WaitFor(gateway) also waits for this callback, so apps cannot start before their key is registered.
    var appKey = builder.AddParameter("litellm-app-key", "sk-conference-demo-app", secret: true);
    gateway.OnResourceReady(async (resource, e, ct) =>
    {
        var logger = e.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
        var key = await appKey.Resource.GetValueAsync(ct);
        ArgumentException.ThrowIfNullOrEmpty(key);
        using var http = new HttpClient { BaseAddress = new Uri(gateway.GetEndpoint("http").Url) };
        http.DefaultRequestHeaders.Authorization = new("Bearer", await masterKey.Resource.GetValueAsync(ct));

        // LiteLLM accepts the hash for lookup, keeping the raw key out of access logs.
        var keyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        using var keyInfo = await http.GetAsync($"/key/info?key={keyHash}", ct);
        if (keyInfo.StatusCode != HttpStatusCode.NotFound)
        {
            keyInfo.EnsureSuccessStatusCode();
            logger.LogInformation("App virtual key 'conference-app' already exists.");
            return;
        }

        using var response = await http.PostAsJsonAsync("/key/generate",
            new { key, key_alias = "conference-app", models = new[] { "azure", "ollama" } }, ct);
        response.EnsureSuccessStatusCode();
        logger.LogInformation("Created app virtual key 'conference-app'.");
    });

    // AddMauiProject nests each device under the "conference-app" resource in the dashboard.
    var maui = builder.AddMauiProject("conference-app", "../ConferenceApp/ConferenceApp.csproj");

    // Mac Catalyst runs on this machine, so it reaches the gateway on localhost.
    // The desktop app also hosts an MCP server for VS Code (see .vscode/mcp.json).
    maui.AddMacCatalystDevice()
        .WithUrl("http://localhost:5299/mcp/", "MCP server")
        .WithEnvironment("AI_GATEWAY_KEY", appKey)
        .WithReference(gateway.GetEndpoint("http"))
        .WaitFor(gateway);

    // Simulators and emulators reach the gateway, and send telemetry, through dev tunnels.
    var tunnel = builder.AddDevTunnel("gateway-tunnel")
        .WithAnonymousAccess()
        .WithReference(gateway.GetEndpoint("http"));

    maui.AddiOSSimulator()
        .WithEnvironment("AI_GATEWAY_KEY", appKey)
        .WithOtlpDevTunnel()
        .WithReference(gateway, tunnel)
        .WaitFor(gateway);

    maui.AddAndroidEmulator()
        .WithEnvironment("AI_GATEWAY_KEY", appKey)
        .WithOtlpDevTunnel()
        .WithReference(gateway, tunnel)
        .WaitFor(gateway);
}

builder.Build().Run();
