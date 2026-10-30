using OmniDeck.App.Updates;

namespace OmniDeck.Tests.Updates;

/// <summary>An update service that answers from its properties instead of GitHub.</summary>
internal sealed class OdFakeUpdates : IOdUpdateService
{
    public bool Installed { get; set; } = true;

    public string? Available { get; set; }

    public Exception? Failure { get; set; }

    public bool Applied { get; private set; }

    public int Checks { get; private set; }

    public bool IsInstalled => Installed;

    public Task<string?> CheckAsync(CancellationToken cancellationToken)
    {
        Checks++;
        return Failure is { } failure ? Task.FromException<string?>(failure) : Task.FromResult(Available);
    }

    public Task DownloadAsync(IProgress<int> progress, CancellationToken cancellationToken)
    {
        if (Failure is { } failure)
        {
            return Task.FromException(failure);
        }

        progress.Report(100);
        return Task.CompletedTask;
    }

    public void ApplyAndRestart() => Applied = true;
}
