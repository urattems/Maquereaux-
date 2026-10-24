using System.Text.Json.Serialization;
using Maquereaux.Core.Input;

namespace Maquereaux.Core.Models;

/// <summary>
/// Identifie une touche du clavier.
/// <para>
/// <see cref="VirtualKey"/> (VK) = « quelle touche logique » (dépend de la disposition AZERTY/QWERTY).
/// <see cref="ScanCode"/> = « quelle position physique » (ce que lisent beaucoup de jeux).
/// Le scan code est résolu au moment de la capture, puis re-résolu depuis le VK au démarrage
/// du moteur avec la disposition clavier courante (un profil créé en AZERTY reste correct en QWERTY).
/// </para>
/// </summary>
public sealed record KeyStroke(ushort VirtualKey, ushort ScanCode = 0, bool IsExtended = false, string? Name = null)
{
    /// <summary>Nom affichable (nom capturé, sinon nom par défaut du VK).</summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? KeyNames.GetName(VirtualKey) : Name!;

    /// <summary>Crée une touche à partir d'un VK seul (scan code résolu plus tard par Win32).</summary>
    public static KeyStroke FromVirtualKey(ushort vk) =>
        new(vk, 0, KeyNames.IsExtendedKey(vk), KeyNames.GetName(vk));

    public override string ToString() => DisplayName;
}
