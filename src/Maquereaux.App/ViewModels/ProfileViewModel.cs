using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.App.ViewModels;

/// <summary>Profil éditable. Toute modification lève <see cref="Changed"/> (sauvegarde auto, hotkeys, instantané moteur).</summary>
public sealed partial class ProfileViewModel : ObservableObject
{
    public ProfileViewModel(Profile model)
    {
        Model = model;
        Steps = new ObservableCollection<StepViewModel>(model.Steps.Select(s => new StepViewModel(s, RaiseChanged)));
        Reindex();
    }

    public Profile Model { get; }
    public Guid Id => Model.Id;
    public ObservableCollection<StepViewModel> Steps { get; }

    /// <summary>Le profil a changé. Paramètre : true si le raccourci/mode d'activation a changé (ré-enregistrement des hotkeys).</summary>
    public event Action<ProfileViewModel, bool>? Changed;

    public string Name
    {
        get => Model.Name;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "Sans nom" : value.Trim();
            if (v.Length > ProfileLimits.MaxNameLength) v = v[..ProfileLimits.MaxNameLength];
            if (SetProperty(Model.Name, v, Model, (m, x) => m.Name = x)) RaiseChanged(true);
        }
    }

    public int IntervalMs
    {
        get => Model.IntervalMs;
        set
        {
            var v = ProfileLimits.ClampInterval(value);
            if (SetProperty(Model.IntervalMs, v, Model, (m, x) => m.IntervalMs = x))
            {
                OnPropertyChanged(nameof(Summary));
                RaiseChanged();
            }
            else if (v != value) OnPropertyChanged();
        }
    }

    public SequenceMode SequenceMode
    {
        get => Model.SequenceMode;
        set
        {
            if (SetProperty(Model.SequenceMode, value, Model, (m, x) => m.SequenceMode = x))
            {
                OnPropertyChanged(nameof(Summary));
                RaiseChanged();
            }
        }
    }

    public int RepeatCount
    {
        get => Model.RepeatCount;
        set
        {
            var v = ProfileLimits.ClampRepeat(value);
            if (SetProperty(Model.RepeatCount, v, Model, (m, x) => m.RepeatCount = x)) RaiseChanged();
            else if (v != value) OnPropertyChanged();
        }
    }

    public Hotkey? Hotkey
    {
        get => Model.Hotkey;
        set
        {
            if (SetProperty(Model.Hotkey, value, Model, (m, x) => m.Hotkey = x))
            {
                OnPropertyChanged(nameof(HotkeyText));
                RaiseChanged(true);
            }
        }
    }

    public string HotkeyText => Model.Hotkey?.ToString() ?? "aucun raccourci";

    /// <summary>Résumé court pour les listes : "3 touches · 10 ms · Cycle".</summary>
    public string Summary => $"{Model.Steps.Count} touche(s) · {Model.IntervalMs} ms · {Model.SequenceMode}";

    // ---------- Édition de la séquence ----------

    [RelayCommand]
    private void AddStep(KeyStroke? key)
    {
        if (key is null || Model.Steps.Count >= ProfileLimits.MaxSteps) return;
        var step = new MacroStep(key);
        Model.Steps.Add(step);
        Steps.Add(new StepViewModel(step, RaiseChanged));
        AfterStructureChange();
    }

    [RelayCommand]
    private void RemoveStep(StepViewModel? step)
    {
        if (step is null) return;
        Model.Steps.Remove(step.Model);
        Steps.Remove(step);
        AfterStructureChange();
    }

    [RelayCommand]
    private void MoveStepUp(StepViewModel? step) => Move(step, -1);

    [RelayCommand]
    private void MoveStepDown(StepViewModel? step) => Move(step, +1);

    [RelayCommand]
    private void ClearSteps()
    {
        Model.Steps.Clear();
        Steps.Clear();
        AfterStructureChange();
    }

    private void Move(StepViewModel? step, int delta)
    {
        if (step is null) return;
        int i = Steps.IndexOf(step);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= Steps.Count) return;
        Steps.Move(i, j);
        Model.Steps.RemoveAt(i);
        Model.Steps.Insert(j, step.Model);
        AfterStructureChange();
    }

    private void AfterStructureChange()
    {
        Reindex();
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSteps));
        RaiseChanged();
    }

    public bool HasSteps => Steps.Count > 0;

    private void Reindex()
    {
        for (int i = 0; i < Steps.Count; i++) Steps[i].Index = i + 1;
    }

    private void RaiseChanged() => RaiseChanged(false);

    private void RaiseChanged(bool hotkeysAffected) => Changed?.Invoke(this, hotkeysAffected);

    public override string ToString() => Name;
}
