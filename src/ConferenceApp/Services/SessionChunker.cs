using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ConferenceApp.Models;
using Microsoft.Extensions.DataIngestion;

namespace ConferenceApp.Services;

internal sealed class SessionChunker(IReadOnlyDictionary<string, Session> sessions) : IngestionChunker<string>
{
    public int Count { get; private set; }

    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IngestionDocument document, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = sessions[document.Identifier];
        var elements = document.EnumerateContent().ToList();
        if (elements.FirstOrDefault() is IngestionDocumentHeader)
            elements.RemoveAt(0);
        var text = string.Join(" ", elements.Select(element => element.Text ?? element.GetMarkdown()));
        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var sentences = Regex.Split(normalized, @"(?<=[.!?])\s+").Where(s => s.Length > 0).ToArray();
        for (var i = 0; i < sentences.Length; i += 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var excerpt = string.Join(" ", sentences.Skip(i).Take(2));
            var chunk = new IngestionChunk<string>($"{session.Title}. {excerpt}", document);
            chunk.Metadata["excerpt"] = excerpt;
            Count++;
            yield return chunk;
        }
        await Task.CompletedTask;
    }
}
