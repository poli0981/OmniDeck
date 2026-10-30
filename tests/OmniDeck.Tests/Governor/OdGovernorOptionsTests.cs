using System.Globalization;
using OmniDeck.Core.Governor;
using OmniDeck.Core.Settings;

namespace OmniDeck.Tests.Governor;

public sealed class OdGovernorOptionsTests
{
    private const long MiB = OdGovernorOptions.MiB;
    private const long GiB = OdGovernorOptions.GiB;

    [Theory]
    [InlineData(8, 2_867, 4_096)]
    [InlineData(16, 4_608, 6_144)]
    [InlineData(64, 4_608, 6_144)]
    public void DefaultCaps_FollowTheMachinesMemory(long ramGiB, long softMiB, long hardMiB)
    {
        var options = OdGovernorOptions.From(new OdMemorySettings(), ramGiB * GiB);

        Assert.Equal(softMiB, options.SoftCapBytes / MiB);
        Assert.Equal(hardMiB, options.HardCapBytes / MiB);
        Assert.Equal(TimeSpan.FromMinutes(5), options.IdleAfter);
        Assert.Equal(TimeSpan.FromMinutes(30), options.HibernateAfter);
        Assert.Equal(TimeSpan.FromSeconds(10), options.PollInterval);
    }

    [Fact]
    public void CustomCaps_AreMegabytes_AndTheHardCapNeverSitsBelowTheSoftOne()
    {
        var custom = OdGovernorOptions.From(new OdMemorySettings { SoftCapMegabytes = 1_024, HardCapMegabytes = 2_048 }, 32 * GiB);
        var softOnly = OdGovernorOptions.From(new OdMemorySettings { SoftCapMegabytes = 8_192 }, 32 * GiB);

        Assert.Equal(1 * GiB, custom.SoftCapBytes);
        Assert.Equal(2 * GiB, custom.HardCapBytes);
        Assert.Equal(8 * GiB, softOnly.HardCapBytes);
    }

    [Fact]
    public void Normalized_RepairsThresholdsAndKeepRunningIds()
    {
        var repaired = new OdSettings
        {
            Memory = new OdMemorySettings
            {
                IdleAfterMinutes = 0,
                HibernateAfterMinutes = -5,
                SoftCapMegabytes = 4_000,
                HardCapMegabytes = 1_000,
                KeepRunning = ["web", "", "web"],
            },
        }.Normalized().Memory;

        Assert.Equal(1, repaired.IdleAfterMinutes);
        Assert.Equal(1, repaired.HibernateAfterMinutes);
        Assert.Equal(4_000, repaired.HardCapMegabytes);
        Assert.Equal(new[] { "web" }, repaired.KeepRunning, StringComparer.Ordinal);
        Assert.Equal(OdMemorySettings.MaxMinutes, new OdMemorySettings { IdleAfterMinutes = int.MaxValue }.Normalized().HibernateAfterMinutes);
        Assert.Equal(0, new OdMemorySettings { SoftCapMegabytes = -1 }.Normalized().SoftCapMegabytes);
    }

    [Fact]
    public void Normalized_KeepsSettingsWithoutAMemorySection()
    {
        var settings = new OdSettings { Memory = null! }.Normalized();

        Assert.Equal(new OdMemorySettings().IdleAfterMinutes, settings.Memory.IdleAfterMinutes);
        Assert.Equal(new OdSettings().Normalized(), new OdSettings().Normalized());
    }

    [Fact]
    public void Bytes_ReadLikeTaskManager()
    {
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:0} MB", 412.0), OdBytes.Format(412 * MiB));
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:0.#} GB", 4.5), OdBytes.Format(45 * GiB / 10));
    }

    [Fact]
    public void ProcessMemory_MeasuresThisProcess()
    {
        var memory = new OdProcessMemory();

        memory.Reclaim();

        Assert.True(memory.PhysicalBytes > GiB);
        Assert.True(memory.CommittedBytes() > MiB);
    }
}
