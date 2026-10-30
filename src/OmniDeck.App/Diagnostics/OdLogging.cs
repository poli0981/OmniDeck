using OmniDeck.Core.Storage;
using Serilog;
using Serilog.Events;

namespace OmniDeck.App.Diagnostics;

/// <summary>
/// Serilog to rolling files under the data root's <c>logs\</c> folder, kept for seven days
/// (docs/SPEC.md §5). Nothing leaves the machine; bug reports attach a tail of these files only when
/// the user asks for one.
/// </summary>
internal static class OdLogging
{
    private const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static ILogger CreateLogger(OdDataRoot dataRoot) =>
        new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.File(
                System.IO.Path.Combine(dataRoot.LogsDirectory, "omnideck-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: Template,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
}
