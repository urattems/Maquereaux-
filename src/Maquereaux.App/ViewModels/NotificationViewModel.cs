using System.Windows.Input;

namespace Maquereaux.App.ViewModels;

public enum NotificationLevel
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Toast affiché en bas à droite (empilable, auto-fermeture pour Info/Success, action optionnelle).</summary>
public sealed class NotificationViewModel
{
    public NotificationViewModel(string message, NotificationLevel level, string? actionLabel = null, ICommand? actionCommand = null)
    {
        Message = message;
        Level = level;
        ActionLabel = actionLabel;
        ActionCommand = actionCommand;
        Time = DateTime.Now;
    }

    public string Message { get; }
    public NotificationLevel Level { get; }
    public DateTime Time { get; }

    /// <summary>Libellé du bouton d'action (ex. « Annuler »), null = pas d'action.</summary>
    public string? ActionLabel { get; }

    public ICommand? ActionCommand { get; }
}
