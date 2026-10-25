using System.Globalization;
using Maquereaux.App.Controls;
using Maquereaux.App.ViewModels;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.App.Tests;

public class MacroLogicTests
{
    public MacroLogicTests() => CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

    [Fact]
    public void Presets_are_the_documented_values()
    {
        Assert.Equal(new[] { 10, 16, 33, 50, 100 }, MacroLogic.IntervalPresets);
    }

    [Theory]
    [InlineData(10, SequenceMode.Cycle, 3, 100)]
    [InlineData(10, SequenceMode.Burst, 3, 300)]
    [InlineData(100, SequenceMode.Cycle, 1, 10)]
    [InlineData(0, SequenceMode.Cycle, 1, 0)]
    [InlineData(20, SequenceMode.Burst, 0, 50)]
    public void Inputs_per_second(int interval, SequenceMode mode, int steps, double expected)
    {
        Assert.Equal(expected, MacroLogic.InputsPerSecond(interval, mode, steps), 6);
    }

    [Theory]
    [InlineData(10, SequenceMode.Cycle, 1, "= 100 inputs/s")]
    [InlineData(16, SequenceMode.Cycle, 1, "= 63 inputs/s")]
    [InlineData(1000, SequenceMode.Cycle, 1, "= 1 input/s")]
    [InlineData(400, SequenceMode.Cycle, 1, "= 2,5 inputs/s")]
    [InlineData(1, SequenceMode.Burst, 2, "= 2 000 inputs/s")]
    public void Equivalence_text(int interval, SequenceMode mode, int steps, string expected)
    {
        Assert.Equal(expected, MacroLogic.EquivalenceText(interval, mode, steps).Replace(' ', ' ').Replace(' ', ' '));
    }

    [Fact]
    public void Hold_advice_only_for_instant_presses_on_non_desktop_targets()
    {
        var p = Profile.CreateDefault();
        Assert.False(MacroLogic.ShouldAdviseHold(p)); // pas de cible
        p.TargetProcess = "notepad.exe";
        Assert.False(MacroLogic.ShouldAdviseHold(p)); // appli de bureau
        p.TargetProcess = "eldenring.exe";
        Assert.True(MacroLogic.ShouldAdviseHold(p));
        foreach (var s in p.Steps) s.HoldMs = 16;
        Assert.False(MacroLogic.ShouldAdviseHold(p)); // déjà corrigé
        p.Steps.Clear();
        Assert.False(MacroLogic.ShouldAdviseHold(p));
    }

    [Theory]
    [InlineData(0, "instantané")]
    [InlineData(16, "16 ms")]
    public void Hold_chip(int hold, string expected) => Assert.Equal(expected, MacroLogic.HoldChip(hold));

    [Fact]
    public void Delay_chip()
    {
        Assert.Equal("→ 10 ms", MacroLogic.DelayChip(null, 10));
        Assert.Equal("→ 50 ms", MacroLogic.DelayChip(50, 10));
    }
}

public class NumericBoxParseTests
{
    [Theory]
    [InlineData("16", false, 0, 100, 16)]
    [InlineData(" 16 ms ", false, 0, 100, 16)]
    [InlineData("1 000", false, 0, 5000, 1000)]
    [InlineData("1 000", false, 0, 5000, 1000)]
    public void Accepts_valid_input(string raw, bool allowEmpty, int min, int max, int expected)
    {
        Assert.True(NumericBox.TryParse(raw, allowEmpty, min, max, out var v));
        Assert.Equal(expected, v);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("101")]
    [InlineData("1.5")]
    [InlineData("")]
    public void Rejects_invalid_or_out_of_range(string raw)
    {
        Assert.False(NumericBox.TryParse(raw, false, 0, 100, out _));
    }

    [Fact]
    public void Empty_is_accepted_when_allowed()
    {
        Assert.True(NumericBox.TryParse("  ", true, 1, 100, out var v));
        Assert.Null(v);
    }
}

public class IntervalSliderTests
{
    [Theory]
    [InlineData(1, 0.0)]
    [InlineData(1000, 1.0)]
    [InlineData(60000, 1.0)]
    public void Slider_bounds(int ms, double pos) => Assert.Equal(pos, MacroLogic.IntervalToSlider(ms), 6);

    [Fact]
    public void Slider_is_logarithmic_and_roundtrips()
    {
        Assert.Equal(1.0 / 3, MacroLogic.IntervalToSlider(10), 6);
        Assert.Equal(2.0 / 3, MacroLogic.IntervalToSlider(100), 6);
        foreach (var ms in new[] { 1, 2, 7, 10, 16, 25, 33, 50, 100, 250, 1000 })
            Assert.Equal(ms, MacroLogic.SliderToInterval(MacroLogic.IntervalToSlider(ms)));
    }

    [Fact]
    public void Slider_snaps_to_nearby_presets()
    {
        Assert.Equal(16, MacroLogic.SliderToInterval(MacroLogic.IntervalToSlider(16) + 0.008));
        Assert.Equal(1, MacroLogic.SliderToInterval(-1));
        Assert.Equal(1000, MacroLogic.SliderToInterval(2));
    }

    [Fact]
    public void Vm_slider_keeps_long_intervals_typed_by_hand()
    {
        var vm = new ProfileViewModel(Profile.CreateDefault()) { IntervalMs = 5000 };
        vm.IntervalSlider = 1.0;
        Assert.Equal(5000, vm.IntervalMs);
        vm.IntervalSlider = 2.0 / 3;
        Assert.Equal(100, vm.IntervalMs);
    }
}

public class ProfileViewModelTests
{
    private static ProfileViewModel Vm(out List<bool> changes)
    {
        var vm = new ProfileViewModel(Profile.CreateDefault());
        var list = new List<bool>();
        vm.Changed += (_, hk) => list.Add(hk);
        changes = list;
        return vm;
    }

    private static string Keys(ProfileViewModel vm) => string.Concat(vm.Steps.Select(s => s.Key.DisplayName));

    [Fact]
    public void Move_reorders_view_and_model_and_reindexes()
    {
        var vm = Vm(out var changes);
        vm.MoveStepCommand.Execute((0, 2));
        Assert.Equal("ZEA", Keys(vm));
        Assert.Equal("ZEA", string.Concat(vm.Model.Steps.Select(s => s.Key.DisplayName)));
        Assert.Equal(new[] { 1, 2, 3 }, vm.Steps.Select(s => s.Index));
        Assert.Single(changes);
    }

    [Fact]
    public void Move_out_of_range_is_ignored()
    {
        var vm = Vm(out var changes);
        vm.MoveStepCommand.Execute((0, 9));
        vm.MoveStepUpCommand.Execute(vm.Steps[0]);
        vm.MoveStepDownCommand.Execute(vm.Steps[2]);
        Assert.Equal("AZE", Keys(vm));
        Assert.Empty(changes);
    }

    [Fact]
    public void Up_and_down_move_by_one()
    {
        var vm = Vm(out _);
        vm.MoveStepDownCommand.Execute(vm.Steps[0]);
        Assert.Equal("ZAE", Keys(vm));
        vm.MoveStepUpCommand.Execute(vm.Steps[2]);
        Assert.Equal("ZEA", Keys(vm));
    }

    [Fact]
    public void Duplicate_inserts_copy_right_after()
    {
        var vm = Vm(out _);
        vm.Steps[1].HoldMs = 12;
        vm.DuplicateStepCommand.Execute(vm.Steps[1]);
        Assert.Equal("AZZE", Keys(vm));
        Assert.Equal(12, vm.Steps[2].HoldMs);
        Assert.NotSame(vm.Steps[1].Model, vm.Steps[2].Model);
    }

    [Fact]
    public void Remove_then_undo_restores_at_same_position()
    {
        var vm = Vm(out _);
        vm.RemoveStepCommand.Execute(vm.Steps[1]);
        Assert.Equal("AE", Keys(vm));
        Assert.True(vm.CanUndoRemove);
        vm.UndoRemoveStepCommand.Execute(null);
        Assert.Equal("AZE", Keys(vm));
        Assert.False(vm.CanUndoRemove);
    }

    [Fact]
    public void Apply_hold_to_all_steps()
    {
        var vm = Vm(out var changes);
        vm.ApplyHoldToAllCommand.Execute(16);
        Assert.All(vm.Model.Steps, s => Assert.Equal(16, s.HoldMs));
        Assert.All(vm.Steps, s => Assert.Equal(16, s.HoldMs));
        Assert.NotEmpty(changes);
    }

    [Fact]
    public void Set_interval_preset_and_equivalence()
    {
        var vm = Vm(out _);
        vm.SetIntervalCommand.Execute(16);
        Assert.Equal(16, vm.IntervalMs);
        Assert.Contains("63", vm.EquivalenceText);
        vm.SequenceMode = SequenceMode.Burst;
        Assert.Contains("188", vm.EquivalenceText);
    }

    [Fact]
    public void Hold_advice_follows_target_and_holds()
    {
        var vm = Vm(out _);
        Assert.False(vm.ShowHoldAdvice);
        vm.TargetProcess = "game";
        Assert.True(vm.ShowHoldAdvice);
        vm.ApplyHoldToAllCommand.Execute(MacroLogic.RecommendedGameHoldMs);
        Assert.False(vm.ShowHoldAdvice);
    }

    [Fact]
    public void Add_step_respects_max()
    {
        var vm = Vm(out _);
        for (int i = 0; i < 100; i++) vm.AddStepCommand.Execute(KeyStroke.FromVirtualKey(VirtualKeys.A));
        Assert.Equal(ProfileLimits.MaxSteps, vm.Steps.Count);
    }
}
