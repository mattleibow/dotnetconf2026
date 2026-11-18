using System.ComponentModel;

namespace ConferenceApp.AI;

public sealed class SessionInsights
{
    [Description("A useful, concise summary in one or two sentences, using only the supplied session details.")]
    public string Summary { get; set; } = "";

    [Description("One to three distinct topics chosen only from AI, Agents, Cloud, Data, Mobile, Web, IoT, Architecture, Security, DevOps, Gaming.")]
    public string[] Topics { get; set; } = [];
}
