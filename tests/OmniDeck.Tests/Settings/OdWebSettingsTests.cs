using OmniDeck.Core.Settings;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Settings;

public sealed class OdWebSettingsTests : IDisposable
{
    private readonly OdTempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void CleanChoices_ComeBackAsTheSameInstance()
    {
        var web = new OdWebSettings().With("discord", app => app with { Notifications = false, Permissions = Answers(("Microphone", true)) });

        Assert.Same(web, web.Normalized());
        Assert.Same(web, new OdSettings { Web = web }.Normalized().Web);
    }

    [Fact]
    public void AHandEditedFile_IsRepaired()
    {
        var web = new OdWebSettings
        {
            Apps = new Dictionary<string, OdWebAppSettings>(StringComparer.Ordinal)
            {
                [" discord "] = new() { Permissions = Answers((" Camera", false), ("", true)) },
                [""] = new(),
                ["gmail"] = null!,
            },
        };

        var repaired = web.Normalized();

        var discord = Assert.Single(repaired.Apps);
        Assert.Equal("discord", discord.Key);
        Assert.Equal(Answers(("Camera", false)), discord.Value.Permissions);
    }

    [Fact]
    public void Choices_SurviveARestart()
    {
        var root = new OdDataRoot(_temp.Path);
        new OdSettingsService(root, Logger.None).Update(s => s with
        {
            Web = s.Web.With("discord", app => app with { Notifications = true, Permissions = Answers(("Microphone", true)) }),
        });

        var web = new OdSettingsService(root, Logger.None).Current.Web;

        Assert.True(web.For("discord").Notifications);
        Assert.True(web.For("discord").Permissions["Microphone"]);
        Assert.Null(web.For("gmail").Notifications);
        Assert.Empty(web.For("gmail").Permissions);
    }

    private static Dictionary<string, bool> Answers(params (string Kind, bool Allowed)[] answers) =>
        answers.ToDictionary(answer => answer.Kind, answer => answer.Allowed, StringComparer.Ordinal);
}
