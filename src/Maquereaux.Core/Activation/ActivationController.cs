using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Activation;

/// <summary>
/// Traduit les événements « humains » (hotkey pressé, touche maintenue/relâchée, bouton, urgence)
/// en ordres Start/Stop pour le moteur. Logique pure, testée avec un faux moteur.
/// <para>À appeler depuis UN seul thread (voir <see cref="SerialExecutor"/>) : pas de verrou interne.</para>
/// </summary>
public sealed class ActivationController
{
    private readonly IMacroRunner _runner;
    private readonly Func<Guid, Profile?> _findProfile;
    private readonly Func<Profile, ISendGate?> _gateFactory;
    private Guid? _holdProfileId;

    /// <param name="runner">Le moteur.</param>
    /// <param name="findProfile">Retrouve un profil par Id (état courant, éventuellement modifié dans l'UI).</param>
    /// <param name="gateFactory">Crée le filtre d'envoi d'un profil (ex. filtre de fenêtre), ou null.</param>
    public ActivationController(IMacroRunner runner, Func<Guid, Profile?> findProfile, Func<Profile, ISendGate?>? gateFactory = null)
    {
        _runner = runner;
        _findProfile = findProfile;
        _gateFactory = gateFactory ?? (_ => null);
    }

    /// <summary>Levé quand une action ne peut pas aboutir (ex. séquence vide). Message en français pour l'UI.</summary>
    public event Action<string>? Error;

    /// <summary>Levé quand un profil est démarré via un raccourci (l'UI peut le sélectionner comme profil actif).</summary>
    public event Action<Guid>? ProfileActivated;

    /// <summary>Mode Toggle : même profil en cours → stop ; sinon (re)démarre ce profil (un seul actif à la fois).</summary>
    public void Toggle(Guid profileId)
    {
        if (_runner.IsRunning && _runner.RunningProfile?.Id == profileId)
        {
            _runner.Stop(StopReason.Manual);
            return;
        }
        StartProfile(profileId);
    }

    /// <summary>Mode Hold : touche enfoncée → démarre (les répétitions automatiques du clavier sont ignorées).</summary>
    public void HoldDown(Guid profileId)
    {
        if (_holdProfileId == profileId && _runner.IsRunning) return; // auto-repeat
        if (StartProfile(profileId)) _holdProfileId = profileId;
    }

    /// <summary>Mode Hold : touche relâchée → stop (seulement si c'est bien ce profil qui tourne).</summary>
    public void HoldUp(Guid profileId)
    {
        if (_holdProfileId != profileId) return;
        _holdProfileId = null;
        if (_runner.IsRunning && _runner.RunningProfile?.Id == profileId)
            _runner.Stop(StopReason.HoldReleased);
    }

    /// <summary>Arrêt d'urgence : coupe tout, toujours.</summary>
    public void EmergencyStop()
    {
        _holdProfileId = null;
        _runner.Stop(StopReason.Emergency);
    }

    /// <summary>Arrêt simple (bouton Stop).</summary>
    public void Stop()
    {
        _holdProfileId = null;
        _runner.Stop(StopReason.Manual);
    }

    private bool StartProfile(Guid profileId)
    {
        var profile = _findProfile(profileId);
        if (profile is null)
        {
            Error?.Invoke("Profil introuvable.");
            return false;
        }
        if (profile.Steps.Count == 0)
        {
            Error?.Invoke($"« {profile.Name} » : la séquence est vide, ajoute au moins une touche.");
            return false;
        }
        try
        {
            _runner.Start(profile, _gateFactory(profile));
            ProfileActivated?.Invoke(profileId);
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Impossible de démarrer « {profile.Name} » : {ex.Message}");
            return false;
        }
    }
}
