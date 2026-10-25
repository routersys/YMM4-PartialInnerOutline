using System.Windows;
using Telemetry;

namespace PartialInnerOutline.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        PartialInnerOutlineTelemetry.EnsureStartedOnce();
        PartialInnerOutlineTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = TestEffects.Create();

        Assert.Equal($"{Texts.PartialInnerOutline} 3px", effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
