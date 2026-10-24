using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32.Tests;

public class SendInputTests
{
    [Fact]
    public void INPUT_struct_has_native_size()
    {
        // 40 octets en x64, 28 en x86 : sinon SendInput refuse tout (cbSize invalide)
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<INPUT>());
    }

    [Fact]
    public void Build_uses_scancode_mode_when_scan_known()
    {
        var down = SendInputSender.Build(0x41, 0x10, false, true);
        Assert.Equal(NativeMethods.INPUT_KEYBOARD, down.type);
        Assert.Equal(0, down.U.ki.wVk);
        Assert.Equal(0x10, down.U.ki.wScan);
        Assert.Equal(NativeMethods.KEYEVENTF_SCANCODE, down.U.ki.dwFlags);
        Assert.Equal(SendInputSender.ExtraInfoSignature, (uint)down.U.ki.dwExtraInfo);

        var up = SendInputSender.Build(0x25, 0x4B, true, false);
        Assert.Equal(NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP | NativeMethods.KEYEVENTF_EXTENDEDKEY, up.U.ki.dwFlags);
    }

    [Fact]
    public void Build_falls_back_to_vk_mode_without_scan()
    {
        var down = SendInputSender.Build(VirtualKeys.Pause, 0, false, true);
        Assert.Equal(VirtualKeys.Pause, down.U.ki.wVk);
        Assert.Equal(0u, down.U.ki.dwFlags & NativeMethods.KEYEVENTF_SCANCODE);
    }

    [WindowsFact]
    public void Layout_resolves_letters_and_extended_keys()
    {
        var hkl = KeyboardLayout.GetForegroundLayout();
        var (scanA, extA) = KeyboardLayout.Resolve(VirtualKeys.A, hkl);
        Assert.NotEqual(0, scanA);
        Assert.False(extA);

        var (scanLeft, extLeft) = KeyboardLayout.Resolve(VirtualKeys.Left, hkl);
        Assert.Equal(0x4B, scanLeft);
        Assert.True(extLeft);

        Assert.Equal((ushort)0, KeyboardLayout.Resolve(VirtualKeys.Pause, hkl).Scan);
    }

    [WindowsFact]
    public void Describe_gives_name_and_scan()
    {
        var f5 = KeyboardLayout.Describe(0x74);
        Assert.Equal("F5", f5.Name);
        Assert.Equal(0x3F, f5.ScanCode);
        var a = KeyboardLayout.Describe(VirtualKeys.A);
        Assert.False(string.IsNullOrWhiteSpace(a.Name));
    }

    [WindowsFact]
    public void Hotkey_register_conflict_is_detected()
    {
        using var loop = new MessageLoopThread();
        using var mgr = new GlobalHotkeyManager(loop);
        var hk = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, 0x86); // Ctrl+Shift+Alt+F23
        Assert.True(mgr.Register(1, hk).Success);
        // Deuxième enregistrement du même raccourci → conflit
        var second = mgr.Register(2, hk);
        Assert.Equal(HotkeyRegistrationStatus.AlreadyInUse, second.Status);
        Assert.Contains("déjà utilisé", second.Describe(hk));
        Assert.Equal(HotkeyRegistrationStatus.Invalid, mgr.Register(3, new Hotkey(HotkeyModifiers.None, VirtualKeys.LControl)).Status);
        mgr.UnregisterAll();
        Assert.Empty(mgr.RegisteredIds);
    }

    /// <summary>
    /// Test bout-en-bout : le moteur envoie F24 via SendInput (scan codes) et un RegisterHotKey(F24)
    /// sur notre boucle de messages le reçoit. Prouve que la chaîne d'envoi réelle fonctionne.
    /// </summary>
    [WindowsFact]
    public void Engine_sends_real_keys_received_by_global_hotkey()
    {
        using var loop = new MessageLoopThread();
        using var mgr = new GlobalHotkeyManager(loop);
        var received = new ConcurrentQueue<int>();
        mgr.Pressed += received.Enqueue;
        Assert.True(mgr.Register(42, new Hotkey(HotkeyModifiers.None, VirtualKeys.F24)).Success, "F24 déjà pris sur cette machine ?");

        var sender = new SendInputSender();
        using var engine = new MacroEngine(sender, () => new StandardSleeper(StopwatchClock.Instance), StopwatchClock.Instance);
        using var done = new ManualResetEventSlim();
        engine.StateChanged += (_, e) => { if (e.State == EngineState.Stopped) done.Set(); };

        var profile = new Profile
        {
            IntervalMs = 30,
            RepeatCount = 5,
            Steps = { new MacroStep(KeyStroke.FromVirtualKey(VirtualKeys.F24)) },
        };
        engine.Start(profile);
        Assert.True(done.Wait(TimeSpan.FromSeconds(10)));
        SpinWait.SpinUntil(() => received.Count >= 5, TimeSpan.FromSeconds(3));

        Assert.Equal(0, sender.FailedEvents);
        Assert.Equal(5, received.Count);
        Assert.All(received, id => Assert.Equal(42, id));
    }

    [WindowsFact]
    public void MessageLoop_invoke_runs_on_loop_thread()
    {
        using var loop = new MessageLoopThread();
        var onLoop = loop.Invoke(() => loop.IsOnLoopThread);
        Assert.True(onLoop);
        Assert.False(loop.IsOnLoopThread);
        Assert.Throws<InvalidOperationException>(() => loop.Invoke(() => throw new InvalidOperationException()));
    }
}
