namespace OmniDeck.Tests.TestSupport;

/// <summary>Finds the checkout the tests run from, for tests that read the sources themselves.</summary>
public static class OdRepository
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmniDeck.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("OmniDeck.slnx was not found above " + AppContext.BaseDirectory);
    }
}
