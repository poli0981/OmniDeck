using System.Windows;
using System.Windows.Controls;
using OmniDeck.Core.Modules;

namespace OmniDeck.Tests.Modules;

/// <summary>A module that records what the host asks of it.</summary>
public sealed class OdFakeModule(string id, OdRamClass ramClass = OdRamClass.Light) : IOdModule, IOdResidency
{
    public OdModuleInfo Info { get; } = new(id, "Module_" + id, "Apps24", "Test", ramClass);

    public IList<string> Calls { get; } = [];

    /// <summary>Whether it works while hidden, as a web app with notifications does.</summary>
    public bool StaysResident { get; set; }

    /// <summary>What <see cref="CanSuspendAsync"/> answers; false behaves like a playing media module.</summary>
    public bool CanSuspend { get; set; } = true;

    public IOdModuleContext? Context { get; private set; }

    /// <summary>Makes <see cref="HibernateAsync"/> fail, like a module with a bug.</summary>
    public bool ThrowOnHibernate { get; set; }

    /// <summary>Makes <see cref="ActivateAsync"/> fail.</summary>
    public bool ThrowOnActivate { get; set; }

    /// <summary>Makes <see cref="CloseAsync"/> fail.</summary>
    public bool ThrowOnClose { get; set; }

    /// <summary>Makes <see cref="DeactivateAsync"/> fail.</summary>
    public bool ThrowOnDeactivate { get; set; }

    /// <summary>How often the host said the module left the screen; kept out of <see cref="Calls"/>.</summary>
    public int Deactivations { get; private set; }

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
        if (ThrowOnActivate)
        {
            throw new InvalidOperationException("activate failed");
        }

        Calls.Add("activate");
    }

    public Task DeactivateAsync()
    {
        Deactivations++;
        return ThrowOnDeactivate ? throw new InvalidOperationException("deactivate failed") : Task.CompletedTask;
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
        if (ThrowOnClose)
        {
            throw new InvalidOperationException("close failed");
        }

        Calls.Add("close");
        return Task.CompletedTask;
    }
}
