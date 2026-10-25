using CommunityToolkit.Mvvm.ComponentModel;
using Maquereaux.Core.Models;

namespace Maquereaux.App.ViewModels;

/// <summary>Une étape de la séquence, affichée comme une touche de clavier (écrit directement dans <see cref="MacroStep"/>).</summary>
public sealed partial class StepViewModel : ObservableObject
{
    private readonly ProfileViewModel _owner;

    public StepViewModel(MacroStep model, ProfileViewModel owner)
    {
        Model = model;
        _owner = owner;
    }

    public MacroStep Model { get; }

    /// <summary>Numéro affiché (1, 2, 3…).</summary>
    [ObservableProperty]
    private int _index;

    /// <summary>En attente d'une nouvelle touche (« Appuie sur une touche… »).</summary>
    [ObservableProperty]
    private bool _isCapturing;

    public KeyStroke Key
    {
        get => Model.Key;
        set
        {
            if (value is null || value == Model.Key) return;
            Model.Key = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Label));
            _owner.AfterStepEdit();
        }
    }

    /// <summary>Libellé de la touche (« A », « Espace », « F6 »).</summary>
    public string Label => Model.Key.DisplayName;

    public int HoldMs
    {
        get => Model.HoldMs;
        set
        {
            var v = ProfileLimits.ClampHold(value);
            if (SetProperty(Model.HoldMs, v, Model, (m, x) => m.HoldMs = x))
            {
                NotifyChips();
                _owner.AfterStepEdit();
            }
            else if (v != value) OnPropertyChanged();
        }
    }

    /// <summary>null = intervalle global du profil.</summary>
    public int? DelayAfterMs
    {
        get => Model.DelayAfterMs;
        set
        {
            int? v = value is null ? null : ProfileLimits.ClampInterval(value.Value);
            if (SetProperty(Model.DelayAfterMs, v, Model, (m, x) => m.DelayAfterMs = x))
            {
                NotifyChips();
                _owner.AfterStepEdit();
            }
            else if (v != value) OnPropertyChanged();
        }
    }

    public string HoldChip => MacroLogic.HoldChip(Model.HoldMs);
    public string DelayChip => MacroLogic.DelayChip(Model.DelayAfterMs, _owner.IntervalMs);
    public bool HasCustomDelay => Model.DelayAfterMs is not null;
    public bool IsInstant => Model.HoldMs == 0;

    /// <summary>Change la durée d'appui sans prévenir le profil (application en lot).</summary>
    internal void SetHoldSilently(int holdMs)
    {
        if (Model.HoldMs == holdMs) return;
        Model.HoldMs = holdMs;
        OnPropertyChanged(nameof(HoldMs));
        NotifyChips();
    }

    internal void NotifyChips()
    {
        OnPropertyChanged(nameof(HoldChip));
        OnPropertyChanged(nameof(DelayChip));
        OnPropertyChanged(nameof(HasCustomDelay));
        OnPropertyChanged(nameof(IsInstant));
    }
}
