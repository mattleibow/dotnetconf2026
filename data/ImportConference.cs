#!/usr/bin/env dotnet
#:property TargetFramework=net11.0
#:property LangVersion=15.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:package AngleSharp@1.10.1

// Imports the CPTMSDUG .NET Conf 2026 website into data/conference/, the offline
// snapshot that the demo app is built from:
//
//   conference.json        structure and relationships: cities, agenda, speakers, venues, sponsors
//   {kind}/{id}.md         "# Title" followed by the published abstract, bio or description
//   {kind}/{id}.png|jpg    local images
//
// Usage (from the repository root):  dotnet run data/ImportConference.cs
// See data/README.md for the format.
//
// Each run deletes and rewrites data/conference/. If something goes wrong, use Git to see
// what changed or to restore the previous snapshot.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

const string sourceUrl = "https://cptmsdug.dev/dotnet-conf-2026.html";

// Relative to this file, so the working directory doesn't matter.
var output = Path.Combine(SourceDirectory(), "conference");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

try
{
    if (args.Length != 0)
        throw new ArgumentException("This single-purpose importer takes no arguments.");

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("dotnetconf2026-demo-importer/1.0");

    // Start clean so removed sessions or speakers don't leave stale files behind.
    if (Directory.Exists(output))
        Directory.Delete(output, recursive: true);
    Directory.CreateDirectory(output);

    var conference = await new ContentBuilder(output, http, new Uri(sourceUrl), cancellation.Token).BuildAsync();
    Content.Validate(output, conference);

    Console.WriteLine($"Imported {conference.Sessions.Count} agenda entries, {conference.Speakers.Count} speakers, " +
        $"{conference.Venues.Count} venues and {conference.Sponsors.Count} sponsors into {output}");
    Console.WriteLine($"No published abstract: {conference.Sessions.Count(s => !s.HasAbstract)} agenda entries. " +
        $"No published bio: {conference.Speakers.Count(s => !s.HasBio)} speakers. These documents contain only a heading.");
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or JsonException
    or InvalidOperationException or ArgumentException or FormatException or OperationCanceledException)
{
    Console.Error.WriteLine($"Import failed: {error.Message}");
    Console.Error.WriteLine("Run 'git restore data/conference' to get the previous snapshot back.");
    return 1;
}

static string SourceDirectory([CallerFilePath] string file = "") => Path.GetDirectoryName(file)!;


// ---------------------------------------------------------------------------------------------
// Website -> conference.json + Markdown + images
// ---------------------------------------------------------------------------------------------

// Reads the event page and writes the whole snapshot into one folder.
// This targets the CPTMSDUG page structure; it isn't a general-purpose scraper.
sealed class ContentBuilder(string folder, HttpClient http, Uri sourceUrl, CancellationToken ct)
{
    // The page uses these keys in its tab IDs: #durban-schedule, #capetown-speakers, and so on.
    static readonly (string Key, string Name)[] Cities =
        [("durban", "Durban"), ("capetown", "Cape Town"), ("bloemfontein", "Bloemfontein")];

    readonly Conference conference = new(1, ".NET Conf 2026 Community Edition South Africa",
        sourceUrl.AbsoluteUri, "Africa/Johannesburg", [], [], [], [], []);

    // Speakers by ID, so agenda entries can check their speaker references.
    readonly Dictionary<string, Speaker> speakers = new(StringComparer.Ordinal);

    // Session abstracts by title and speaker bios by name, read from the page's JavaScript.
    Dictionary<string, JsonElement> descriptions = new();
    Dictionary<string, JsonElement> bios = new();

    public async Task<Conference> BuildAsync()
    {
        var html = Encoding.UTF8.GetString(await http.GetByteArrayAsync(sourceUrl, ct));
        using var page = await new HtmlParser().ParseDocumentAsync(html, ct);

        await ReadEmbeddedTextAsync(page);
        await AddCitiesAndVenuesAsync(page);
        await AddSpeakersAsync(page);
        await AddAgendaAsync(page);
        await AddSponsorsAsync(page);

        // A stable order keeps the JSON diff small between refreshes.
        conference.Sessions.Sort((left, right) => StringComparer.Ordinal.Compare(
            $"{left.Start}/{left.Track}/{left.Id}", $"{right.Start}/{right.Track}/{right.Id}"));

        await File.WriteAllTextAsync(Path.Combine(folder, "conference.json"),
            JsonSerializer.Serialize(conference, ContentJson.Default.Conference) + "\n", ct);

        return conference;
    }

    // Abstracts and bios aren't in the visible HTML. The page fills its pop-ups from two JavaScript
    // objects: defaults in main.js, then Object.assign(...) overrides in an inline script.
    async Task ReadEmbeddedTextAsync(IDocument page)
    {
        var javascript = new StringBuilder();

        foreach (var script in page.QuerySelectorAll("script[src]"))
        {
            var src = script.GetAttribute("src")!;
            if (src.EndsWith("/js/main.js", StringComparison.Ordinal))
                javascript.AppendLine(Encoding.UTF8.GetString(await http.GetByteArrayAsync(new Uri(sourceUrl, src), ct)));
        }

        // Inline scripts come after main.js, so their values win.
        foreach (var script in page.QuerySelectorAll("script:not([src])"))
            javascript.AppendLine(script.TextContent);

        descriptions = Literals.Read(javascript.ToString(), "sessionDescriptions");
        bios = Literals.Read(javascript.ToString(), "speakerBios");

        if (descriptions.Count == 0 || bios.Count == 0)
            throw new InvalidOperationException("Published abstracts/bios were not found; website markup changed.");
    }

    static readonly Dictionary<string, (double Latitude, double Longitude)> VenueCoordinates = new(StringComparer.Ordinal)
    {
        ["richfield-campus-umhlanga"] = (-29.7258, 31.0663),
        ["bbd-cape-town"] = (-33.8931, 18.5112),
        ["central-university-of-technology-cut"] = (-29.1213, 26.2128),
    };

    // Each city has a venue card ("Durban - November 21, 2026") and a registration card.
    async Task AddCitiesAndVenuesAsync(IDocument page)
    {
        var venueCards = page.QuerySelectorAll(".venue-card");
        if (venueCards.Length != Cities.Length)
            throw new InvalidOperationException("Expected one venue card per city.");

        foreach (var (_, cityName) in Cities)
        {
            var card = venueCards.Single(node => Text(node, "h3").StartsWith(cityName + " - ", StringComparison.Ordinal));
            var date = DateOnly.ParseExact(Text(card, "h3").Split(" - ", 2)[1], "MMMM d, yyyy", CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var cityId = Content.Slug(cityName);
            var venueName = Text(card, "h4");
            var venueId = Content.Slug(venueName);
            var address = Text(card, "p");
            var registration = page.QuerySelectorAll(".register-card").Single(node => Text(node, "h3") == cityName);
            var (latitude, longitude) = VenueCoordinates[venueId];

            conference.Cities.Add(new(cityId, cityName, date, venueId, Href(registration)));

            conference.Venues.Add(new(venueId, venueName, cityId, address, latitude, longitude, Href(card),
                await WriteMarkdownAsync("venues", venueId, venueName, address),
                await WriteImageAsync(card, "venues", venueId)));
        }
    }

    // Speakers are listed per city. Someone speaking in several cities is stored once, with all their cities.
    async Task AddSpeakersAsync(IDocument page)
    {
        foreach (var (key, cityName) in Cities)
        {
            var cityId = Content.Slug(cityName);

            foreach (var card in Required(page, $"#{key}-speakers").QuerySelectorAll(".speaker-card"))
            {
                var name = Text(card, "h4");
                var id = Content.Slug(name);

                if (speakers.TryGetValue(id, out var existing))
                {
                    if (existing.Name != name)
                        throw new InvalidOperationException($"Speaker ID collision: {id}");

                    existing.CityIds.Add(cityId);
                    continue;
                }

                // Prefer the embedded tagline; fall back to the title shown on the card.
                var bioName = Content.MatchName(name, bios.Keys);
                var bio = bioName is null ? default : bios[bioName];
                var body = Property(bio, "bio");
                var tagline = WebUtility.HtmlDecode(Property(bio, "tagline") ?? Text(card, ".speaker-title"));
                List<string> links =
                    [.. card.QuerySelectorAll(".social-links a").Select(link => Absolute(link.GetAttribute("href")!)).Distinct()];

                speakers.Add(id, new(id, name, tagline, [cityId], links, !string.IsNullOrWhiteSpace(body),
                    await WriteMarkdownAsync("speakers", id, name, body),
                    await WriteImageAsync(card, "speakers", id)));
            }
        }

        conference.Speakers.AddRange(speakers.Values.OrderBy(speaker => speaker.Id, StringComparer.Ordinal));
    }

    // Each city's agenda has full-width entries (registration, keynote, lunch) and per-track entries.
    // Session IDs include the city, because the same talk can run in several cities.
    async Task AddAgendaAsync(IDocument page)
    {
        foreach (var (key, cityName) in Cities)
        {
            var city = conference.Cities.Single(c => c.Name == cityName);
            var entries = Required(page, $"#{key}-schedule").QuerySelectorAll(".schedule-item, .schedule-full-width");
            if (entries.Length == 0)
                throw new InvalidOperationException($"No agenda entries for {cityName}.");

            foreach (var entry in entries)
            {
                var title = Text(entry, "h4");
                var (start, end) = TimeRange(Text(entry, ".schedule-time"), title);
                var track = entry.QuerySelector(".schedule-track-label")?.TextContent.Trim();

                // A "Lightning round" slot holds several short talks; import each one as its own session.
                var lightningTalks = entry.QuerySelectorAll(".lightning-talk");
                if (lightningTalks.Length > 0)
                {
                    await AddLightningTalksAsync(city, title, start, end, track, lightningTalks);
                    continue;
                }

                // Full-width entries sometimes use the speaker line for a note instead, such as "Lecture 4" for lunch.
                var speakerLine = entry.QuerySelector("p")?.TextContent.Trim();
                string? note = null;
                if (entry.ClassList.Contains("schedule-full-width") && speakerLine is not null && !AreKnownSpeakers(speakerLine))
                {
                    note = speakerLine;
                    speakerLine = null;
                }

                await AddSessionAsync(city, title, start, end, track, speakerLine, note);
            }
        }
    }

    // Lightning talks only list a start time. Each ends when the next starts; the last ends with the slot.
    async Task AddLightningTalksAsync(City city, string slotTitle, TimeOnly slotStart, TimeOnly slotEnd,
        string? track, IHtmlCollection<IElement> talks)
    {
        for (var index = 0; index < talks.Length; index++)
        {
            var talk = talks[index];
            var start = ParseTime(talk.GetAttribute("data-time")!);
            var end = index + 1 < talks.Length ? ParseTime(talks[index + 1].GetAttribute("data-time")!) : slotEnd;

            if (start < slotStart || end > slotEnd || end <= start)
                throw new InvalidOperationException($"Invalid lightning slot in {slotTitle}.");

            await AddSessionAsync(city, Text(talk, ".fw-semibold"), start, end,
                talk.GetAttribute("data-track") ?? track, talk.GetAttribute("data-speaker"), note: null);
        }
    }

    async Task AddSessionAsync(City city, string title, TimeOnly start, TimeOnly end, string? track,
        string? speakerNames, string? note)
    {
        List<string> speakerIds = string.IsNullOrWhiteSpace(speakerNames) ? [] :
            [.. speakerNames.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(name => Content.MatchName(Content.Slug(name), speakers.Keys)
                    ?? throw new InvalidOperationException($"Unknown speaker {name} in {title}."))];

        var id = $"{city.Id}-{Content.Slug(title)}";
        if (conference.Sessions.Any(session => session.Id == id))
            id = $"{id}-{start:HHmm}-{Content.Slug(track ?? "general")}";

        if (conference.Sessions.Any(session => session.Id == id))
            throw new InvalidOperationException($"Duplicate agenda entry {id}.");

        var body = descriptions.TryGetValue(title, out var description) ? description.GetString() : null;

        conference.Sessions.Add(new(
            id, title, city.Id,
            Timestamp(city, start), Timestamp(city, end),
            track, speakerIds, note,
            Kind: speakerIds.Count == 0 ? "general" : "session",
            HasAbstract: !string.IsNullOrWhiteSpace(body),
            Content: await WriteMarkdownAsync("sessions", id, title, body)));
    }

    // Sponsor tier headings name their cities, for example "GOLD - 2 CITY (CAPE TOWN, BLOEMFONTEIN)".
    // A sponsor can appear in several tiers, so its ID includes its cities.
    async Task AddSponsorsAsync(IDocument page)
    {
        foreach (var tier in page.QuerySelectorAll(".sponsor-tier"))
        {
            var label = Text(tier, "h3");
            List<string> cityIds = [.. Cities
                .Where(city => label.Contains(city.Name, StringComparison.OrdinalIgnoreCase))
                .Select(city => Content.Slug(city.Name))];

            if (cityIds.Count == 0)
                throw new InvalidOperationException($"Cannot determine sponsor cities from {label}.");

            foreach (var link in tier.QuerySelectorAll(".sponsor-link"))
            {
                var name = Required(link, "img").GetAttribute("alt")
                    ?? throw new InvalidOperationException("Sponsor is missing its name.");
                var id = $"{Content.Slug(name)}-{string.Join("-", cityIds)}";

                conference.Sponsors.Add(new(id, name, label, cityIds, Absolute(link.GetAttribute("href")!),
                    await WriteMarkdownAsync("sponsors", id, name, label),
                    await WriteImageAsync(link, "sponsors", id)));
            }
        }
    }

    // Writes "# Heading" plus the prose, with HTML entities decoded. Missing prose leaves just the heading.
    async Task<string> WriteMarkdownAsync(string kind, string id, string heading, string? body)
    {
        var path = $"{kind}/{id}.md";
        var prose = WebUtility.HtmlDecode(body ?? "").ReplaceLineEndings("\n").Trim();

        Directory.CreateDirectory(Path.Combine(folder, kind));
        await File.WriteAllTextAsync(Path.Combine(folder, path),
            prose.Length == 0 ? $"# {heading}\n" : $"# {heading}\n\n{prose}\n", ct);

        return path;
    }

    // Downloads the node's first <img> and saves it as {kind}/{id}.png or .jpg.
    async Task<string?> WriteImageAsync(IElement node, string kind, string id)
    {
        if (node.QuerySelector("img") is not { } img)
            return null;

        var uri = new Uri(sourceUrl, img.GetAttribute("src") ?? throw new InvalidOperationException("Image has no source."));
        var bytes = await http.GetByteArrayAsync(uri, ct);

        // Trust the file's contents rather than the extension in its URL.
        var extension = ImageExtension(bytes) ?? throw new InvalidOperationException($"Unsupported or invalid image: {uri}");
        var path = $"{kind}/{id}{extension}";

        Directory.CreateDirectory(Path.Combine(folder, kind));
        await File.WriteAllBytesAsync(Path.Combine(folder, path), bytes, ct);

        // Not every MAUI target can display AVIF, so convert it once here.
        if (extension == ".avif")
            path = await ConvertToPngAsync(kind, id, path);

        return path;
    }

    static string? ImageExtension(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Content.PngSignature))
            return ".png";

        if (bytes.AsSpan().StartsWith(Content.JpegSignature))
            return ".jpg";

        var header = Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 32)));
        return header.Contains("ftypavif", StringComparison.Ordinal) ? ".avif" : null;
    }

    // Uses macOS's built-in sips tool. The resulting snapshot works on every platform.
    async Task<string> ConvertToPngAsync(string kind, string id, string avifPath)
    {
        if (!OperatingSystem.IsMacOS())
            throw new InvalidOperationException("This source has AVIF images. Refresh on macOS (sips converts to PNG); " +
                "the checked-in snapshot works on every platform.");

        var pngPath = $"{kind}/{id}.png";
        using var process = Process.Start(new ProcessStartInfo("/usr/bin/sips")
        {
            ArgumentList = { "-s", "format", "png", Path.Combine(folder, avifPath), "--out", Path.Combine(folder, pngPath) },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new IOException("Could not start sips.");

        // Drain both streams so the process can't block on a full pipe.
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await stdout;
        var error = await stderr;

        if (process.ExitCode != 0)
            throw new IOException($"Image conversion failed: {error}");

        File.Delete(Path.Combine(folder, avifPath));
        return pngPath;
    }

    // --- Small helpers -------------------------------------------------------------------------

    bool AreKnownSpeakers(string names) =>
        names.Split(',', StringSplitOptions.TrimEntries)
            .All(name => Content.MatchName(Content.Slug(name), speakers.Keys) is not null);

    string Absolute(string relative) => new Uri(sourceUrl, relative).AbsoluteUri;

    string Href(IParentNode node) => Absolute(Required(node, "a").GetAttribute("href")!);

    // Agenda times are "HH:mm - HH:mm", with either a hyphen or an en dash.
    static (TimeOnly Start, TimeOnly End) TimeRange(string text, string title)
    {
        var parts = text.Split(['-', '\u2013'], StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TryParseTime(parts[0], out var start) || !TryParseTime(parts[1], out var end) || end <= start)
            throw new InvalidOperationException($"Invalid time range in {title}.");

        return (start, end);
    }

    static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    static TimeOnly ParseTime(string text) => TimeOnly.ParseExact(text, "HH:mm", CultureInfo.InvariantCulture);

    // All three cities are in South Africa: SAST, UTC+2, no daylight saving.
    static string Timestamp(City city, TimeOnly time) =>
        string.Create(CultureInfo.InvariantCulture, $"{city.Date}T{time:HH:mm}:00+02:00");

    static IElement Required(IParentNode node, string selector) =>
        node.QuerySelector(selector) ?? throw new InvalidOperationException($"Website is missing {selector}.");

    // Element text with whitespace collapsed, as a browser would render it.
    static string Text(IParentNode node, string selector) =>
        Regex.Replace(Required(node, selector).TextContent, @"\s+", " ").Trim();

    static string? Property(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) ? property.GetString() : null;
}


// ---------------------------------------------------------------------------------------------
// JavaScript object literals
// ---------------------------------------------------------------------------------------------

// Reads the page's data objects as JSON, without executing any downloaded JavaScript.
static class Literals
{
    // Merges every "const name = {...}" and "Object.assign(name, {...})" in order, so later values win.
    public static Dictionary<string, JsonElement> Read(string source, string name)
    {
        Dictionary<string, JsonElement> result = new(StringComparer.Ordinal);
        var prefix = $@"(?:\bconst\s+{Regex.Escape(name)}\s*=\s*|\bObject\.assign\(\s*{Regex.Escape(name)}\s*,\s*)(?=\{{)";

        foreach (Match match in Regex.Matches(source, prefix))
        {
            var start = match.Index + match.Length;
            var end = FindClosingBrace(source, start)
                ?? throw new FormatException($"Unterminated website data: {name}.");

            // main.js uses unquoted keys (tagline: "..."); quote them so the literal is valid JSON.
            var literal = Regex.Replace(source[start..(end + 1)], """ "(?:\\.|[^"\\])*" | (?<key>[A-Za-z_]\w*)\s*: """,
                key => key.Groups["key"].Success ? $"\"{key.Groups["key"].Value}\":" : key.Value,
                RegexOptions.IgnorePatternWhitespace);

            using var document = JsonDocument.Parse(literal, new() { AllowTrailingCommas = true });
            foreach (var property in document.RootElement.EnumerateObject())
                result[property.Name] = property.Value.Clone();
        }

        return result;
    }

    // Finds the "}" matching the "{" at start, ignoring braces inside double-quoted strings.
    static int? FindClosingBrace(string source, int start)
    {
        var depth = 0;
        bool quoted = false, escaped = false;

        for (var index = start; index < source.Length; index++)
        {
            var character = source[index];

            if (quoted)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') quoted = false;
            }
            else if (character == '"') quoted = true;
            else if (character == '{') depth++;
            else if (character == '}' && --depth == 0) return index;
        }

        return null;
    }
}


// ---------------------------------------------------------------------------------------------
// IDs and validation
// ---------------------------------------------------------------------------------------------

static class Content
{
    public static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    public static ReadOnlySpan<byte> JpegSignature => [0xff, 0xd8, 0xff];

    public static string? MatchName(string name, IEnumerable<string> candidates)
    {
        var names = candidates.ToArray();
        if (names.Contains(name, StringComparer.Ordinal))
            return name;

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var matches = names.Where(candidate => !string.IsNullOrWhiteSpace(candidate) &&
            (candidate.Contains(name, StringComparison.OrdinalIgnoreCase) ||
             name.Contains(candidate, StringComparison.OrdinalIgnoreCase))).ToArray();

        if (matches.Length > 1)
            throw new InvalidOperationException($"Ambiguous name {name}: {string.Join(", ", matches)}.");

        if (matches.Length == 0)
            return null;

        Console.WriteLine($"Matched '{name}' to '{matches[0]}' by name containment.");
        return matches[0];
    }

    // Lowercase kebab-case ASCII: "Jev—a deep dive" becomes "jeva-deep-dive".
    public static string Slug(string value)
    {
        var ascii = string.Concat(value.Normalize(NormalizationForm.FormD).Where(char.IsAscii));
        var slug = Regex.Replace(ascii.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 0 ? slug : throw new FormatException($"Cannot create an ID for {value}.");
    }

    // Every Markdown and image path referenced by the catalogue.
    public static IEnumerable<string> Files(Conference value) =>
        value.Sessions.Select(s => s.Content)
            .Concat(value.Speakers.SelectMany(s => Paths(s.Content, s.Image)))
            .Concat(value.Venues.SelectMany(v => Paths(v.Content, v.Image)))
            .Concat(value.Sponsors.SelectMany(s => Paths(s.Content, s.Image)));

    static IEnumerable<string> Paths(string content, string? image) => image is null ? [content] : [content, image];

    // Checks the written snapshot: IDs, relationships, times, and files.
    public static void Validate(string folder, Conference value)
    {
        if (value.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported content schema.");

        // IDs are unique and kebab-case within each kind.
        var cities = Ids(value.Cities.Select(c => c.Id));
        var speakers = Ids(value.Speakers.Select(s => s.Id));
        var venues = Ids(value.Venues.Select(v => v.Id));
        Ids(value.Sessions.Select(s => s.Id));
        Ids(value.Sponsors.Select(s => s.Id));

        // Relationships point at real entities, and each entity's files match its ID and heading.
        foreach (var city in value.Cities)
            Require(venues.Contains(city.VenueId), $"Unknown venue in {city.Id}.");

        foreach (var venue in value.Venues)
        {
            Require(cities.Contains(venue.CityId), $"Unknown city in {venue.Id}.");
            Document("venues", venue.Id, venue.Name, venue.Content, venue.Image);
        }

        foreach (var speaker in value.Speakers)
        {
            Require(speaker.CityIds.Count > 0 && speaker.CityIds.All(cities.Contains), $"Invalid cities in {speaker.Id}.");
            Document("speakers", speaker.Id, speaker.Name, speaker.Content, speaker.Image, speaker.HasBio);
        }

        foreach (var sponsor in value.Sponsors)
        {
            Require(sponsor.CityIds.Count > 0 && sponsor.CityIds.All(cities.Contains), $"Invalid cities in {sponsor.Id}.");
            Document("sponsors", sponsor.Id, sponsor.Name, sponsor.Content, sponsor.Image);
        }

        foreach (var session in value.Sessions)
        {
            Require(cities.Contains(session.CityId) && session.SpeakerIds.All(speakers.Contains),
                $"Broken relationship in {session.Id}.");
            Require(DateTimeOffset.Parse(session.End, CultureInfo.InvariantCulture) >
                DateTimeOffset.Parse(session.Start, CultureInfo.InvariantCulture), $"Invalid times in {session.Id}.");
            Document("sessions", session.Id, session.Title, session.Content, null, session.HasAbstract);
        }

        // Every referenced file exists inside the snapshot folder.
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        foreach (var relative in Files(value))
        {
            var path = Path.GetFullPath(Path.Combine(folder, relative));
            Require(path.StartsWith(root, StringComparison.Ordinal) && File.Exists(path), $"Missing or invalid asset: {relative}.");

            if (relative.EndsWith(".md", StringComparison.Ordinal))
                Require(File.ReadAllText(path).StartsWith("# ", StringComparison.Ordinal), $"Missing heading: {relative}.");
        }

        // Checks one entity's Markdown (path, heading, prose flag) and optional image (path and format).
        void Document(string kind, string id, string heading, string content, string? image, bool? hasProse = null)
        {
            Require(content == $"{kind}/{id}.md", $"Unexpected document path for {id}.");

            var text = File.ReadAllText(Path.Combine(folder, content));
            Require(text.StartsWith($"# {heading}\n", StringComparison.Ordinal), $"Incorrect heading in {content}.");

            if (hasProse is { } expected)
                Require(!string.IsNullOrWhiteSpace(text[(text.IndexOf('\n') + 1)..]) == expected,
                    $"Incorrect published-text flag for {id}.");

            if (image is null)
                return;

            Require(image == $"{kind}/{id}.png" || image == $"{kind}/{id}.jpg", $"Unexpected image path for {id}.");

            var bytes = File.ReadAllBytes(Path.Combine(folder, image));
            var signature = image.EndsWith(".png", StringComparison.Ordinal) ? PngSignature : JpegSignature;
            Require(bytes.AsSpan().StartsWith(signature), $"Invalid image: {image}.");
        }

        static HashSet<string> Ids(IEnumerable<string> source)
        {
            var items = source.ToArray();
            HashSet<string> ids = [with(StringComparer.Ordinal), .. items];

            Require(items.Length > 0, "Missing entities.");
            Require(ids.Count == items.Length, "Duplicate IDs: " +
                string.Join(", ", items.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key)));
            Require(items.All(id => Regex.IsMatch(id, "^[a-z0-9]+(-[a-z0-9]+)*$")), "Invalid entity IDs.");

            return ids;
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}


// ---------------------------------------------------------------------------------------------
// conference.json shape
// ---------------------------------------------------------------------------------------------

// Times are ISO 8601 strings with an offset. Content and Image are paths relative to conference.json.
sealed record Conference(int SchemaVersion, string Name, string SourceUrl, string TimeZone,
    List<City> Cities, List<Session> Sessions, List<Speaker> Speakers, List<Venue> Venues, List<Sponsor> Sponsors);

sealed record City(string Id, string Name, string Date, string VenueId, string RegistrationUrl);

// Kind is "session" for talks with speakers, or "general" for registration, breaks and so on.
sealed record Session(string Id, string Title, string CityId, string Start, string End, string? Track,
    List<string> SpeakerIds, string? Note, string Kind, bool HasAbstract, string Content);

sealed record Speaker(string Id, string Name, string Tagline, List<string> CityIds, List<string> Links,
    bool HasBio, string Content, string? Image);

sealed record Venue(string Id, string Name, string CityId, string Address, double Latitude, double Longitude,
    string MapUrl, string Content, string? Image);

sealed record Sponsor(string Id, string Name, string Tier, List<string> CityIds, string Url, string Content, string? Image);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(Conference))]
partial class ContentJson : JsonSerializerContext;
