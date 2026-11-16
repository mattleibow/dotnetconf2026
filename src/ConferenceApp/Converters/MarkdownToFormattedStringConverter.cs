using System.Globalization;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ConferenceApp.Converters;

public class MarkdownToFormattedStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var formatted = new FormattedString();
        if (value is string { Length: > 0 } markdown)
            WriteBlocks(Markdown.Parse(markdown), formatted, listPrefix: null);
        return formatted;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static void WriteBlocks(ContainerBlock container, FormattedString formatted, string? listPrefix)
    {
        var index = 1;
        foreach (var block in container)
        {
            switch (block)
            {
                case ListBlock list:
                    foreach (var item in list.OfType<ListItemBlock>())
                        WriteBlocks(item, formatted, list.IsOrdered ? $"{index++}. " : "• ");
                    break;
                case LeafBlock { Inline: { } inline }:
                    if (formatted.Spans.Count > 0)
                        formatted.Spans.Add(new Span { Text = "\n\n" });
                    if (listPrefix is not null)
                    {
                        formatted.Spans.Add(new Span { Text = listPrefix, FontAttributes = FontAttributes.Bold });
                        listPrefix = null;
                    }
                    WriteInlines(inline, formatted, block is HeadingBlock ? FontAttributes.Bold : FontAttributes.None);
                    break;
                case ContainerBlock nested:
                    WriteBlocks(nested, formatted, listPrefix);
                    break;
            }
        }
    }

    private static void WriteInlines(ContainerInline container, FormattedString formatted, FontAttributes attributes)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    formatted.Spans.Add(new Span { Text = literal.Content.ToString(), FontAttributes = attributes });
                    break;
                case CodeInline code:
                    formatted.Spans.Add(new Span { Text = code.Content, FontAttributes = attributes | FontAttributes.Bold });
                    break;
                case LineBreakInline:
                    formatted.Spans.Add(new Span { Text = "\n" });
                    break;
                case EmphasisInline emphasis:
                    var flag = emphasis.DelimiterCount >= 2 ? FontAttributes.Bold : FontAttributes.Italic;
                    WriteInlines(emphasis, formatted, attributes | flag);
                    break;
                case ContainerInline nested:
                    WriteInlines(nested, formatted, attributes);
                    break;
            }
        }
    }
}
