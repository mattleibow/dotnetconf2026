using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ConferenceApp.Models;

public partial class ChatItem(bool isUser, string text = "") : ObservableObject
{
    public bool IsUser { get; } = isUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedText))]
    public partial string Text { get; set; } = text;

    public FormattedString FormattedText => IsUser ? new() { Spans = { new Span { Text = Text } } } : FormatMarkdown(Text);

    private static FormattedString FormatMarkdown(string markdown)
    {
        var formatted = new FormattedString();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                formatted.Spans.Add(new Span { Text = "\n" });

            var line = lines[i];
            if (line.StartsWith("- ") || line.StartsWith("* "))
                line = "• " + line[2..];

            foreach (Match match in InlineTokenRegex().Matches(line))
            {
                if (match.Groups[1].Success)
                    formatted.Spans.Add(new Span { Text = match.Groups[1].Value, FontAttributes = FontAttributes.Bold });
                else if (match.Groups[2].Success)
                    formatted.Spans.Add(new Span { Text = match.Groups[2].Value, FontAttributes = FontAttributes.Italic });
                else if (match.Length > 0)
                    formatted.Spans.Add(new Span { Text = match.Value });
            }
        }
        return formatted;
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*|\*(.+?)\*|([^*]+|\*)")]
    private static partial Regex InlineTokenRegex();
}
