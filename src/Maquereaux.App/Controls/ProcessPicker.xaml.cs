using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Maquereaux.App.Services;

namespace Maquereaux.App.Controls;

/// <summary>
/// Sélecteur de fenêtre cible : liste des applis ouvertes (icône, nom d'exe, titre de fenêtre), recherche instantanée,
/// saisie libre d'un nom d'exe (Entrée) pour une appli pas encore lancée, et « Toutes les fenêtres ».
/// </summary>
public partial class ProcessPicker : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(ProcessPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ProcessPicker)d).UpdateCurrent()));

    /// <summary>Nom de l'exe cible (« notepad.exe »), null = toutes les fenêtres.</summary>
    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private IReadOnlyList<ProcessEntry> _all = Array.Empty<ProcessEntry>();

    public ProcessPicker()
    {
        InitializeComponent();
        Popup.Opened += async (_, _) => await OpenAsync();
        Search.TextChanged += (_, _) => ApplyFilter();
        Search.PreviewKeyDown += OnSearchKey;
        List.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(List, (DependencyObject)e.OriginalSource) is ListBoxItem { Content: ProcessEntry entry })
                Pick(entry.Name);
        };
        List.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && List.SelectedItem is ProcessEntry entry) { Pick(entry.Name); e.Handled = true; }
        };
        AnyWindow.Click += (_, _) => Pick(null);
        UpdateCurrent();
    }

    private async Task OpenAsync()
    {
        Search.Text = "";
        Loading.Visibility = Visibility.Visible;
        NoResult.Visibility = Visibility.Collapsed;
        List.ItemsSource = null;
        Search.Focus();
        _all = await Task.Run(ProcessCatalog.Snapshot);
        Loading.Visibility = Visibility.Collapsed;
        ApplyFilter();
        UpdateCurrent();
    }

    private void ApplyFilter()
    {
        if (Loading.Visibility == Visibility.Visible) return;
        var query = Search.Text.Trim();
        var items = _all.Where(e => e.Matches(query)).ToList();
        List.ItemsSource = items;
        List.SelectedItem = items.FirstOrDefault(e => string.Equals(e.Name, Value, StringComparison.OrdinalIgnoreCase))
                            ?? (query.Length > 0 ? items.FirstOrDefault() : null);
        NoResult.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoResultHint.Text = query.Length > 0 ? $"Entrée pour cibler « {Normalize(query)} » quand même" : "Aucune appli avec une fenêtre visible";
    }

    private void OnSearchKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when List.Items.Count > 0:
                List.SelectedIndex = Math.Min(List.SelectedIndex + 1, List.Items.Count - 1);
                List.ScrollIntoView(List.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up when List.Items.Count > 0:
                List.SelectedIndex = Math.Max(List.SelectedIndex - 1, 0);
                List.ScrollIntoView(List.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter:
                if (List.SelectedItem is ProcessEntry entry) Pick(entry.Name);
                else if (Search.Text.Trim() is { Length: > 0 } typed) Pick(Normalize(typed));
                e.Handled = true;
                break;
            case Key.Escape:
                Toggle.IsChecked = false;
                e.Handled = true;
                break;
        }
    }

    private static string Normalize(string typed) =>
        typed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? typed.ToLowerInvariant() : typed.ToLowerInvariant() + ".exe";

    private void Pick(string? name)
    {
        Value = name;
        Toggle.IsChecked = false;
        Toggle.Focus();
    }

    private void UpdateCurrent()
    {
        var value = Value;
        CurrentText.Text = value ?? "Toutes les fenêtres";
        var icon = value is null ? null : _all.FirstOrDefault(e => string.Equals(e.Name, value, StringComparison.OrdinalIgnoreCase))?.Icon;
        CurrentIcon.Source = icon;
        CurrentGlyph.Visibility = icon is null ? Visibility.Visible : Visibility.Collapsed;
    }
}
