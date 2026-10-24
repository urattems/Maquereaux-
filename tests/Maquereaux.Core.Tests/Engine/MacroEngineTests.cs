using System.Collections.Concurrent;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Engine;

public class MacroEngineTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeSleeper _sleeper;
    private readonly RecordingSender _sender;
    private readonly MacroEngine _engine;
    private readonly ConcurrentQueue<EngineStateChangedEventArgs> _events = new();
    private readonly ManualResetEventSlim _stopped = new();

    public MacroEngineTests()
    {
        _sleeper = new FakeSleeper(_clock);
        _sender = new RecordingSender(_clock);
        _engine = new MacroEngine(_sender, _sleeper, _clock);
        _engine.StateChanged += (_, e) =>
        {
            _events.Enqueue(e);
            if (e.State == EngineState.Stopped) _stopped.Set();
        };
    }

    private EngineStateChangedEventArgs RunToEnd(Profile p, ISendGate? gate = null)
    {
        _stopped.Reset();
        _engine.Start(p, gate);
        Assert.True(_stopped.Wait(TimeSpan.FromSeconds(10)), "le moteur ne s'est pas arrêté");
        return _events.Last(e => e.State == EngineState.Stopped);
    }

    /// <summary>Arrête le moteur (depuis son propre thread) après N lots envoyés.</summary>
    private void StopAfterBatches(int n) =>
        _sender.AfterSend = s => { if (s.BatchSizes.Count >= n) _engine.Stop(); };

    [Fact]
    public void Cycle_repeat_2_sends_AZEAZE_then_completes()
    {
        var end = RunToEnd(TestProfiles.Aze(SequenceMode.Cycle, repeat: 2));
        Assert.Equal(StopReason.Completed, end.Reason);
        Assert.Equal(new ushort[] { VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E, VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E }, _sender.DownKeys);
        Assert.True(_sender.AllKeysReleased);
        Assert.Equal(6, _engine.TicksSent);
        Assert.False(_engine.IsRunning);
        Assert.Null(_engine.RunningProfile);
    }

    [Fact]
    public void Cycle_infinite_seven_ticks_gives_AZEAZEA()
    {
        StopAfterBatches(7);
        var end = RunToEnd(TestProfiles.Aze(SequenceMode.Cycle));
        Assert.Equal(StopReason.Manual, end.Reason);
        Assert.Equal(new ushort[] { VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E, VirtualKeys.A, VirtualKeys.Z, VirtualKeys.E, VirtualKeys.A }, _sender.DownKeys);
    }

    [Fact]
    public void Burst_sends_whole_sequence_in_one_SendInput_per_tick()
    {
        RunToEnd(TestProfiles.Aze(SequenceMode.Burst, repeat: 3));
        Assert.Equal(new[] { 6, 6, 6 }, _sender.BatchSizes);
        Assert.Equal(new[] { "A↓", "A↑", "Z↓", "Z↑", "E↓", "E↑" }, _sender.Actions.Take(6).Select(a => a.ToString()));
        Assert.True(_sender.AllKeysReleased);
    }

    [Fact]
    public void Burst_repeat_2_stops_after_2_cycles()
    {
        var end = RunToEnd(TestProfiles.Aze(SequenceMode.Burst, repeat: 2));
        Assert.Equal(StopReason.Completed, end.Reason);
        Assert.Equal(6, _sender.DownKeys.Count);
    }

    [Fact]
    public void Cadence_is_absolute_lateness_does_not_accumulate()
    {
        _clock.NowMs = 1000;
        _sleeper.Lateness = () => 3; // chaque réveil a 3 ms de retard
        RunToEnd(TestProfiles.Aze(repeat: 4, interval: 10));
        var downTimes = _sender.Sent.Where(s => s.Action.IsDown).Select(s => s.TimeMs).ToList();
        // Si la dérive se cumulait on aurait 1003, 1016, 1029… ; en cadence absolue : +10 à chaque fois.
        Assert.Equal(new double[] { 1003, 1013, 1023, 1033, 1043, 1053, 1063, 1073, 1083, 1093, 1103, 1113 }, downTimes);
    }

    [Fact]
    public void Big_stall_reanchors_instead_of_bursting_to_catch_up()
    {
        int sleeps = 0;
        _sleeper.Lateness = () => ++sleeps == 3 ? 100 : 0; // le 3e réveil a 100 ms de retard
        RunToEnd(TestProfiles.Aze(repeat: 3, interval: 10));
        var t = _sender.Sent.Where(s => s.Action.IsDown).Select(s => s.TimeMs).ToList();
        var gaps = t.Zip(t.Skip(1), (a, b) => b - a).ToList();
        // Après le gros retard, plus aucun intervalle < 10 ms (pas de rafale de rattrapage)
        Assert.All(gaps, g => Assert.True(g >= 10, $"intervalle {g} ms < 10 ms"));
        Assert.Contains(gaps, g => g >= 100);
    }

    [Fact]
    public void Per_step_delay_is_respected_in_cycle_mode()
    {
        var p = TestProfiles.Aze(repeat: 1, interval: 10);
        p.Steps[0].DelayAfterMs = 50;
        RunToEnd(p);
        var t = _sender.Sent.Where(s => s.Action.IsDown).Select(s => s.TimeMs).ToList();
        Assert.Equal(new double[] { 0, 50, 60 }, t);
    }

    [Fact]
    public void Hold_keeps_key_down_for_HoldMs()
    {
        RunToEnd(TestProfiles.Aze(repeat: 1, interval: 20, hold: 5));
        var a = _sender.Sent.Where(s => s.Action.Key.VirtualKey == VirtualKeys.A).ToList();
        Assert.Equal(2, a.Count);
        Assert.True(a[0].Action.IsDown);
        Assert.Equal(5, a[1].TimeMs - a[0].TimeMs, 6);
        Assert.True(_sender.AllKeysReleased);
    }

    [Fact]
    public void Stop_during_hold_releases_the_pressed_key()
    {
        // Pendant l'attente de maintien (une touche est enfoncée), on demande l'arrêt.
        _sleeper.OnSleep = _ =>
        {
            if (_sender.DownKeys.Count > 0 && !_sender.AllKeysReleased) _engine.Stop();
        };
        var end = RunToEnd(TestProfiles.Aze(hold: 50));
        Assert.Equal(StopReason.Manual, end.Reason);
        Assert.True(_sender.AllKeysReleased, "une touche est restée bloquée !");
        Assert.False(_sender.Actions.Last().IsDown);
    }

    [Fact]
    public void Sender_exception_stops_with_error_and_releases_keys()
    {
        _sender.ThrowOnBatch = 2; // 1 = down A, 2 = up A → plantage avec A enfoncée
        var end = RunToEnd(TestProfiles.Aze(hold: 5));
        Assert.Equal(StopReason.Error, end.Reason);
        Assert.IsType<InvalidOperationException>(end.Error);
        Assert.True(_sender.AllKeysReleased);
    }

    [Fact]
    public void Gate_closed_pauses_then_resumes()
    {
        int checks = 0;
        var gate = new FakeGate { Predicate = () => ++checks > 3 }; // fermé pour les 3 premiers sondages
        RunToEnd(TestProfiles.Aze(repeat: 1), gate);
        var states = _events.Select(e => e.State).ToList();
        Assert.Equal(new[] { EngineState.Running, EngineState.Paused, EngineState.Running, EngineState.Stopped }, states);
        Assert.Equal(3, _sender.DownKeys.Count);
    }

    [Fact]
    public void Gate_closing_mid_run_sends_nothing_while_paused()
    {
        bool open = true;
        var gate = new FakeGate { Predicate = () => open };
        _sender.AfterSend = s =>
        {
            if (s.BatchSizes.Count == 2) { open = false; ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(50); open = true; }); }
        };
        RunToEnd(TestProfiles.Aze(repeat: 2), gate);
        Assert.Equal(6, _sender.DownKeys.Count);
        Assert.Contains(_events, e => e.State == EngineState.Paused);
    }

    [Fact]
    public void Safety_timeout_stops_the_engine()
    {
        _engine.MaxRunDuration = TimeSpan.FromMilliseconds(50);
        var end = RunToEnd(TestProfiles.Aze(interval: 10));
        Assert.Equal(StopReason.SafetyTimeout, end.Reason);
        Assert.InRange(_engine.TicksSent, 4, 6);
    }

    [Fact]
    public void Start_while_running_replaces_previous_profile()
    {
        _sender.AfterSend = _ => Thread.Sleep(1); // évite une boucle 100 % CPU
        var first = TestProfiles.Aze();
        first.Name = "Premier";
        _engine.Start(first);
        var second = TestProfiles.Aze(repeat: 1);
        second.Name = "Second";
        _stopped.Reset();
        _engine.Start(second);
        SpinWait.SpinUntil(() => !_engine.IsRunning, TimeSpan.FromSeconds(5));
        var stops = _events.Where(e => e.State == EngineState.Stopped).ToList();
        Assert.Equal(StopReason.Replaced, stops[0].Reason);
        Assert.Equal("Premier", stops[0].Profile!.Name);
        Assert.Equal(StopReason.Completed, stops[1].Reason);
        Assert.True(_sender.AllKeysReleased);
    }

    [Fact]
    public void Engine_runs_on_a_snapshot_of_the_profile()
    {
        var p = TestProfiles.Aze(repeat: 1);
        _sender.AfterSend = _ => p.Steps.Clear(); // modification pendant l'exécution
        RunToEnd(p);
        Assert.Equal(3, _sender.DownKeys.Count);
    }

    [Fact]
    public void Observer_receives_each_tick_with_expected_interval()
    {
        var obs = new RecordingObserver();
        _engine.Observer = obs;
        RunToEnd(TestProfiles.Aze(repeat: 1, interval: 10));
        Assert.Equal(1, obs.Resyncs);
        Assert.Equal(new double[] { 0, 10, 10 }, obs.Ticks.Select(t => t.Expected));
        Assert.Equal(new double[] { 0, 10, 20 }, obs.Ticks.Select(t => t.Now));
    }

    [Fact]
    public void Prepare_receives_distinct_keys()
    {
        RunToEnd(TestProfiles.Aze(repeat: 1));
        Assert.Equal(3, _sender.Prepared.Count);
    }

    [Fact]
    public void Start_with_empty_sequence_throws_and_stays_stopped()
    {
        Assert.Throws<ArgumentException>(() => _engine.Start(new Profile()));
        Assert.False(_engine.IsRunning);
        Assert.Empty(_events);
    }

    [Fact]
    public void Stop_when_not_running_is_a_noop()
    {
        _engine.Stop();
        _engine.ReleaseAllKeys();
        Assert.Empty(_sender.Sent);
        Assert.Empty(_events);
    }

    [Fact]
    public void Dispose_stops_with_shutdown_reason()
    {
        _sender.AfterSend = _ => Thread.Sleep(1);
        _stopped.Reset();
        _engine.Start(TestProfiles.Aze());
        _engine.Dispose();
        Assert.True(_stopped.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(StopReason.Shutdown, _events.Last().Reason);
    }

    [Fact]
    public void Throwing_subscriber_does_not_break_engine()
    {
        _engine.StateChanged += (_, _) => throw new Exception("abonné cassé");
        var end = RunToEnd(TestProfiles.Aze(repeat: 1));
        Assert.Equal(StopReason.Completed, end.Reason);
    }

    [Fact]
    public void Sleeper_can_be_swapped_and_null_is_rejected()
    {
        var other = new FakeSleeper(_clock);
        _engine.Sleeper = other;
        Assert.Same(other, _engine.Sleeper);
        Assert.Throws<ArgumentNullException>(() => _engine.Sleeper = null!);
    }

    [Fact]
    public void Constructor_rejects_nulls()
    {
        Assert.Throws<ArgumentNullException>(() => new MacroEngine(null!, _sleeper, _clock));
        Assert.Throws<ArgumentNullException>(() => new MacroEngine(_sender, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new MacroEngine(_sender, _sleeper, null!));
    }

    private sealed class RecordingObserver : ITickObserver
    {
        public int Resyncs;
        public List<(double Now, double Expected)> Ticks { get; } = new();
        public void OnResync() => Resyncs++;
        public void OnTick(double nowMs, double expectedIntervalMs) => Ticks.Add((nowMs, expectedIntervalMs));
    }
}
