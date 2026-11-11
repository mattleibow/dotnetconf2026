using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ConferenceApp.Models;
using Microsoft.Extensions.DataIngestion;

namespace ConferenceApp.Services;

internal sealed class SessionChunker(IReadOnlyDictionary<string, Session> sessions) : IngestionChunker<string>
{
    public int ChunkCount { get; private set; }

    public override async IAsyncEnumerable<IngestionChunk<string>> ProcessAsync(
        IngestionDocument document, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = sessions[document.Identifier];
        // Preserve the original Markdown, including list markers, for identical embeddings and excerpts.
        var markdown = await File.ReadAllTextAsync(document.Identifier, cancellationToken);
        var firstNewline = markdown.IndexOf('\n');
        var content = firstNewline < 0 ? "" : markdown[(firstNewline + 1)..].Trim();
        var sentences = Regex.Split(Regex.Replace(content, @"\s+", " ").Trim(), @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        for (var i = 0; i < sentences.Length; i += 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var excerpt = string.Join(" ", sentences.Skip(i).Take(2));
            ChunkCount++;
            yield return new IngestionChunk<string>($"{session.Title}. {excerpt}", document, excerpt);
        }
    }
}
