using Azure.Provisioning.CognitiveServices;

var builder = DistributedApplication.CreateBuilder(args);

// Local AI server: Ollama in a container (models kept in a volume), plus Open WebUI to chat with it directly.
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .WithOpenWebUI();
ollama.AddModel("ollama-chat", "qwen2.5:3b");

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
    .WithEnvironment("OLLAMA_API_BASE", ollama.GetEndpoint("http"))
    .WithHttpHealthCheck("/health/liveliness")
    .WaitFor(litellmDb)
    .WaitFor(ollama);

if (builder.ExecutionContext.IsRunMode)
{
    // The app authenticates to LiteLLM with its own virtual key (created once in the Admin UI, then entered here).
    var appKey = builder.AddParameter("litellm-app-key", "sk-conference-demo-app", secret: true);

    var maui = builder.AddMauiProject("conference-app", "../ConferenceApp/ConferenceApp.csproj");

    // Mac Catalyst runs on this machine, so it reaches the gateway on localhost.
    maui.AddMacCatalystDevice()
        .WithEnvironment("AI_GATEWAY_KEY", appKey)
        .WithReference(gateway.GetEndpoint("http"));

    // Simulators and emulators reach the gateway, and send telemetry, through dev tunnels.
    var tunnel = builder.AddDevTunnel("gateway-tunnel")
        .WithAnonymousAccess()
        .WithReference(gateway.GetEndpoint("http"));

    maui.AddiOSSimulator()
        .WithEnvironment("AI_GATEWAY_KEY", appKey)
        .WithOtlpDevTunnel()
        .WithReference(gateway, tunnel);

    maui.AddAndroidEmulator()
        .WithEnvironment("AI_GATEWAY_KEY", appKey)
        .WithOtlpDevTunnel()
        .WithReference(gateway, tunnel);
}

builder.Build().Run();
