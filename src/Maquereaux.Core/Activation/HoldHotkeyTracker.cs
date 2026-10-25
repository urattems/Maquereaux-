using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Activation;

/// <summary>Ce que le hook doit faire d'un événement : l'avaler ou non, et quel profil démarrer/arrêter.</summary>
public readonly record struct HoldDecision(bool Swallow, Guid? StartProfile, Guid? StopProfile)
{
    public static readonly HoldDecision None = default;
}

/// <summary>
/// Détection des raccourcis en mode « Maintenir » à partir des événements bruts du hook clavier.
/// <para>
/// Appelé DANS le callback du hook : doit être ultra rapide (dictionnaire + quelques booléens, aucune allocation).
/// Un seul thread appelle <see cref="Process"/> (celui du hook) ; <see cref="SetBindings"/> peut être
/// appelé depuis un autre thread (échange atomique de la table).
/// </para>
/// <para>
/// Boutons de souris : traités comme des touches (VK 0x01, 0x02…), mais jamais avalés — le clic doit
/// toujours arriver au jeu / à l'appli.
/// </para>
/// <para>
/// Règle clé : les événements INJECTÉS sont ignorés. Sinon une macro qui envoie la touche F7 pourrait
/// se déclencher (ou s'arrêter) elle-même.
/// </para>
/// </summary>
public sealed class HoldHotkeyTracker
{
    private volatile Dictionary<ushort, List<(Guid ProfileId, HotkeyModifiers Mods)>> _byKey = new();
    private readonly Dictionary<ushort, Guid> _held = new(); // touche maintenue → profil démarré
    private bool _ctrlL, _ctrlR, _altL, _altR, _shiftL, _shiftR, _winL, _winR;

    /// <summary>Remplace les raccourcis surveillés (profils en mode Hold).</summary>
    public void SetBindings(IEnumerable<(Guid ProfileId, Hotkey Hotkey)> bindings)
    {
        var map = new Dictionary<ushort, List<(Guid, HotkeyModifiers)>>();
        foreach (var (id, hk) in bindings)
        {
            if (hk is null || !hk.IsValid) continue;
            if (!map.TryGetValue(hk.VirtualKey, out var list)) map[hk.VirtualKey] = list = new();
            list.Add((id, hk.Modifiers));
        }
        _byKey = map;
    }

    public bool HasBindings => _byKey.Count > 0;

    /// <summary>Au moins un raccourci « Maintenir » est un bouton de souris (→ hook souris nécessaire).</summary>
    public bool HasMouseBindings => _byKey.Keys.Any(VirtualKeys.IsMouseButton);

    private static bool Swallowable(ushort vk) => !VirtualKeys.IsMouseButton(vk);

    /// <summary>Modificateurs physiquement enfoncés (suivis à partir des événements reçus).</summary>
    public HotkeyModifiers CurrentModifiers =>
        (_ctrlL || _ctrlR ? HotkeyModifiers.Control : 0) |
        (_altL || _altR ? HotkeyModifiers.Alt : 0) |
        (_shiftL || _shiftR ? HotkeyModifiers.Shift : 0) |
        (_winL || _winR ? HotkeyModifiers.Win : 0);

    public HoldDecision Process(in RawKeyEvent e)
    {
        if (e.IsInjected) return HoldDecision.None;
        if (TrackModifier(e.VirtualKey, e.IsDown)) return HoldDecision.None;

        if (!e.IsDown)
        {
            // Relâchement : on stoppe si c'est une touche que NOUS avons prise en charge à l'appui
            // (peu importe l'état des modificateurs à ce moment-là).
            if (_held.Remove(e.VirtualKey, out var stopped))
                return new HoldDecision(Swallowable(e.VirtualKey), null, stopped);
            return HoldDecision.None;
        }

        // Répétition automatique du clavier pendant le maintien : on continue d'avaler, sans nouvel ordre
        if (_held.ContainsKey(e.VirtualKey)) return new HoldDecision(Swallowable(e.VirtualKey), null, null);

        if (!_byKey.TryGetValue(e.VirtualKey, out var candidates)) return HoldDecision.None;
        var mods = CurrentModifiers;
        foreach (var (profileId, required) in candidates)
        {
            if (required != mods) continue; // correspondance exacte (F7 ≠ Ctrl+F7)
            _held[e.VirtualKey] = profileId;
            return new HoldDecision(Swallowable(e.VirtualKey), profileId, null);
        }
        return HoldDecision.None;
    }

    /// <summary>Oublie les touches maintenues (ex. après un arrêt d'urgence).</summary>
    public void Reset() => _held.Clear();

    private bool TrackModifier(ushort vk, bool down)
    {
        switch (vk)
        {
            case VirtualKeys.LControl: case VirtualKeys.Control: _ctrlL = down; return true;
            case VirtualKeys.RControl: _ctrlR = down; return true;
            case VirtualKeys.LMenu: case VirtualKeys.Menu: _altL = down; return true;
            case VirtualKeys.RMenu: _altR = down; return true;
            case VirtualKeys.LShift: case VirtualKeys.Shift: _shiftL = down; return true;
            case VirtualKeys.RShift: _shiftR = down; return true;
            case VirtualKeys.LWin: _winL = down; return true;
            case VirtualKeys.RWin: _winR = down; return true;
            default: return false;
        }
    }
}
