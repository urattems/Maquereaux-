using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.App.ViewModels;

/// <summary>Une étape de la visite guidée.</summary>
public sealed record OnboardingStep(string Title, string Text, string IconKey, string Page);

/// <summary>Visite guidée (3 étapes, passable, rejouable) et réglages du mode compact.</summary>
public sealed partial class MainViewModel
{
    public IReadOnlyList<OnboardingStep> OnboardingSteps { get; } = new[]
    {
        new OnboardingStep("Compose ta séquence",
            "Clique sur « + » puis appuie sur une touche. Glisse les touches pour les réordonner ; leurs puces règlent la durée d'appui et le délai.",
            "Icon.Keyboard", "Macro"),
        new OnboardingStep("Règle la cadence",
            "Choisis l'intervalle au curseur, au clavier ou en un clic sur un preset. Maquereaux t'indique tout de suite combien d'inputs par seconde ça donne.",
            "Icon.Timer", "Macro"),
        new OnboardingStep("Lance, mesure, compare",
            "Démarre avec le raccourci du profil (F6 par défaut) ou la carte héros. Ctrl+Alt+Pause coupe tout, partout. Le Labo mesure ta précision contre celle de la macro.",
            "Icon.Play", "Labo"),
    };

    [ObservableProperty]
    private bool _isOnboardingVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentOnboardingStep))]
    [NotifyPropertyChangedFor(nameof(IsLastOnboardingStep))]
    [NotifyPropertyChangedFor(nameof(OnboardingProgress))]
    private int _onboardingIndex;

    public OnboardingStep CurrentOnboardingStep => OnboardingSteps[Math.Clamp(OnboardingIndex, 0, OnboardingSteps.Count - 1)];
    public bool IsLastOnboardingStep => OnboardingIndex >= OnboardingSteps.Count - 1;
    public string OnboardingProgress => $"{OnboardingIndex + 1} / {OnboardingSteps.Count}";

    /// <summary>À appeler au démarrage : ouvre la visite si elle n'a jamais été terminée ni passée.</summary>
    public void ShowOnboardingIfNeeded()
    {
        if (!_settings.OnboardingCompleted) StartOnboarding();
    }

    [RelayCommand]
    private void StartOnboarding()
    {
        OnboardingIndex = 0;
        IsOnboardingVisible = true;
    }

    [RelayCommand]
    private void NextOnboarding()
    {
        if (IsLastOnboardingStep) { FinishOnboarding(); return; }
        OnboardingIndex++;
    }

    [RelayCommand]
    private void PreviousOnboarding()
    {
        if (OnboardingIndex > 0) OnboardingIndex--;
    }

    /// <summary>Passer ou terminer : la visite ne revient plus d'elle-même (rejouable depuis Paramètres).</summary>
    [RelayCommand]
    private void FinishOnboarding()
    {
        IsOnboardingVisible = false;
        if (_settings.OnboardingCompleted) return;
        _settings.OnboardingCompleted = true;
        MarkDirty();
    }

    // ---------- Mode compact ----------

    /// <summary>Opacité de la pastille compacte (30 % à 100 %).</summary>
    public double CompactOpacity
    {
        get => _settings.CompactOpacity;
        set
        {
            var v = Math.Clamp(double.IsFinite(value) ? value : 0.95, AppSettings.MinCompactOpacity, 1);
            if (Math.Abs(v - _settings.CompactOpacity) < 1e-6) return;
            _settings.CompactOpacity = v;
            OnPropertyChanged();
            MarkDirty();
        }
    }
}
