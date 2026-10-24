using System.Collections.Concurrent;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Profiles;

/// <summary>
/// Copies « figées » des profils, lisibles depuis n'importe quel thread.
/// <para>
/// Pourquoi : l'UI modifie les profils sur le thread UI, mais les hotkeys démarrent le moteur depuis un
/// autre thread. Lire un objet pendant que l'UI l'édite = risque de crash (« collection modifiée »).
/// L'UI publie donc une copie à chaque modification ; les autres threads ne lisent que ces copies.
/// </para>
/// </summary>
public sealed class ProfileRegistry
{
    private readonly ConcurrentDictionary<Guid, Profile> _snapshots = new();

    /// <summary>Publie (ou remplace) l'instantané d'un profil.</summary>
    public void Publish(Profile profile) => _snapshots[profile.Id] = profile.Clone();

    public void Remove(Guid id) => _snapshots.TryRemove(id, out _);

    public void Clear() => _snapshots.Clear();

    /// <summary>Retourne une copie (l'appelant peut la modifier sans risque), ou null.</summary>
    public Profile? Find(Guid id) => _snapshots.TryGetValue(id, out var p) ? p.Clone() : null;

    public IReadOnlyList<Profile> All => _snapshots.Values.Select(p => p.Clone()).ToList();

    public int Count => _snapshots.Count;
}
