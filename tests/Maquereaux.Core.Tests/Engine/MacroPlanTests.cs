using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Engine;

public class MacroPlanTests
{
    [Fact]
    public void Cycle_mode_seven_ticks_gives_AZEAZEA()
    {
        var plan = MacroPlan.Build(TestProfiles.Aze(SequenceMode.Cycle));
        var keys = plan.Enumerate().Take(7).Select(t => Assert.Single(t.Steps).Key.VirtualKey).ToList();
        Assert.Equal(new ushort[] { VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E, VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E, VirtualKeys.A }, keys);
    }

    [Fact]
    public void Burst_mode_each_tick_contains_whole_sequence_in_order()
    {
        var plan = MacroPlan.Build(TestProfiles.Aze(SequenceMode.Burst));
        foreach (var tick in plan.Enumerate().Take(5))
            Assert.Equal(new ushort[] { VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E }, tick.Steps.Select(s => s.Key.VirtualKey));
    }

    [Fact]
    public void RepeatCount_zero_is_infinite()
    {
        var plan = MacroPlan.Build(TestProfiles.Aze(repeat: 0));
        Assert.True(plan.IsInfinite);
        Assert.Equal(10_000, plan.Enumerate().Take(10_000).Count());
    }

    [Theory]
    [InlineData(SequenceMode.Cycle, 2, 6)]
    [InlineData(SequenceMode.Burst, 2, 2)]
    [InlineData(SequenceMode.Cycle, 1, 3)]
    public void RepeatCount_limits_total_ticks(SequenceMode mode, int repeat, int expectedTicks)
    {
        var plan = MacroPlan.Build(TestProfiles.Aze(mode, repeat));
        Assert.Equal(expectedTicks, plan.TotalTicks);
        Assert.Equal(expectedTicks, plan.Enumerate().Count());
    }

    [Fact]
    public void Cycle_uses_per_step_delay_or_global_interval()
    {
        var p = TestProfiles.Aze(interval: 25);
        p.Steps[1].DelayAfterMs = 100;
        var plan = MacroPlan.Build(p);
        Assert.Equal(new double[] { 25, 100, 25 }, plan.Cycle.Select(t => t.DelayAfterMs));
    }

    [Fact]
    public void Burst_ignores_per_step_delay()
    {
        var p = TestProfiles.Aze(SequenceMode.Burst, interval: 40);
        p.Steps[0].DelayAfterMs = 500;
        var tick = Assert.Single(MacroPlan.Build(p).Cycle);
        Assert.Equal(40, tick.DelayAfterMs);
    }

    [Fact]
    public void Interval_below_minimum_is_clamped_to_1ms()
    {
        var p = TestProfiles.Aze(interval: 0);
        p.Steps[0].DelayAfterMs = -5;
        var plan = MacroPlan.Build(p);
        Assert.All(plan.Cycle, t => Assert.Equal(1, t.DelayAfterMs));
    }

    [Fact]
    public void Instant_tick_precomputes_down_up_pairs()
    {
        var tick = Assert.Single(MacroPlan.Build(TestProfiles.Aze(SequenceMode.Burst)).Cycle);
        Assert.True(tick.IsInstant);
        Assert.Equal(new[] { "A↓", "A↑", "Z↓", "Z↑", "E↓", "E↑" }, tick.InstantActions.Select(a => a.ToString()));
    }

    [Fact]
    public void Tick_with_hold_is_not_instant()
    {
        var tick = Assert.Single(MacroPlan.Build(TestProfiles.Aze(SequenceMode.Burst, hold: 3)).Cycle);
        Assert.False(tick.IsInstant);
        Assert.Empty(tick.InstantActions);
    }

    [Fact]
    public void Empty_sequence_throws()
    {
        Assert.Throws<ArgumentException>(() => MacroPlan.Build(new Profile()));
    }

    [Fact]
    public void Null_profile_throws()
    {
        Assert.Throws<ArgumentNullException>(() => MacroPlan.Build(null!));
    }

    [Fact]
    public void DistinctKeys_deduplicates_by_virtual_key()
    {
        var p = TestProfiles.Aze();
        p.Steps.Add(new MacroStep(TestProfiles.A));
        Assert.Equal(3, MacroPlan.Build(p).DistinctKeys.Count());
    }
}
