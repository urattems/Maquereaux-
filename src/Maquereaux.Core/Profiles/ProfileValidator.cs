using Maquereaux.Core.Models;

namespace Maquereaux.Core.Profiles;

/// <summary>
/// Validation et « nettoyage » des profils.
/// <list type="bullet">
/// <item><see cref="Sanitize(Profile)"/> : corrige silencieusement (bornes, valeurs nulles…) → utilisé au chargement JSON.</item>
/// <item><see cref="Validate"/> : liste les problèmes à montrer à l'utilisateur (conflits de raccourcis…).</item>
/// </list>
/// </summary>
public static class ProfileValidator
{
    /// <summary>Corrige un profil potentiellement invalide (JSON édité à la main, ancienne version…). Retourne le même objet.</summary>
    public static Profile Sanitize(Profile p)
    {
        if (p.Id == Guid.Empty) p.Id = Guid.NewGuid();
        p.Name = string.IsNullOrWhiteSpace(p.Name) ? "Sans nom" : p.Name.Trim();
        if (p.Name.Length > ProfileLimits.MaxNameLength) p.Name = p.Name[..ProfileLimits.MaxNameLength];
        p.IntervalMs = ProfileLimits.ClampInterval(p.IntervalMs);
        p.RepeatCount = ProfileLimits.ClampRepeat(p.RepeatCount);
        if (!Enum.IsDefined(p.SequenceMode)) p.SequenceMode = SequenceMode.Cycle;
        if (!Enum.IsDefined(p.ActivationMode)) p.ActivationMode = ActivationMode.Toggle;

        p.Steps ??= new List<MacroStep>();
        p.Steps.RemoveAll(s => s is null || s.Key is null || s.Key.VirtualKey == 0 || s.Key.VirtualKey > 0xFE);
        if (p.Steps.Count > ProfileLimits.MaxSteps) p.Steps.RemoveRange(ProfileLimits.MaxSteps, p.Steps.Count - ProfileLimits.MaxSteps);
        foreach (var s in p.Steps)
        {
            s.HoldMs = ProfileLimits.ClampHold(s.HoldMs);
            if (s.DelayAfterMs is { } d) s.DelayAfterMs = ProfileLimits.ClampInterval(d);
        }

        if (p.Hotkey is not null && !p.Hotkey.IsValid) p.Hotkey = null;
        p.TargetProcess = NormalizeProcessName(p.TargetProcess);
        return p;
    }

    /// <summary>Nettoie une liste complète : Ids dupliqués régénérés, liste vide → profil par défaut.</summary>
    public static List<Profile> SanitizeAll(IEnumerable<Profile?>? profiles)
    {
        var result = new List<Profile>();
        var ids = new HashSet<Guid>();
        foreach (var p in profiles ?? Enumerable.Empty<Profile?>())
        {
            if (p is null) continue;
            Sanitize(p);
            if (!ids.Add(p.Id))
            {
                p.Id = Guid.NewGuid();
                ids.Add(p.Id);
            }
            result.Add(p);
        }
        if (result.Count == 0) result.Add(Profile.CreateDefault());
        return result;
    }

    /// <summary>
    /// Normalise un nom de process : "C:\...\Notepad.EXE" → "notepad.exe" ; "notepad" → "notepad.exe" ; vide → null.
    /// </summary>
    public static string? NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.Trim().Trim('"');
        var slash = Math.Max(n.LastIndexOf('\\'), n.LastIndexOf('/'));
        if (slash >= 0) n = n[(slash + 1)..];
        n = n.ToLowerInvariant();
        if (n.Length == 0) return null;
        if (!n.EndsWith(".exe", StringComparison.Ordinal)) n += ".exe";
        return n;
    }

    /// <summary>Problèmes bloquants ou avertissements pour un profil, dans le contexte des autres profils et réglages.</summary>
    public static List<string> Validate(Profile p, IEnumerable<Profile> allProfiles, AppSettings settings)
    {
        var issues = new List<string>();
        if (p.Steps.Count == 0) issues.Add("La séquence est vide.");

        if (p.Hotkey is null)
        {
            if (p.ActivationMode == ActivationMode.Hold)
                issues.Add("Le mode « Maintenir » a besoin d'un raccourci.");
        }
        else
        {
            if (p.Hotkey == settings.EmergencyStopHotkey)
                issues.Add($"{p.Hotkey} est déjà l'arrêt d'urgence.");

            var clash = allProfiles.FirstOrDefault(o => o.Id != p.Id && o.Hotkey == p.Hotkey);
            if (clash is not null)
                issues.Add($"{p.Hotkey} est aussi le raccourci de « {clash.Name} ».");

            if (p.Hotkey.IsMouse && p.ActivationMode != ActivationMode.Hold)
                issues.Add($"{p.Hotkey} ne marche qu'en mode « Maintenir » (la macro tourne tant que le bouton est enfoncé).");
            else if (p.Hotkey.IsMouse && p.Hotkey.Modifiers == HotkeyModifiers.None && p.TargetProcess is null)
                issues.Add($"Sans fenêtre cible, chaque {p.Hotkey.ToString().ToLowerInvariant()} dans n'importe quelle appli lancera la macro : choisis la fenêtre du jeu.");

                        // Mode Hold : si la touche de maintien fait partie de la séquence, la macro se « relâcherait » elle-même
            if (p.ActivationMode == ActivationMode.Hold && p.Steps.Any(s => s.Key.VirtualKey == p.Hotkey.VirtualKey))
                issues.Add($"En mode « Maintenir », la touche du raccourci ({p.Hotkey}) ne peut pas faire partie de la séquence.");
        }

        if (p.SequenceMode == SequenceMode.Burst)
        {
            var totalHold = p.Steps.Sum(s => s.HoldMs);
            if (totalHold >= p.IntervalMs && totalHold > 0)
                issues.Add($"Burst : la somme des durées d'appui ({totalHold} ms) dépasse l'intervalle ({p.IntervalMs} ms) → la cadence ne sera pas tenue.");
        }
        else
        {
            foreach (var s in p.Steps.Where(s => s.HoldMs > 0 && s.HoldMs >= (s.DelayAfterMs ?? p.IntervalMs)))
            {
                issues.Add($"« {s.Key.DisplayName} » : l'appui ({s.HoldMs} ms) dure plus longtemps que le délai qui suit → la cadence ne sera pas tenue.");
                break;
            }
        }
        return issues;
    }
}
