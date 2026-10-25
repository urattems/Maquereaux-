using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Maquereaux.App.Services;

/// <summary>Une appli ouverte proposée comme fenêtre cible.</summary>
public sealed record ProcessEntry(string Name, string Title, ImageSource? Icon)
{
    /// <summary>Texte affiché sous le nom (titre de la fenêtre principale).</summary>
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Title.Contains(query, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Liste des applis ayant une fenêtre visible, avec leur icône (extraite de l'exe) et le titre de leur fenêtre.
/// Lecture seule, sur demande (ouverture du sélecteur) : aucune surveillance en tâche de fond.
/// </summary>
public static class ProcessCatalog
{
    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Énumère les applis fenêtrées (à appeler hors du thread UI : l'énumération prend quelques dizaines de ms).</summary>
    public static IReadOnlyList<ProcessEntry> Snapshot()
    {
        var byName = new Dictionary<string, ProcessEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero || p.Id == Environment.ProcessId) continue;
                var name = p.ProcessName.ToLowerInvariant() + ".exe";
                if (byName.ContainsKey(name)) continue;
                byName[name] = new ProcessEntry(name, p.MainWindowTitle, IconFor(name, p));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // process terminé entre-temps ou accès refusé : on ignore
            }
            finally
            {
                p.Dispose();
            }
        }
        return byName.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ImageSource? IconFor(string name, Process p)
    {
        lock (IconCache)
        {
            if (IconCache.TryGetValue(name, out var cached)) return cached;
        }
        ImageSource? icon = null;
        try
        {
            // MainModule échoue pour un process plus privilégié (admin) : pas d'icône, ce n'est pas grave
            var path = p.MainModule?.FileName;
            if (path is not null)
            {
                using var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (ico is not null)
                {
                    var src = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    src.Freeze(); // utilisable depuis le thread UI
                    icon = src;
                }
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        {
        }
        lock (IconCache) IconCache[name] = icon;
        return icon;
    }
}
