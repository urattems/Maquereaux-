using Maquereaux.Core.Profiles;

namespace Maquereaux.Core.Input;

/// <summary>Donne le nom du process de la fenêtre au premier plan (implémentation Win32 dans Maquereaux.Win32).</summary>
public interface IForegroundProcessProvider
{
    /// <summary>Nom normalisé ("notepad.exe") du process au premier plan, ou null si inconnu.</summary>
    string? GetForegroundProcessName();
}

/// <summary>
/// Filtre de fenêtre cible : n'autorise l'envoi que si l'appli choisie a le focus.
/// Sécurité : évite de spammer dans Discord ou un navigateur par erreur.
/// </summary>
public sealed class ProcessGate : ISendGate
{
    private readonly IForegroundProcessProvider _provider;

    public ProcessGate(IForegroundProcessProvider provider, string targetProcess)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Target = ProfileValidator.NormalizeProcessName(targetProcess)
                 ?? throw new ArgumentException("Nom de process vide.", nameof(targetProcess));
    }

    /// <summary>Nom normalisé du process cible.</summary>
    public string Target { get; }

    public bool CanSend() => string.Equals(_provider.GetForegroundProcessName(), Target, StringComparison.OrdinalIgnoreCase);
}
