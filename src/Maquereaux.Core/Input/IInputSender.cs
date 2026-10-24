using Maquereaux.Core.Models;

namespace Maquereaux.Core.Input;

/// <summary>Un événement clavier élémentaire à envoyer : appui (down) ou relâchement (up).</summary>
public readonly record struct KeyAction(KeyStroke Key, bool IsDown)
{
    public static KeyAction Down(KeyStroke key) => new(key, true);
    public static KeyAction Up(KeyStroke key) => new(key, false);
    public override string ToString() => $"{Key.DisplayName}{(IsDown ? "↓" : "↑")}";
}

/// <summary>
/// Envoi d'événements clavier. Implémentation réelle : <c>SendInputSender</c> (Maquereaux.Win32) ;
/// dans les tests : un fake qui enregistre simplement les actions.
/// </summary>
public interface IInputSender
{
    /// <summary>Appelé au démarrage du moteur : l'occasion de résoudre les scan codes (disposition clavier courante).</summary>
    void Prepare(IEnumerable<KeyStroke> keys);

    /// <summary>Envoie les actions dans l'ordre, en un seul lot si possible (un seul appel SendInput).</summary>
    void Send(IReadOnlyList<KeyAction> actions);
}

/// <summary>
/// Autorise ou non l'envoi à l'instant présent (ex. filtre « seulement si notepad.exe a le focus »).
/// Interrogé à chaque tick : doit être rapide.
/// </summary>
public interface ISendGate
{
    bool CanSend();
}

/// <summary>Porte toujours ouverte (pas de filtre).</summary>
public sealed class OpenGate : ISendGate
{
    public static readonly OpenGate Instance = new();
    public bool CanSend() => true;
}
