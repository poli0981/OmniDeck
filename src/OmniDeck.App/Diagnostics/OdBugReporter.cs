using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using OmniDeck.App.Settings;
using OmniDeck.Core;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Localization;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Reports;
using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Web.Apps;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Diagnostics;
using OmniDeck.Web.Runtime;

namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Puts a bug report together (docs/legal/PRIVACY.md §3): OmniDeck's version, Windows' build, the UI
/// preset, the modules and what they use, the end of the log and, for a web app, its console, each
/// passed through <see cref="OdScrubber"/>. Used from the UI thread.
/// </summary>
public sealed class OdBugReporter(
    IOdSettingsService settings,
    OdModuleHost host,
    OdRamGovernor governor,
    OdDataRoot dataRoot,
    OdCatalog catalog,
    OdSignInHealth health,
    TimeProvider time)
{
    /// <summary>How many log lines a report carries.</summary>
    public const int LogLines = 500;

    private static readonly OdVersionInfo Version = OdVersionInfo.FromAssembly(typeof(OdBugReporter).Assembly);

    /// <summary>What a report can be about: OmniDeck in general, then each module.</summary>
    public IReadOnlyList<OdChoice<string>> Subjects() =>
        [new("", OdLocalization.Get("Report_General")), .. host.Modules.Select(hosted => new OdChoice<string>(hosted.Info.Id, hosted.Info.Title))];

    /// <summary>A report about the module <paramref name="subjectId"/> (empty: OmniDeck in general), with the error OmniDeck showed, if any.</summary>
    public OdBugReport Compose(string subjectId, string? error)
    {
        ArgumentNullException.ThrowIfNull(subjectId);
        var hosted = subjectId.Length > 0 ? host.Find(subjectId) : null;
        var web = hosted?.Module as OdWebAppModule;
        var subject = hosted?.Info.Title ?? OdLocalization.Get("Report_General");
        var windows = WindowsVersion();
        var preset = settings.Current.Preset;
        var errors = web is null ? "" : OdConsoleLog.Write(web.Console.Errors);
        return new OdBugReport
        {
            Version = Version.DisplayText,
            Windows = windows,
            Preset = preset,
            Subject = subject,
            Issue = OdScrubber.Scrub(Issue(windows, preset, hosted?.Info.Id ?? "OmniDeck in general", errors, error)),
            SystemInfo = OdScrubber.Scrub(SystemInfo(windows, preset)),
            Logs = OdScrubber.Scrub(LogTail()),
            ConsoleLog = web is null ? null : OdScrubber.Scrub(OdConsoleLog.Write(web.Console.All)),
            ConsoleOf = web?.Info.Id,
        };
    }

    /// <summary>Windows' version and build, as the bug form asks for them.</summary>
    internal static string WindowsVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var display = key?.GetValue("DisplayVersion") as string;
        var build = key?.GetValue("CurrentBuild") as string ?? Environment.OSVersion.Version.Build.ToString(CultureInfo.InvariantCulture);
        var revision = key?.GetValue("UBR") is int ubr ? "." + ubr.ToString(CultureInfo.InvariantCulture) : "";
        return string.IsNullOrEmpty(display) ? $"Windows 11 ({build}{revision})" : $"Windows 11 {display} ({build}{revision})";
    }

    // The issue is for the maintainer: it stays in English whatever the UI language, so modules go by id.
    private string Issue(string windows, string preset, string subject, string consoleErrors, string? error)
    {
        var issue = new StringBuilder();
        Section(issue, "OmniDeck version", Version.DisplayText);
        Section(issue, "Windows version / build", windows);
        Section(issue, "UI preset", preset);
        Section(issue, "Affected module / web app", subject);
        Section(issue, "What happened, and what did you expect?", "_What you did, what happened, and what you expected instead._");
        Section(issue, "Console errors", consoleErrors.Length == 0 ? "_None captured._" : "```text\n" + consoleErrors + "\n```");
        if (error is not null)
        {
            Section(issue, "The error OmniDeck showed", "```text\n" + error + "\n```");
        }

        var snapshot = governor.Snapshot;
        var now = time.GetUtcNow();
        var modules = new StringBuilder("| Module | State | Hidden for | Memory |\n|---|---|---|---|\n");
        foreach (var module in host.Modules.Where(module => module.State != OdModuleState.Created))
        {
            var hidden = module.State == OdModuleState.Active ? "" : ((int)(now - module.LastActive).TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min";
            var memory = snapshot?.For(module.Info.Id) is { } bytes ? OdBytes.Format(bytes) : "";
            modules.Append(CultureInfo.InvariantCulture, $"| {module.Info.Id} | {module.State} | {hidden} | {memory} |\n");
        }

        Section(issue, "Modules", modules.ToString().TrimEnd());
        return issue.ToString();
    }

    private static void Section(StringBuilder issue, string heading, string body) =>
        issue.Append("### ").Append(heading).Append("\n\n").Append(body).Append("\n\n");

    private string SystemInfo(string windows, string preset)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartObject();
            json.WriteString("version", Version.Version);
            json.WriteString("commit", Version.Commit);
            json.WriteString("windows", windows);
            json.WriteString("webView2", OdWebRuntime.InstalledVersion());
            json.WriteString("preset", preset);
            json.WriteString("language", CultureInfo.CurrentUICulture.Name);
            json.WriteString("catalog", catalog.Updated);
            WriteMemory(json);
            json.WriteStartArray("modules");
            var now = time.GetUtcNow();
            foreach (var module in host.Modules)
            {
                json.WriteStartObject();
                json.WriteString("id", module.Info.Id);
                json.WriteString("state", module.State.ToString());
                json.WriteNumber("hiddenMinutes", module.State is OdModuleState.Created or OdModuleState.Active ? 0 : (int)(now - module.LastActive).TotalMinutes);
                if (governor.Snapshot?.For(module.Info.Id) is { } bytes)
                {
                    json.WriteNumber("memoryBytes", bytes);
                }

                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartObject("signInCookies");
            foreach (var profile in catalog.Apps.Select(app => app.Profile).Distinct(StringComparer.Ordinal))
            {
                if (health.Kept(profile) is { } count)
                {
                    json.WriteNumber(profile, count);
                }
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void WriteMemory(Utf8JsonWriter json)
    {
        var options = governor.Options;
        json.WriteStartObject("memory");
        json.WriteNumber("committedBytes", governor.CommittedBytes);
        if (governor.Snapshot is { } snapshot)
        {
            json.WriteNumber("shellBytes", snapshot.ShellBytes);
            json.WriteNumber("sharedBytes", snapshot.SharedBytes);
        }

        json.WriteNumber("softCapBytes", options.SoftCapBytes);
        json.WriteNumber("hardCapBytes", options.HardCapBytes);
        json.WriteEndObject();
    }

    /// <summary>The last <see cref="LogLines"/> lines of the logs, newest file last.</summary>
    private string LogTail()
    {
        var lines = new List<string>();
        var files = Directory.Exists(dataRoot.LogsDirectory)
            ? Directory.GetFiles(dataRoot.LogsDirectory, "omnideck-*.log").OrderByDescending(File.GetLastWriteTimeUtc).ToList()
            : [];
        foreach (var file in files.TakeWhile(_ => lines.Count < LogLines))
        {
            lines.InsertRange(0, ReadShared(file).TakeLast(LogLines - lines.Count));
        }

        return string.Join("\n", lines);
    }

    // The log is open for writing; read it without getting in the way.
    private static List<string> ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }
}
