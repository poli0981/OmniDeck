using System.IO.Compression;
using System.Text;

namespace OmniDeck.Core.Reports;

/// <summary>
/// A bug report as OmniDeck saves it (docs/legal/PRIVACY.md §3): the issue text, the system facts, the
/// end of the log and, for a web app, its console, each already scrubbed. It is written to a zip on
/// this PC and never sent; the user attaches it to the issue.
/// </summary>
public sealed record OdBugReport
{
    /// <summary>OmniDeck's version and commit, as About shows them.</summary>
    public required string Version { get; init; }

    /// <summary>Windows' version and build.</summary>
    public required string Windows { get; init; }

    public required string Preset { get; init; }

    /// <summary>What the report is about: a module's name, or OmniDeck in general.</summary>
    public required string Subject { get; init; }

    /// <summary>The issue's text, laid out like the GitHub form: issue.md.</summary>
    public required string Issue { get; init; }

    /// <summary>Versions, modules, memory and sign-in counts: system.json.</summary>
    public required string SystemInfo { get; init; }

    /// <summary>The end of the log: logs.txt.</summary>
    public required string Logs { get; init; }

    /// <summary>The web app's console, when the report is about one: console-‹app›.txt.</summary>
    public string? ConsoleLog { get; init; }

    /// <summary>The id of the web app whose console <see cref="ConsoleLog"/> is.</summary>
    public string? ConsoleOf { get; init; }

    /// <summary>Writes the report to <paramref name="stream"/> as a zip.</summary>
    public void WriteZip(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, "issue.md", Issue);
        Add(zip, "system.json", SystemInfo);
        Add(zip, "logs.txt", Logs);
        if (ConsoleLog is not null && ConsoleOf is not null)
        {
            Add(zip, "console-" + ConsoleOf + ".txt", ConsoleLog);
        }
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }
}
