using OmniDeck.Core.Runtime;

namespace OmniDeck.Tests.Runtime;

public sealed class OdSingleInstanceGuardTests
{
    [Fact]
    public void FirstGuardIsPrimary_SecondIsNot()
    {
        var suffix = $".test-{Guid.NewGuid():N}";
        using var first = new OdSingleInstanceGuard(suffix);
        using var second = new OdSingleInstanceGuard(suffix);

        Assert.True(first.IsPrimaryInstance);
        Assert.False(second.IsPrimaryInstance);
    }

    [Fact]
    public void Signal_FromASecondInstance_ActivatesTheFirst()
    {
        var suffix = $".test-{Guid.NewGuid():N}";
        using var activated = new ManualResetEventSlim();
        using var first = new OdSingleInstanceGuard(suffix);
        first.StartListening(activated.Set);

        using (var second = new OdSingleInstanceGuard(suffix))
        {
            second.SignalPrimaryInstance();
        }

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AfterTheFirstIsDisposed_ANewGuardIsPrimary()
    {
        var suffix = $".test-{Guid.NewGuid():N}";
        new OdSingleInstanceGuard(suffix).Dispose();

        using var next = new OdSingleInstanceGuard(suffix);

        Assert.True(next.IsPrimaryInstance);
    }
}
