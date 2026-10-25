using Maquereaux.Core.Engine;
using Maquereaux.Core.Stats;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Stats;

public class TimingStatsTests
{
    [Fact]
    public void Empty_series_gives_empty_stats()
    {
        var s = TimingStats.Compute(Array.Empty<double>());
        Assert.True(s.IsEmpty);
        Assert.Same(TimingStats.Empty, s);
        Assert.Equal(0, s.RateHz);
    }

    [Fact]
    public void Known_dataset_mean_min_max_stddev()
    {
        // Jeu classique : moyenne 5, écart-type population 2
        var s = TimingStats.Compute(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 });
        Assert.Equal(8, s.Count);
        Assert.Equal(5, s.Mean, 10);
        Assert.Equal(2, s.Min);
        Assert.Equal(9, s.Max);
        Assert.Equal(2, s.StdDev, 10);
        Assert.Equal(4.5, s.P50, 10);
        Assert.Equal(200, s.RateHz, 10);
    }

    [Fact]
    public void Percentiles_use_linear_interpolation_like_excel()
    {
        // 1..100 : PERCENTILE.INC(95%) = 95,05 ; 99% = 99,01 ; médiane = 50,5
        var data = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();
        var s = TimingStats.Compute(data);
        Assert.Equal(50.5, s.P50, 10);
        Assert.Equal(95.05, s.P95, 10);
        Assert.Equal(99.01, s.P99, 10);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 40)]
    [InlineData(50, 25)]
    [InlineData(25, 17.5)]
    [InlineData(-5, 10)]
    [InlineData(150, 40)]
    public void Percentile_edges(double p, double expected)
    {
        Assert.Equal(expected, TimingStats.Percentile(new double[] { 10, 20, 30, 40 }, p), 10);
    }

    [Fact]
    public void Percentile_single_and_empty()
    {
        Assert.Equal(7, TimingStats.Percentile(new double[] { 7 }, 95));
        Assert.Equal(0, TimingStats.Percentile(Array.Empty<double>(), 95));
    }

    [Fact]
    public void Constant_series_has_zero_jitter()
    {
        var s = TimingStats.Compute(Enumerable.Repeat(10.0, 1000));
        Assert.Equal(0, s.StdDev, 10);
        Assert.Equal(10, s.P99, 10);
        Assert.Equal(100, s.RateHz, 10);
    }

    [Fact]
    public void Missed_counts_intervals_above_1_5x_expected()
    {
        var s = TimingStats.Compute(new double[] { 10, 14.9, 15, 15.1, 30 }, expectedMs: 10);
        Assert.Equal(2, s.Missed); // 15.1 et 30 (15 n'est pas > 15)
    }

    [Fact]
    public void Missed_with_per_sample_expected_ignores_unknown()
    {
        var s = TimingStats.Compute(new[] { (20.0, 10.0), (20.0, 0.0), (20.0, 15.0) });
        Assert.Equal(1, s.Missed);
    }

    [Fact]
    public void No_expected_means_no_missed()
    {
        Assert.Equal(0, TimingStats.Compute(new double[] { 1, 100 }).Missed);
    }
}

public class TimingRecorderTests
{
    [Fact]
    public void Records_intervals_between_ticks()
    {
        var r = new TimingRecorder();
        r.OnResync();
        r.OnTick(0, 0);
        r.OnTick(10, 10);
        r.OnTick(21, 10);
        Assert.Equal(new[] { (10.0, 10.0), (11.0, 10.0) }, r.GetSamples());
        Assert.Equal(3, r.TotalTicks);
        var s = r.Snapshot();
        Assert.Equal(10.5, s.Mean, 10);
    }

    [Fact]
    public void Resync_breaks_the_interval_chain()
    {
        var r = new TimingRecorder();
        r.OnTick(0, 0);
        r.OnTick(10, 10);
        r.OnResync();             // pause : le trou ne doit pas compter comme un intervalle
        r.OnTick(5000, 0);
        r.OnTick(5010, 10);
        Assert.Equal(new[] { 10.0, 10.0 }, r.GetSamples().Select(x => x.Actual));
    }

    [Fact]
    public void Ring_buffer_keeps_last_values_only()
    {
        var r = new TimingRecorder(capacity: 3);
        for (int i = 0; i <= 10; i++) r.OnTick(i * 10 + (i == 10 ? 5 : 0), 10);
        var samples = r.GetSamples();
        Assert.Equal(3, samples.Length);
        Assert.Equal(15, samples[^1].Actual);
        Assert.Equal(3, r.Capacity);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var r = new TimingRecorder();
        r.OnTick(0, 0);
        r.OnTick(10, 10);
        r.Reset();
        Assert.Empty(r.GetSamples());
        Assert.Equal(0, r.TotalTicks);
        Assert.True(r.Snapshot().IsEmpty);
    }

    [Fact]
    public void Invalid_capacity_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimingRecorder(0));
    }

    [Fact]
    public void Works_as_engine_observer()
    {
        var clock = new FakeClock();
        var sender = new RecordingSender(clock);
        var rec = new TimingRecorder();
        using var engine = new MacroEngine(sender, () => new FakeSleeper(clock), clock) { Observer = rec };
        using var done = new ManualResetEventSlim();
        engine.StateChanged += (_, e) => { if (e.State == EngineState.Stopped) done.Set(); };
        engine.Start(TestProfiles.Aze(repeat: 10, interval: 10));
        Assert.True(done.Wait(5000));
        var s = rec.Snapshot();
        Assert.Equal(29, s.Count);
        Assert.Equal(10, s.Mean, 10);
        Assert.Equal(0, s.StdDev, 10);
        Assert.Equal(0, s.Missed);
    }
}

public class HybridSleeperTests
{
    [Fact]
    public void Sleeps_coarse_then_spins_to_exact_target()
    {
        var clock = new FakeClock();
        var coarse = new FakeSleeper(clock);
        double? coarseTarget = null;
        coarse.OnSleep = t => coarseTarget = t;
        // Horloge qui avance toute seule pendant le spin (simule le temps qui passe)
        var spinning = new AdvancingClock(clock, 0.05);
        var h = new HybridSleeper(coarse, spinning, spinMs: 1.0);
        Assert.True(h.SleepUntil(10, CancellationToken.None));
        Assert.Equal(9, coarseTarget);
        Assert.True(clock.NowMs >= 10);
        Assert.Contains("Hybrid", h.Name);
    }

    [Fact]
    public void Skips_coarse_sleep_when_already_close()
    {
        var clock = new FakeClock { NowMs = 9.5 };
        var coarse = new FakeSleeper(clock);
        var h = new HybridSleeper(coarse, new AdvancingClock(clock, 0.1), 1.0);
        Assert.True(h.SleepUntil(10, CancellationToken.None));
        Assert.Equal(0, coarse.Calls);
    }

    [Fact]
    public void Cancellation_during_spin_returns_false()
    {
        var clock = new FakeClock();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var h = new HybridSleeper(new FakeSleeper(clock), clock, 1.0); // horloge figée : spin infini sans annulation
        Assert.False(h.SleepUntil(0.5, cts.Token));
    }

    [Fact]
    public void Coarse_cancellation_propagates()
    {
        var clock = new FakeClock();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var h = new HybridSleeper(new FakeSleeper(clock), clock, 1.0);
        Assert.False(h.SleepUntil(100, cts.Token));
    }

    [Fact]
    public void Dispose_disposes_coarse_and_nulls_rejected()
    {
        var clock = new FakeClock();
        var inner = new DisposeTracker();
        new HybridSleeper(inner, clock).Dispose();
        Assert.True(inner.Disposed);
        Assert.Throws<ArgumentNullException>(() => new HybridSleeper(null!, clock));
        Assert.Throws<ArgumentNullException>(() => new HybridSleeper(inner, null!));
    }

    [Fact]
    public void Real_clock_hybrid_is_accurate()
    {
        // Marge de spin (40 ms) > granularité de Thread.Sleep (~16 ms) : même sur une machine chargée,
        // le sommeil grossier se termine avant l'échéance et le spin finit pile à l'heure.
        using var h = new HybridSleeper(new StandardSleeper(StopwatchClock.Instance), StopwatchClock.Instance, 40);
        // 5 essais, médiane : une préemption isolée du runner partagé (3 assemblages de tests en parallèle) ne fait plus
        // échouer le test (vu en CI : 16,8 ms une fois) ; « jamais en avance » reste vérifié sur chaque essai.
        var lates = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            var target = StopwatchClock.Instance.NowMs + 50;
            Assert.True(h.SleepUntil(target, CancellationToken.None));
            lates.Add(StopwatchClock.Instance.NowMs - target);
        }
        Assert.All(lates, late => Assert.True(late >= 0, $"réveil en avance de {-late:0.000} ms"));
        lates.Sort();
        Assert.InRange(lates[2], 0, 15);
    }

    private sealed class AdvancingClock : IClock
    {
        private readonly FakeClock _inner;
        private readonly double _step;
        public AdvancingClock(FakeClock inner, double step) { _inner = inner; _step = step; }
        public double NowMs { get { _inner.Advance(_step); return _inner.NowMs; } }
    }

    private sealed class DisposeTracker : ISleeper
    {
        public bool Disposed;
        public string Name => "t";
        public bool SleepUntil(double targetMs, CancellationToken ct) => true;
        public void Dispose() => Disposed = true;
    }
}
