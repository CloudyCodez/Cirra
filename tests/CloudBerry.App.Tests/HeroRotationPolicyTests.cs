using CloudBerry.App.Services;

namespace CloudBerry.App.Tests;

public sealed class HeroRotationPolicyTests
{
    [Fact]
    public void PauseCanBeResumedWithoutClearingSelection()
    {
        var policy = new HeroRotationPolicy();
        Assert.False(policy.CanAdvance(60000, true, false, paused: true));
        Assert.True(policy.CanAdvance(60000, true, false, paused: false));
    }

    [Fact]
    public void InteractionAndNonHomeStatesPreventRotation()
    {
        var policy = new HeroRotationPolicy();
        policy.Interact(20000);
        Assert.False(policy.CanAdvance(31999, true, false));
        Assert.True(policy.CanAdvance(32000, true, false));
        Assert.False(policy.CanAdvance(60000, false, false));
        Assert.False(policy.CanAdvance(60000, true, true));
    }
}
