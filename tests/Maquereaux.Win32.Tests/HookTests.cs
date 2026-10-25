using System.Collections.Concurrent;
using System.Diagnostics;
using Maquereaux.Core.Activation;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Win32.Tests;

/// <summary>
/// Tests d'intégration du hook WH_KEYBOARD_LL : on envoie de vraies touches (F13–F24, sans effet) via
/// SendInput et on vérifie ce que le hook capture.
/// </summary>
[Collection("Clavier global")] // jamais en parallèle : ils partagent le clavier du système
public class HookTests
{
    private static void SendKey(SendInputSender sender, ushort vk)
    {
        var k = KeyStroke.FromVirtualKey(vk);
        sender.Prepare(new[] { k });
        sender.Send(new[] { KeyAction.Down(k), KeyAction.Up(k) });
    }

    [WindowsFact]
    public void Hook_captures_injected_keys_with_flags_and_signature()
    {
        using var loop = new MessageLoopThread();
        using var hook = new KeyboardHook(loop);
        var events = new ConcurrentQueue<RawKeyEvent>();
        hook.KeyEvent += e => { if (e.VirtualKey == VirtualKeys.F13 + 9) events.Enqueue(e); }; // F22
        Assert.True(hook.Install());
        Assert.True(hook.IsInstalled);

        var sender = new SendInputSender();
        for (int i = 0; i < 5; i++) SendKey(sender, (ushort)(VirtualKeys.F13 + 9));
        SpinWait.SpinUntil(() => events.Count >= 10, TimeSpan.FromSeconds(3));

        Assert.Equal(10, events.Count); // 5 down + 5 up
        Assert.All(events, e => Assert.True(e.IsInjected));
        Assert.All(events, e => Assert.True(e.IsFromMaquereaux));
        Assert.Equal(5, events.Count(e => e.IsDown));
        var ts = events.Select(e => e.TimestampMs).ToList();
        Assert.Equal(ts.OrderBy(t => t), ts); // horodatages croissants
        hook.Uninstall();
        Assert.False(hook.IsInstalled);
    }

    [WindowsFact]
    public void Filter_can_swallow_keys()
    {
        using var loop = new MessageLoopThread();
        using var hook = new KeyboardHook(loop);
        using var hotkeys = new GlobalHotkeyManager(loop);
        int hotkeyHits = 0;
        hotkeys.Pressed += _ => Interlocked.Increment(ref hotkeyHits);
        // F21 enregistré en hotkey : s'il est avalé par le hook, le hotkey ne doit PAS se déclencher
        Assert.True(hotkeys.Register(7, new Hotkey(HotkeyModifiers.None, (ushort)(VirtualKeys.F13 + 8))).Success);
        hook.Filter = e => e.VirtualKey == VirtualKeys.F13 + 8;
        Assert.True(hook.Install());

        SendKey(new SendInputSender(), (ushort)(VirtualKeys.F13 + 8));
        Thread.Sleep(300);
        Assert.Equal(0, hotkeyHits);

        hook.Filter = null;
        SendKey(new SendInputSender(), (ushort)(VirtualKeys.F13 + 8));
        SpinWait.SpinUntil(() => hotkeyHits > 0, TimeSpan.FromSeconds(2));
        Assert.Equal(1, hotkeyHits);
    }

    [WindowsFact]
    public void Hold_tracker_ignores_injected_hotkey_through_real_hook()
    {
        // Le tracker Hold branché sur le vrai hook ne doit PAS réagir à une touche injectée (anti auto-déclenchement)
        using var loop = new MessageLoopThread();
        using var hook = new KeyboardHook(loop);
        var tracker = new HoldHotkeyTracker();
        tracker.SetBindings(new[] { (Guid.NewGuid(), new Hotkey(HotkeyModifiers.None, (ushort)(VirtualKeys.F13 + 7))) });
        int decisions = 0;
        hook.Filter = e =>
        {
            var d = tracker.Process(e);
            if (d.StartProfile is not null || d.StopProfile is not null) Interlocked.Increment(ref decisions);
            return d.Swallow;
        };
        Assert.True(hook.Install());
        var seen = 0;
        hook.KeyEvent += e => { if (e.VirtualKey == VirtualKeys.F13 + 7) Interlocked.Increment(ref seen); };
        SendKey(new SendInputSender(), (ushort)(VirtualKeys.F13 + 7));
        SpinWait.SpinUntil(() => seen >= 2, TimeSpan.FromSeconds(2));
        Assert.Equal(2, seen);
        Assert.Equal(0, decisions);
    }

    /// <summary>
    /// Mesure bout-en-bout « comme le Labo » : le moteur envoie F23 à 10 ms (timer haute résolution),
    /// le hook horodate chaque appui ; les intervalles capturés doivent être cohérents avec 10 ms.
    /// </summary>
    [WindowsFact]
    public void Engine_at_10ms_is_seen_by_hook_at_about_10ms()
    {
        using var loop = new MessageLoopThread();
        using var hook = new KeyboardHook(loop);
        var downs = new ConcurrentQueue<double>();
        hook.KeyEvent += e => { if (e.VirtualKey == VirtualKeys.F13 + 10 && e.IsDown) downs.Enqueue(e.TimestampMs); };
        Assert.True(hook.Install());

        using var engine = new MacroEngine(new SendInputSender(), () => new HighResolutionSleeper(StopwatchClock.Instance), StopwatchClock.Instance);
        using var done = new ManualResetEventSlim();
        engine.StateChanged += (_, e) => { if (e.State == EngineState.Stopped) done.Set(); };
        engine.Start(new Profile
        {
            IntervalMs = 10,
            RepeatCount = 101,
            Steps = { new MacroStep(KeyStroke.FromVirtualKey((ushort)(VirtualKeys.F13 + 10))) },
        });
        Assert.True(done.Wait(TimeSpan.FromSeconds(10)));
        SpinWait.SpinUntil(() => downs.Count >= 101, TimeSpan.FromSeconds(3));

        var t = downs.ToArray();
        Assert.Equal(101, t.Length);
        var intervals = t.Zip(t.Skip(1), (a, b) => b - a).ToArray();
        var stats = Core.Stats.TimingStats.Compute(intervals);
        Console.WriteLine($"Hook : moyenne {stats.Mean:0.000} ms, p95 {stats.P95:0.000}, σ {stats.StdDev:0.000}");
        Assert.InRange(stats.Mean, 9.5, 10.5);
    }

    [WindowsFact]
    public void Foreground_and_process_helpers_work()
    {
        var names = ProcessInfo.GetWindowedProcessNames();
        Assert.All(names, n => Assert.EndsWith(".exe", n));
        Assert.Equal(Process.GetCurrentProcess().ProcessName.ToLowerInvariant() + ".exe", ProcessInfo.GetProcessName((uint)Environment.ProcessId));
        Assert.False(ProcessInfo.IsProcessElevated((uint)Environment.ProcessId) && !ProcessInfo.IsCurrentProcessElevated);
        _ = new ForegroundProcessProvider().GetForegroundProcessName(); // ne doit pas lever
        _ = ProcessInfo.DetectUipiBlockedForeground();
        Assert.Null(ProcessInfo.GetProcessName(0));
    }
}

/// <summary>Hook souris (raccourcis « Maintenir » sur un bouton) : traduction des messages + installation.</summary>
public class MouseHookTests
{
    [Theory]
    [InlineData(0x0201u, 0u, VirtualKeys.LButton, true)]
    [InlineData(0x0202u, 0u, VirtualKeys.LButton, false)]
    [InlineData(0x0204u, 0u, VirtualKeys.RButton, true)]
    [InlineData(0x0205u, 0u, VirtualKeys.RButton, false)]
    [InlineData(0x0207u, 0u, VirtualKeys.MButton, true)]
    [InlineData(0x020Bu, 0x00010000u, VirtualKeys.XButton1, true)]
    [InlineData(0x020Cu, 0x00020000u, VirtualKeys.XButton2, false)]
    public void Buttons_are_translated_to_virtual_keys(uint msg, uint mouseData, ushort vk, bool down)
    {
        Assert.Equal((vk, down), MouseHook.Translate(msg, mouseData));
    }

    [Theory]
    [InlineData(0x0200u)] // WM_MOUSEMOVE
    [InlineData(0x020Au)] // WM_MOUSEWHEEL
    public void Moves_and_wheel_are_ignored(uint msg) => Assert.Equal((ushort)0, MouseHook.Translate(msg, 0).Vk);

    [WindowsFact]
    public void Install_and_uninstall()
    {
        using var loop = new MessageLoopThread();
        using var hook = new MouseHook(loop) { Button = _ => { } };
        Assert.True(hook.Install());
        Assert.True(hook.IsInstalled);
        hook.Uninstall();
        Assert.False(hook.IsInstalled);
    }
}
