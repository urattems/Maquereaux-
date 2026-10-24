using System.Windows;
using Microsoft.Win32;

namespace Maquereaux.App.Services;

/// <summary>Boîtes de dialogue (fichiers, confirmations). Isolées ici pour garder les ViewModels testables.</summary>
public interface IDialogService
{
    string? OpenFile(string title, string filter);
    string? SaveFile(string title, string filter, string defaultName);
    bool Confirm(string title, string message);
}

public sealed class DialogService : IDialogService
{
    public const string JsonFilter = "Profils Maquereaux (*.json)|*.json|Tous les fichiers (*.*)|*.*";
    public const string CsvFilter = "CSV (*.csv)|*.csv";
    public const string SessionFilter = "Session Labo (*.mqsession.json)|*.mqsession.json|JSON (*.json)|*.json";

    public string? OpenFile(string title, string filter)
    {
        var dlg = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dlg.ShowDialog(Application.Current?.MainWindow) == true ? dlg.FileName : null;
    }

    public string? SaveFile(string title, string filter, string defaultName)
    {
        var dlg = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultName, AddExtension = true, OverwritePrompt = true };
        return dlg.ShowDialog(Application.Current?.MainWindow) == true ? dlg.FileName : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(Application.Current?.MainWindow!, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
