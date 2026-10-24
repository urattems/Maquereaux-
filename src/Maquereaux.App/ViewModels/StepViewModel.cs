using CommunityToolkit.Mvvm.ComponentModel;
using Maquereaux.Core.Models;

namespace Maquereaux.App.ViewModels;

/// <summary>Une étape éditable de la séquence (écrit directement dans le modèle <see cref="MacroStep"/>).</summary>
public sealed partial class StepViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public StepViewModel(MacroStep model, Action onChanged)
    {
        Model = model;
        _onChanged = onChanged;
    }

    public MacroStep Model { get; }

    /// <summary>Numéro affiché (1, 2, 3…).</summary>
    [ObservableProperty]
    private int _index;

    public KeyStroke Key
    {
        get => Model.Key;
        set
        {
            if (value is null || value == Model.Key) return;
            Model.Key = value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public int HoldMs
    {
        get => Model.HoldMs;
        set
        {
            var v = ProfileLimits.ClampHold(value);
            if (SetProperty(Model.HoldMs, v, Model, (m, x) => m.HoldMs = x)) _onChanged();
            else if (v != value) OnPropertyChanged(); // valeur bornée : on rafraîchit le champ
        }
    }

    /// <summary>null = intervalle global du profil.</summary>
    public int? DelayAfterMs
    {
        get => Model.DelayAfterMs;
        set
        {
            int? v = value is null ? null : ProfileLimits.ClampInterval(value.Value);
            if (SetProperty(Model.DelayAfterMs, v, Model, (m, x) => m.DelayAfterMs = x)) _onChanged();
            else if (v != value) OnPropertyChanged();
        }
    }
}
