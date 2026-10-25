using System.Diagnostics;
using System.Globalization;
using System.Text;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Stats;
using Xunit.Abstractions;

namespace Maquereaux.Win32.Tests;

public class TimerTests
{
    [WindowsFact]
    public void HighResolutionSleeper_uses_native_timer_and_reaches_target()
    {
        using var s = new HighResolutionSleeper(StopwatchClock.Instance);
        Assert.True(s.IsHighResolution);
        Assert.Contains("waitable", s.Name);
        // 5 essais : une seule mesure peut tomber sur une préemption du runner partagé (vu en CI : 9,8 ms une fois).
        // Jamais en avance, et retard médian raisonnable même sur une VM chargée.
        var lates = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            var target = StopwatchClock.Instance.NowMs + 5;
            Assert.True(s.SleepUntil(target, CancellationToken.None));
            lates.Add(StopwatchClock.Instance.NowMs - target);
        }
        Assert.All(lates, late => Assert.True(late >= 0, $"réveil en avance de {-late:0.000} ms"));
        lates.Sort();
        Assert.InRange(lates[2], 0, 8);
    }

    [WindowsFact]
    public void HighResolutionSleeper_is_interrupted_by_cancellation()
    {
        using var s = new HighResolutionSleeper(StopwatchClock.Instance);
        using var cts = new CancellationTokenSource(20);
        var sw = Stopwatch.StartNew();
        Assert.False(s.SleepUntil(StopwatchClock.Instance.NowMs + 10_000, cts.Token));
        Assert.True(sw.ElapsedMilliseconds < 2_000);
    }

    [WindowsFact]
    public void Factory_creates_each_strategy()
    {
        foreach (var strategy in Enum.GetValues<TimerStrategy>())
        {
            using var s = SleeperFactory.Create(strategy, StopwatchClock.Instance);
            Assert.True(s.SleepUntil(StopwatchClock.Instance.NowMs + 2, CancellationToken.None));
            Assert.False(string.IsNullOrWhiteSpace(SleeperFactory.Describe(strategy)));
        }
    }

    [WindowsFact]
    public void HighResolution_cadence_is_far_better_than_15ms()
    {
        // Version courte (100 ticks) du benchmark, tolérance large pour la CI
        var stats = TimerBenchmarks.Measure(TimerStrategy.HighResolution, 10, 100).Stats;
        Assert.InRange(stats.Mean, 9.0, 11.0);
    }
}

/// <summary>
/// Benchmarks J2 : 1 000 ticks à 10 ms par stratégie, via le VRAI moteur (avec un envoi factice
/// pour ne pas taper de touches). Exclus de la CI par défaut (Category=Benchmark) ; exécutés dans
/// un job dédié qui publie le tableau de résultats.
/// </summary>
[Trait("Category", "Benchmark")]
public class TimerBenchmarks
{
    private readonly ITestOutputHelper _output;

    public TimerBenchmarks(ITestOutputHelper output) => _output = output;

    public sealed record Result(TimerStrategy Strategy, int IntervalMs, TimingStats Stats, double CpuPercent);

    /// <summary>Lance le moteur sur <paramref name="ticks"/> ticks et mesure les intervalles + le CPU.</summary>
    public static Result Measure(TimerStrategy strategy, int intervalMs, int ticks)
    {
        var recorder = new TimingRecorder(ticks + 10);
        using var engine = new MacroEngine(new NullSender(), () => SleeperFactory.Create(strategy, StopwatchClock.Instance), StopwatchClock.Instance)
        {
            Observer = recorder,
        };
        using var done = new ManualResetEventSlim();
        engine.StateChanged += (_, e) => { if (e.State == EngineState.Stopped) done.Set(); };

        var profile = new Profile
        {
            IntervalMs = intervalMs,
            RepeatCount = ticks + 1, // +1 : N+1 ticks = N intervalles
            Steps = { new MacroStep(KeyStroke.FromVirtualKey(VirtualKeys.F24)) },
        };

        var proc = Process.GetCurrentProcess();
        var cpu0 = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        engine.Start(profile);
        if (!done.Wait(TimeSpan.FromMilliseconds(intervalMs * ticks * 3 + 10_000)))
            throw new TimeoutException("benchmark trop long");
        wall.Stop();
        proc.Refresh();
        var cpu = (proc.TotalProcessorTime - cpu0).TotalMilliseconds / wall.Elapsed.TotalMilliseconds * 100;

        var stats = TimingStats.Compute(recorder.GetSamples());
        return new Result(strategy, intervalMs, stats, cpu);
    }

    [WindowsFact]
    public void Benchmark_all_strategies()
    {
        var results = new List<Result>();
        foreach (var s in Enum.GetValues<TimerStrategy>())
            results.Add(Measure(s, 10, 1000));
        results.Add(Measure(TimerStrategy.HighResolution, 5, 1000));
        results.Add(Measure(TimerStrategy.Hybrid, 5, 1000));
        results.Add(Measure(TimerStrategy.HighResolution, 1, 1000));
        results.Add(Measure(TimerStrategy.Hybrid, 1, 1000));

        var md = ToMarkdown(results);
        _output.WriteLine(md);
        var path = Environment.GetEnvironmentVariable("BENCH_OUTPUT") ?? Path.Combine(AppContext.BaseDirectory, "bench-results.md");
        File.WriteAllText(path, md, Encoding.UTF8);
        Console.WriteLine(md);

        // Critère d'acceptation J2 : à 10 ms, moyenne ∈ [9,8 ; 10,2] et p95 ≤ 11 pour HighRes OU Hybrid
        var ok = results.Where(r => r.IntervalMs == 10 && r.Strategy != TimerStrategy.Standard)
            .Any(r => r.Stats.Mean is >= 9.8 and <= 10.2 && r.Stats.P95 <= 11);
        Assert.True(ok, "Critère J2 non atteint :\n" + md);
    }

    public static string ToMarkdown(IEnumerable<Result> results)
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"Machine : {Environment.MachineName} · {Environment.ProcessorCount} cœurs logiques · {Environment.OSVersion.VersionString}");
        sb.AppendLine();
        sb.AppendLine("| Stratégie | Intervalle | Moyenne | Min | Max | p95 | p99 | Jitter (σ) | Manqués | CPU (1 cœur) |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var r in results)
        {
            var s = r.Stats;
            sb.AppendLine(string.Format(c, "| {0} | {1} ms | {2:0.000} | {3:0.000} | {4:0.000} | {5:0.000} | {6:0.000} | {7:0.000} | {8} | {9:0.0} % |",
                r.Strategy, r.IntervalMs, s.Mean, s.Min, s.Max, s.P95, s.P99, s.StdDev, s.Missed, r.CpuPercent));
        }
        sb.AppendLine();
        sb.AppendLine($"_{results.First().Stats.Count} intervalles mesurés par ligne, valeurs en ms._");
        return sb.ToString();
    }

    private sealed class NullSender : IInputSender
    {
        public void Prepare(IEnumerable<KeyStroke> keys) { }
        public void Send(IReadOnlyList<KeyAction> actions) { }
    }
}
