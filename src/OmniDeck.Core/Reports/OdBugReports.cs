using System.Globalization;
using OmniDeck.Core.Storage;

namespace OmniDeck.Core.Reports;

/// <summary>The data root's reports folder: each report is a bugreport-‹stamp›.zip, and the ten newest are kept.</summary>
public sealed class OdBugReports(OdDataRoot dataRoot, TimeProvider time)
{
    /// <summary>How many reports the folder keeps.</summary>
    public const int Kept = 10;

    /// <summary>Saves <paramref name="report"/>, deletes the reports beyond the newest <see cref="Kept"/>, and returns its path.</summary>
    public string Save(OdBugReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(dataRoot);
        var folder = dataRoot.ReportsDirectory;
        Directory.CreateDirectory(folder);
        var stamp = time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(folder, "bugreport-" + stamp + ".zip");
        var n = 1;
        while (File.Exists(path))
        {
            n++;
            path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"bugreport-{stamp}-{n}.zip"));
        }

        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            report.WriteZip(file);
        }

        foreach (var old in Directory.GetFiles(folder, "bugreport-*.zip").OrderDescending(StringComparer.Ordinal).Skip(Kept))
        {
            File.Delete(old);
        }

        return path;
    }
}
