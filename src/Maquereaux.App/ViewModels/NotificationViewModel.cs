namespace Maquereaux.App.ViewModels;

public enum NotificationLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>Message affiché dans le bandeau de notifications (bas de fenêtre).</summary>
public sealed class NotificationViewModel
{
    public NotificationViewModel(string message, NotificationLevel level)
    {
        Message = message;
        Level = level;
        Time = DateTime.Now;
    }

    public string Message { get; }
    public NotificationLevel Level { get; }
    public DateTime Time { get; }
    public string Icon => Level switch
    {
        NotificationLevel.Error => "✖",
        NotificationLevel.Warning => "⚠",
        _ => "ℹ",
    };
}
