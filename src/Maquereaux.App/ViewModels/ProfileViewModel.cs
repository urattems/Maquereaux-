using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.App.ViewModels;

/// <summary>Profil éditable. Toute modification lève <see cref="Changed"/> (sauvegarde auto, hotkeys, instantané moteur).</summary>
public sealed partial class ProfileViewModel : ObservableObject
{
    private (int Index, MacroStep Step)? _lastRemoved;

    public ProfileViewModel(Profile model)
    {
        Model = model;
        Steps = new ObservableCollection<StepViewModel>(model.Steps.Select(s => new StepViewModel(s, this)));
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
                NotifyCadence();
                foreach (var s in Steps) s.NotifyChips();
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
                NotifyCadence();
                OnPropertyChanged(nameof(IsBurst));
                RaiseChanged();
            }
        }
    }

    public bool IsBurst => Model.SequenceMode == SequenceMode.Burst;

    public int RepeatCount
    {
        get => Model.RepeatCount;
        set
        {
            var v = ProfileLimits.ClampRepeat(value);
            if (SetProperty(Model.RepeatCount, v, Model, (m, x) => m.RepeatCount = x))
            {
                OnPropertyChanged(nameof(RepeatText));
                RaiseChanged();
            }
            else if (v != value) OnPropertyChanged();
        }
    }

    /// <summary>« en boucle » ou « 5 cycles puis arrêt ».</summary>
    public string RepeatText => Model.RepeatCount == 0 ? "en boucle, jusqu'à l'arrêt" : $"{Model.RepeatCount} cycle(s) puis arrêt automatique";

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

    public ActivationMode ActivationMode
    {
        get => Model.ActivationMode;
        set
        {
            if (SetProperty(Model.ActivationMode, value, Model, (m, x) => m.ActivationMode = x))
            {
                OnPropertyChanged(nameof(ActivationText));
                RaiseChanged(true);
            }
        }
    }

    public string ActivationText => Model.ActivationMode == ActivationMode.Hold ? "maintenir" : "ON/OFF";

    /// <summary>Process cible (filtre de fenêtre), null = toutes les fenêtres.</summary>
    public string? TargetProcess
    {
        get => Model.TargetProcess;
        set
        {
            var v = Core.Profiles.ProfileValidator.NormalizeProcessName(value);
            if (SetProperty(Model.TargetProcess, v, Model, (m, x) => m.TargetProcess = x))
            {
                OnPropertyChanged(nameof(TargetText));
                OnPropertyChanged(nameof(HasTarget));
                OnPropertyChanged(nameof(ShowHoldAdvice));
                RaiseChanged();
            }
            else if (v != value) OnPropertyChanged();
        }
    }

    public string TargetText => Model.TargetProcess ?? "toutes les fenêtres";
    public bool HasTarget => Model.TargetProcess is not null;

    [RelayCommand]
    private void ClearTargetProcess() => TargetProcess = null;

    /// <summary>Problèmes détectés (conflits de raccourcis, séquence vide…), recalculés par le MainViewModel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIssues))]
    private IReadOnlyList<string> _issues = Array.Empty<string>();

    public bool HasIssues => Issues.Count > 0;

    /// <summary>Profil actuellement sélectionné (mise en évidence dans la liste).</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Profil en cours d'exécution.</summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>Résumé court pour les listes : "3 touches · 10 ms · Cycle".</summary>
    public string Summary => $"{Model.Steps.Count} touche{(Model.Steps.Count > 1 ? "s" : "")} · {Model.IntervalMs} ms · {(IsBurst ? "Burst" : "Cycle")}";

    // ---------- Cadence ----------

    public IReadOnlyList<int> IntervalPresets => MacroLogic.IntervalPresets;

    /// <summary>« = 100 inputs/s » selon l'intervalle, le mode et le nombre de touches.</summary>
    public string EquivalenceText => MacroLogic.EquivalenceText(Model.IntervalMs, Model.SequenceMode, Model.Steps.Count);

    [RelayCommand]
    private void SetInterval(int ms) => IntervalMs = ms;

    /// <summary>Position du curseur d'intervalle (0..1, échelle logarithmique 1–1000 ms).</summary>
    public double IntervalSlider
    {
        get => MacroLogic.IntervalToSlider(Model.IntervalMs);
        set
        {
            var ms = MacroLogic.SliderToInterval(value);
            // Un intervalle > 1000 ms saisi au clavier ne doit pas être écrasé par un simple rafraîchissement du curseur
            if (ms == MacroLogic.SliderMaxIntervalMs && Model.IntervalMs > ms) return;
            IntervalMs = ms;
        }
    }

    private void NotifyCadence()
    {
        OnPropertyChanged(nameof(IntervalSlider));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(EquivalenceText));
    }

    // ---------- Conseil sur la durée d'appui ----------

    /// <summary>Afficher « Certains jeux ratent les appuis instantanés » ?</summary>
    public bool ShowHoldAdvice => MacroLogic.ShouldAdviseHold(Model);

    /// <summary>Applique la même durée d'appui à toute la séquence (ex. 16 ms pour les jeux).</summary>
    [RelayCommand]
    private void ApplyHoldToAll(int holdMs)
    {
        var v = ProfileLimits.ClampHold(holdMs);
        foreach (var s in Steps) s.SetHoldSilently(v);
        AfterStepEdit();
    }

    // ---------- Édition de la séquence ----------

    [RelayCommand]
    private void AddStep(Core.Models.KeyStroke? key)
    {
        if (key is null || Model.Steps.Count >= ProfileLimits.MaxSteps) return;
        var step = new MacroStep(key);
        Model.Steps.Add(step);
        Steps.Add(new StepViewModel(step, this));
        AfterStructureChange();
    }

    [RelayCommand]
    private void RemoveStep(StepViewModel? step)
    {
        if (step is null) return;
        int i = Steps.IndexOf(step);
        if (i < 0) return;
        _lastRemoved = (i, step.Model);
        Model.Steps.RemoveAt(i);
        Steps.RemoveAt(i);
        OnPropertyChanged(nameof(CanUndoRemove));
        AfterStructureChange();
    }

    /// <summary>Une suppression peut être annulée (dernière étape supprimée).</summary>
    public bool CanUndoRemove => _lastRemoved is not null;

    [RelayCommand]
    private void UndoRemoveStep()
    {
        if (_lastRemoved is not { } r || Model.Steps.Count >= ProfileLimits.MaxSteps) return;
        int i = Math.Clamp(r.Index, 0, Steps.Count);
        Model.Steps.Insert(i, r.Step);
        Steps.Insert(i, new StepViewModel(r.Step, this));
        _lastRemoved = null;
        OnPropertyChanged(nameof(CanUndoRemove));
        AfterStructureChange();
    }

    [RelayCommand]
    private void DuplicateStep(StepViewModel? step)
    {
        if (step is null || Model.Steps.Count >= ProfileLimits.MaxSteps) return;
        int i = Steps.IndexOf(step);
        if (i < 0) return;
        var copy = step.Model.Clone();
        Model.Steps.Insert(i + 1, copy);
        Steps.Insert(i + 1, new StepViewModel(copy, this));
        AfterStructureChange();
    }

    [RelayCommand]
    private void MoveStepUp(StepViewModel? step) => MoveBy(step, -1);

    [RelayCommand]
    private void MoveStepDown(StepViewModel? step) => MoveBy(step, +1);

    /// <summary>Déplace une étape de l'index <c>from</c> à l'index <c>to</c> (glisser-déposer).</summary>
    [RelayCommand]
    private void MoveStep((int From, int To) move)
    {
        var (i, j) = move;
        if (i < 0 || j < 0 || i >= Steps.Count || j >= Steps.Count || i == j) return;
        var step = Steps[i];
        Steps.Move(i, j);
        Model.Steps.RemoveAt(i);
        Model.Steps.Insert(j, step.Model);
        AfterStructureChange();
    }

    [RelayCommand]
    private void ClearSteps()
    {
        Model.Steps.Clear();
        Steps.Clear();
        AfterStructureChange();
    }

    private void MoveBy(StepViewModel? step, int delta)
    {
        if (step is null) return;
        int i = Steps.IndexOf(step);
        MoveStep((i, i + delta));
    }

    private void AfterStructureChange()
    {
        Reindex();
        NotifyCadence();
        OnPropertyChanged(nameof(HasSteps));
        OnPropertyChanged(nameof(StepCountText));
        OnPropertyChanged(nameof(ShowHoldAdvice));
        RaiseChanged();
    }

    /// <summary>Appelé par une étape modifiée (appui, délai, touche).</summary>
    internal void AfterStepEdit()
    {
        OnPropertyChanged(nameof(ShowHoldAdvice));
        RaiseChanged();
    }

    public bool HasSteps => Steps.Count > 0;

    public string StepCountText => $"{Steps.Count} / {ProfileLimits.MaxSteps}";

    private void Reindex()
    {
        for (int i = 0; i < Steps.Count; i++) Steps[i].Index = i + 1;
    }

    private void RaiseChanged() => RaiseChanged(false);

    private void RaiseChanged(bool hotkeysAffected) => Changed?.Invoke(this, hotkeysAffected);

    public override string ToString() => Name;
}
