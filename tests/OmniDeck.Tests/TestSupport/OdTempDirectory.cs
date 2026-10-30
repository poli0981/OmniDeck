namespace OmniDeck.Tests.TestSupport;

/// <summary>A unique, empty folder under %TEMP% that is removed with everything in it.</summary>
public sealed class OdTempDirectory : IDisposable
{
    public OdTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"od-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
