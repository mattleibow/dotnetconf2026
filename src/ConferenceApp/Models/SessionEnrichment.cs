using System.Text.Json.Serialization;

namespace ConferenceApp.Models;

public record SessionEnrichment(string Summary, string Topic)
{
    public static SessionEnrichment Empty { get; } = new("", "None");

    [JsonIgnore]
    public bool HasTopic => Topic != "None";
}

[JsonSerializable(typeof(Dictionary<string, SessionEnrichment>))]
internal partial class EnrichmentJsonContext : JsonSerializerContext;
