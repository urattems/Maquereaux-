using System.Collections.Concurrent;
using Maquereaux.Core.Activation;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Activation;

public class HoldHotkeyTrackerTests
{
    private readonly HoldHotkeyTracker _t = new();
    private readonly Guid _p1 = Guid.NewGuid();
    private readonly Guid _p2 = Guid.NewGuid();

    public HoldHotkeyTrackerTests()
    {
        _t.SetBindings(new[]
        {
            (_p1, new Hotkey(HotkeyModifiers.None, VirtualKeys.F7)),
            (_p2, new Hotkey(HotkeyModifiers.Control, VirtualKeys.F7)),
        });
    }

    private static RawKeyEvent Ev(ushort vk, bool down, bool injected = false) => new(0, vk, 0, down, injected);

    [Fact]
    public void Down_starts_up_stops_and_both_are_swallowed()
    {
        var d = _t.Process(Ev(VirtualKeys.F7, true));
        Assert.Equal(new HoldDecision(true, _p1, null), d);
        var u = _t.Process(Ev(VirtualKeys.F7, false));
        Assert.Equal(new HoldDecision(true, null, _p1), u);
    }

    [Fact]
    public void Mouse_button_starts_and_stops_but_is_never_swallowed()
    {
        var p3 = Guid.NewGuid();
        _t.SetBindings(new[] { (p3, new Hotkey(HotkeyModifiers.None, VirtualKeys.RButton)) });
        Assert.True(_t.HasMouseBindings);
        // Le clic doit toujours arriver au jeu : Swallow = false à l'appui comme au relâchement
        Assert.Equal(new HoldDecision(false, p3, null), _t.Process(Ev(VirtualKeys.RButton, true)));
        Assert.Equal(new HoldDecision(false, null, p3), _t.Process(Ev(VirtualKeys.RButton, false)));
        // Clic injecté (par un autre logiciel) : ignoré
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.RButton, true, injected: true)));
        // Clic gauche non lié : rien
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.LButton, true)));
    }

    [Fact]
    public void Keyboard_only_bindings_have_no_mouse_bindings() => Assert.False(_t.HasMouseBindings);

    [Fact]
    public void Autorepeat_is_swallowed_without_new_command()
    {
        _t.Process(Ev(VirtualKeys.F7, true));
        var repeat = _t.Process(Ev(VirtualKeys.F7, true));
        Assert.True(repeat.Swallow);
        Assert.Null(repeat.StartProfile);
    }

    [Fact]
    public void Injected_events_are_ignored()
    {
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.F7, true, injected: true)));
        // Un injecté ne doit pas non plus arrêter un maintien physique en cours
        _t.Process(Ev(VirtualKeys.F7, true));
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.F7, false, injected: true)));
    }

    [Fact]
    public void Modifiers_must_match_exactly()
    {
        _t.Process(Ev(VirtualKeys.LControl, true));
        var d = _t.Process(Ev(VirtualKeys.F7, true));
        Assert.Equal(_p2, d.StartProfile); // Ctrl+F7 → profil 2, pas le profil 1
        Assert.Equal(HotkeyModifiers.Control, _t.CurrentModifiers);
        _t.Process(Ev(VirtualKeys.LControl, false)); // Ctrl relâché avant F7
        Assert.Equal(_p2, _t.Process(Ev(VirtualKeys.F7, false)).StopProfile);
    }

    [Fact]
    public void Unbound_key_passes_through()
    {
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.A, true)));
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.A, false)));
    }

    [Fact]
    public void Modifier_events_are_never_swallowed()
    {
        Assert.False(_t.Process(Ev(VirtualKeys.RShift, true)).Swallow);
        Assert.Equal(HotkeyModifiers.Shift, _t.CurrentModifiers);
        _t.Process(Ev(VirtualKeys.RMenu, true));
        _t.Process(Ev(VirtualKeys.LWin, true));
        Assert.Equal(HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Win, _t.CurrentModifiers);
    }

    [Fact]
    public void Up_without_tracked_down_passes_through()
    {
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.F7, false)));
    }

    [Fact]
    public void Reset_and_rebinding()
    {
        _t.Process(Ev(VirtualKeys.F7, true));
        _t.Reset();
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.F7, false)));
        _t.SetBindings(Array.Empty<(Guid, Hotkey)>());
        Assert.False(_t.HasBindings);
        Assert.Equal(HoldDecision.None, _t.Process(Ev(VirtualKeys.F7, true)));
    }

    [Fact]
    public void Invalid_bindings_are_ignored()
    {
        _t.SetBindings(new[] { (_p1, new Hotkey(HotkeyModifiers.None, VirtualKeys.LShift)), (_p2, (Hotkey)null!) });
        Assert.False(_t.HasBindings);
    }

    /// <summary>Scénario complet : hook → tracker → contrôleur → moteur (faux).</summary>
    [Fact]
    public void Hold_logic_end_to_end_with_controller()
    {
        var runner = new FakeRunner();
        var profile = TestProfiles.Aze();
        profile.Id = _p1;
        var ctrl = new ActivationController(runner, id => id == _p1 ? profile : null);

        void Feed(RawKeyEvent e)
        {
            var d = _t.Process(e);
            if (d.StartProfile is { } s) ctrl.HoldDown(s);
            if (d.StopProfile is { } p) ctrl.HoldUp(p);
        }

        Feed(Ev(VirtualKeys.F7, true));
        Assert.True(runner.IsRunning);
        Feed(Ev(VirtualKeys.F7, true));
        Feed(Ev(VirtualKeys.F7, true));
        Feed(Ev(VirtualKeys.F7, false));
        Assert.False(runner.IsRunning);
        Assert.Equal(new[] { "start:AZE", "stop:AZE:HoldReleased" }, runner.Log);
    }
}

public class ProcessGateTests
{
    private sealed class FakeForeground : IForegroundProcessProvider
    {
        public volatile string? Current;
        public string? GetForegroundProcessName() => Current;
    }

    [Fact]
    public void Open_only_when_target_has_focus()
    {
        var fg = new FakeForeground { Current = "notepad.exe" };
        var gate = new ProcessGate(fg, "Notepad");
        Assert.Equal("notepad.exe", gate.Target);
        Assert.True(gate.CanSend());
        fg.Current = "discord.exe";
        Assert.False(gate.CanSend());
        fg.Current = null;
        Assert.False(gate.CanSend());
        fg.Current = "NOTEPAD.EXE";
        Assert.True(gate.CanSend());
    }

    [Fact]
    public void Invalid_arguments_throw()
    {
        Assert.Throws<ArgumentException>(() => new ProcessGate(new FakeForeground(), " "));
        Assert.Throws<ArgumentNullException>(() => new ProcessGate(null!, "x"));
    }

    /// <summary>Filtre process : focus perdu → pause, focus revenu → reprise (moteur réel + horloge simulée).</summary>
    [Fact]
    public void Focus_lost_pauses_engine_and_focus_back_resumes()
    {
        var clock = new FakeClock();
        var sender = new RecordingSender(clock);
        var fg = new FakeForeground { Current = "notepad.exe" };
        using var engine = new MacroEngine(sender, () => new FakeSleeper(clock), clock);
        var states = new ConcurrentQueue<EngineState>();
        using var done = new ManualResetEventSlim();
        engine.StateChanged += (_, e) => { states.Enqueue(e.State); if (e.State == EngineState.Stopped) done.Set(); };

        int sentWhileUnfocused = 0;
        sender.AfterSend = s =>
        {
            if (fg.Current != "notepad.exe") sentWhileUnfocused++;
            if (s.BatchSizes.Count == 3)
            {
                fg.Current = "chrome.exe"; // l'utilisateur passe sur une autre fenêtre
                ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(60); fg.Current = "notepad.exe"; });
            }
        };

        engine.Start(TestProfiles.Aze(repeat: 3), new ProcessGate(fg, "notepad.exe"));
        Assert.True(done.Wait(5000));
        Assert.Equal(0, sentWhileUnfocused);
        Assert.Equal(9, sender.DownKeys.Count);
        Assert.Equal(new[] { EngineState.Running, EngineState.Paused, EngineState.Running, EngineState.Stopped }, states);
    }
}
