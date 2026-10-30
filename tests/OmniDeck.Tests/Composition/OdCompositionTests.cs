using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App;
using OmniDeck.App.Composition;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Shell;
using OmniDeck.Core;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Tests.TestSupport;
using Serilog.Core;

namespace OmniDeck.Tests.Composition;

public sealed class OdCompositionTests
{
    [Fact]
    public void Container_ValidatesOnBuild()
    {
        using var temp = new OdTempDirectory();
        var services = new ServiceCollection()
            .AddOmniDeckCore(new OdDataRoot(temp.Path), Logger.None)
            .AddOmniDeckApp();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<OdCrashHandler>());
        Assert.Same(provider.GetRequiredService<OdTrayNotifier>(), provider.GetRequiredService<IOdNotifier>());
    }

    [Fact]
    public void Core_DoesNotDependOnTheShell()
    {
        var references = typeof(OdDataRoot).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain(typeof(OdApp).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain("Velopack", references, StringComparer.Ordinal);
        Assert.DoesNotContain("Wpf.Ui", references, StringComparer.Ordinal);
    }

    [Fact]
    public void SupportCode_IsStableForTheSameFailure()
    {
        var when = new DateTimeOffset(2026, 10, 30, 14, 5, 0, TimeSpan.FromHours(7));
        var exception = new InvalidOperationException("boom");

        var code = OdCrashHandler.SupportCode(exception, when);

        Assert.Equal(code, OdCrashHandler.SupportCode(new InvalidOperationException("boom"), when));
        Assert.Matches(@"^261030-1405-[0-9A-F]{4}$", code);
        Assert.NotEqual(code, OdCrashHandler.SupportCode(new InvalidOperationException("other"), when), StringComparer.Ordinal);
    }

    [Fact]
    public void Program_IsTheEntryPoint()
    {
        Assert.Equal("Program", typeof(OdApp).Assembly.EntryPoint?.DeclaringType?.Name);
        Assert.Null(typeof(OdApp).GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
    }
}
