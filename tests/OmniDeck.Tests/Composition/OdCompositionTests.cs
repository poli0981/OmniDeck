using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OmniDeck.App;
using OmniDeck.App.Composition;
using OmniDeck.App.Diagnostics;
using OmniDeck.App.Shell;
using OmniDeck.Core;
using OmniDeck.Core.Modules;
using OmniDeck.Core.Storage;
using OmniDeck.Modules;
using OmniDeck.Tests.TestSupport;
using OmniDeck.Web;
using OmniDeck.Web.Catalog;
using OmniDeck.Web.Runtime;
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
            .AddOmniDeckModules()
            .AddOmniDeckWeb(OdCatalog.Empty, new OdWebOptions())
            .AddOmniDeckApp();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<OdCrashHandler>());
        Assert.StartsWith("OmniDeck/", provider.GetRequiredService<HttpClient>().DefaultRequestHeaders.UserAgent.ToString(), StringComparison.Ordinal);
        Assert.Same(provider.GetRequiredService<OdTrayNotifier>(), provider.GetRequiredService<IOdNotifier>());
        Assert.Equal(new[] { "clock", "timer", "calculator", "weather", "calendar" }, provider.GetRequiredService<OdModuleHost>().Modules.Select(m => m.Info.Id), StringComparer.Ordinal);
    }

    [Fact]
    public void EachCatalogApp_BecomesAModule_AfterTheBuiltInOnes()
    {
        using var temp = new OdTempDirectory();
        var catalog = OdCatalogService.Load(Path.Combine(OdRepository.Root, "catalog", "webapps.json"), temp.Combine("none.json"), new DateOnly(2026, 11, 2), Logger.None);
        using var provider = new ServiceCollection()
            .AddOmniDeckCore(new OdDataRoot(temp.Path), Logger.None)
            .AddOmniDeckModules()
            .AddOmniDeckWeb(catalog, new OdWebOptions())
            .AddOmniDeckApp()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var modules = provider.GetRequiredService<OdModuleHost>().Modules;

        Assert.Equal(5 + catalog.Apps.Count, modules.Count);
        Assert.Equal(catalog.Apps.Select(app => app.Id), modules.Skip(5).Select(module => module.Info.Id), StringComparer.Ordinal);
        Assert.Equal("Gmail", modules.Single(module => string.Equals(module.Info.Id, "gmail", StringComparison.Ordinal)).Info.Title);
    }

    [Fact]
    public void Core_DoesNotDependOnTheShell()
    {
        var references = typeof(OdDataRoot).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain(typeof(OdApp).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain("Velopack", references, StringComparer.Ordinal);
        Assert.DoesNotContain("Wpf.Ui", references, StringComparer.Ordinal);
        Assert.DoesNotContain(typeof(OdModulesServiceCollectionExtensions).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain(typeof(OdWebServiceCollectionExtensions).Assembly.GetName().Name, references, StringComparer.Ordinal);
    }

    [Fact]
    public void TheWebRunner_DependsOnCore_NeverOnTheShellOrTheModules()
    {
        var references = typeof(OdWebServiceCollectionExtensions).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.Contains(typeof(OdDataRoot).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain(typeof(OdApp).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain(typeof(OdModulesServiceCollectionExtensions).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain("Velopack", references, StringComparer.Ordinal);
    }

    [Fact]
    public void Modules_DependOnCoreButNeverOnTheShell()
    {
        var references = typeof(OdModulesServiceCollectionExtensions).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.Contains(typeof(OdDataRoot).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain(typeof(OdApp).Assembly.GetName().Name, references, StringComparer.Ordinal);
        Assert.DoesNotContain("Velopack", references, StringComparer.Ordinal);
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
