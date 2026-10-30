using System.Windows;
using System.Windows.Controls;
using OmniDeck.Core.Modules;

namespace OmniDeck.Tests.Modules;

/// <summary>A module that records what the host asks of it.</summary>
public sealed class OdFakeModule(string id, OdRamClass ramClass = OdRamClass.Light) : IOdModule
{
    public OdModuleInfo Info { get; } = new(id, "Module_" + id, "Apps24", "Test", ramClass);

    public IList<string> Calls { get; } = [];

    /// <summary>What <see cref="CanSuspendAsync"/> answers; false behaves like a playing media module.</summary>
    public bool CanSuspend { get; set; } = true;

    public IOdModuleContext? Context { get; private set; }

    /// <summary>Makes <see cref="HibernateAsync"/> fail, like a module with a bug.</summary>
    public bool ThrowOnHibernate { get; set; }

    public Task InitializeAsync(IOdModuleContext context)
    {
        Context = context;
        Calls.Add("init");
        return Task.CompletedTask;
    }

    public FrameworkElement CreateView()
    {
        Calls.Add("view");
        return new Border();
    }

    public async Task ActivateAsync()
    {
        // A real await, so the host's one-at-a-time gate is exercised across a dispatcher turn.
        await Task.Yield();
        Calls.Add("activate");
    }

    public Task<bool> CanSuspendAsync() => Task.FromResult(CanSuspend);

    public Task SuspendAsync(OdSuspendReason reason)
    {
        Calls.Add("suspend:" + reason);
        return Task.CompletedTask;
    }

    public Task HibernateAsync()
    {
        if (ThrowOnHibernate)
        {
            throw new InvalidOperationException("hibernate failed");
        }

        Calls.Add("hibernate");
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        Calls.Add("close");
        return Task.CompletedTask;
    }
}
