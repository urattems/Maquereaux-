using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Models;

public class ModelTests
{
    [Theory]
    [InlineData(HotkeyModifiers.None, VirtualKeys.F6, "F6")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, VirtualKeys.Pause, "Ctrl+Alt+Pause")]
    [InlineData(HotkeyModifiers.Shift | HotkeyModifiers.Win, VirtualKeys.A, "Shift+Win+A")]
    public void Hotkey_ToString(HotkeyModifiers mods, ushort vk, string expected)
    {
        Assert.Equal(expected, new Hotkey(mods, vk).ToString());
    }

    [Fact]
    public void Hotkey_defaults()
    {
        Assert.Equal("F6", Hotkey.DefaultToggle.ToString());
        Assert.Equal("Ctrl+Alt+Pause", Hotkey.DefaultEmergencyStop.ToString());
    }

    [Theory]
    [InlineData(VirtualKeys.F6, true)]
    [InlineData(VirtualKeys.LControl, false)]
    [InlineData(VirtualKeys.Shift, false)]
    [InlineData((ushort)0, false)]
    public void Hotkey_validity(ushort vk, bool valid)
    {
        Assert.Equal(valid, new Hotkey(HotkeyModifiers.None, vk).IsValid);
    }

    [Fact]
    public void Hotkey_value_equality()
    {
        Assert.Equal(new Hotkey(HotkeyModifiers.Alt, 0x41), new Hotkey(HotkeyModifiers.Alt, 0x41));
    }

    [Theory]
    [InlineData(VirtualKeys.A, "A")]
    [InlineData((ushort)0x39, "9")]
    [InlineData(VirtualKeys.F1, "F1")]
    [InlineData(VirtualKeys.F24, "F24")]
    [InlineData((ushort)(VirtualKeys.NumPad0 + 5), "Pavé 5")]
    [InlineData(VirtualKeys.Space, "Espace")]
    [InlineData((ushort)0xFE, "VK 0xFE")]
    public void KeyNames_GetName(ushort vk, string name)
    {
        Assert.Equal(name, KeyNames.GetName(vk));
    }

    [Theory]
    [InlineData(VirtualKeys.Left, true)]
    [InlineData(VirtualKeys.Insert, true)]
    [InlineData(VirtualKeys.RControl, true)]
    [InlineData(VirtualKeys.Divide, true)]
    [InlineData(VirtualKeys.A, false)]
    [InlineData(VirtualKeys.LControl, false)]
    [InlineData((ushort)(VirtualKeys.NumPad0 + 4), false)]
    public void KeyNames_IsExtended(ushort vk, bool ext)
    {
        Assert.Equal(ext, KeyNames.IsExtendedKey(vk));
    }

    [Fact]
    public void VirtualKeys_IsModifier()
    {
        Assert.True(VirtualKeys.IsModifier(VirtualKeys.RMenu));
        Assert.False(VirtualKeys.IsModifier(VirtualKeys.A));
    }

    [Fact]
    public void KeyStroke_display_name_falls_back_to_vk_name()
    {
        Assert.Equal("A", new KeyStroke(VirtualKeys.A).DisplayName);
        Assert.Equal("Q (azerty)", new KeyStroke(VirtualKeys.A, Name: "Q (azerty)").ToString());
        Assert.True(KeyStroke.FromVirtualKey(VirtualKeys.Up).IsExtended);
    }

    [Fact]
    public void Profile_clone_is_deep()
    {
        var p = TestProfiles.Aze();
        p.TargetProcess = "notepad.exe";
        var c = p.Clone();
        c.Steps[0].HoldMs = 99;
        c.Steps.Add(new MacroStep());
        Assert.Equal(0, p.Steps[0].HoldMs);
        Assert.Equal(3, p.Steps.Count);
        Assert.Equal(p.Id, c.Id);
        Assert.Equal("notepad.exe", c.TargetProcess);
    }

    [Fact]
    public void Default_profile_is_aze_at_100ms()
    {
        var p = Profile.CreateDefault();
        Assert.Equal(100, p.IntervalMs);
        Assert.Equal("AZE", string.Concat(p.Steps.Select(s => s.Key.DisplayName)));
        Assert.Equal(Hotkey.DefaultToggle, p.Hotkey);
    }

    [Fact]
    public void Limits_clamp()
    {
        Assert.Equal(1, ProfileLimits.ClampInterval(-3));
        Assert.Equal(60_000, ProfileLimits.ClampInterval(int.MaxValue));
        Assert.Equal(0, ProfileLimits.ClampHold(-1));
        Assert.Equal(0, ProfileLimits.ClampRepeat(-1));
    }

    [Fact]
    public void KeyAction_factories_and_text()
    {
        Assert.True(KeyAction.Down(TestProfiles.A).IsDown);
        Assert.Equal("A↑", KeyAction.Up(TestProfiles.A).ToString());
    }

    [Fact]
    public void StopwatchClock_is_monotonic()
    {
        var a = StopwatchClock.Instance.NowMs;
        var b = StopwatchClock.Instance.NowMs;
        Assert.True(b >= a);
        Assert.True(StopwatchClock.TicksToMs(System.Diagnostics.Stopwatch.Frequency) is > 999.9 and < 1000.1);
    }

    [Fact]
    public void OpenGate_always_open()
    {
        Assert.True(OpenGate.Instance.CanSend());
    }

    [Fact]
    public void StandardSleeper_waits_until_target_and_honours_cancellation()
    {
        using var s = new StandardSleeper(StopwatchClock.Instance);
        var target = StopwatchClock.Instance.NowMs + 20;
        Assert.True(s.SleepUntil(target, CancellationToken.None));
        Assert.True(StopwatchClock.Instance.NowMs >= target);
        Assert.True(s.SleepUntil(0, CancellationToken.None)); // échéance passée → retour immédiat

        using var cts = new CancellationTokenSource(30);
        var t0 = StopwatchClock.Instance.NowMs;
        Assert.False(s.SleepUntil(t0 + 10_000, cts.Token));
        Assert.True(StopwatchClock.Instance.NowMs - t0 < 5_000);
        Assert.False(string.IsNullOrEmpty(s.Name));
    }
}
