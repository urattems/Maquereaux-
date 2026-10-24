using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Maquereaux.App.Services;
using Maquereaux.Core.Models;
using Maquereaux.Win32;

namespace Maquereaux.App.Controls;

/// <summary>
/// Champ « clique puis appuie sur une touche ». Une fois la touche capturée : met à jour <see cref="Key"/>
/// (liaison two-way) et exécute <see cref="KeyCapturedCommand"/> avec la <see cref="KeyStroke"/> en paramètre.
/// Échap annule.
/// </summary>
public class KeyCaptureBox : TextBox
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.Register(
        nameof(Key), typeof(KeyStroke), typeof(KeyCaptureBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((KeyCaptureBox)d).Refresh()));

    public static readonly DependencyProperty KeyCapturedCommandProperty = DependencyProperty.Register(
        nameof(KeyCapturedCommand), typeof(ICommand), typeof(KeyCaptureBox));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(KeyCaptureBox), new PropertyMetadata("Clique puis appuie sur une touche", (d, _) => ((KeyCaptureBox)d).Refresh()));

    private bool _capturing;

    public KeyCaptureBox()
    {
        // Les styles implicites WPF ne s'appliquent qu'au type EXACT : on reprend explicitement celui des TextBox
        SetResourceReference(StyleProperty, typeof(TextBox));
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        IsUndoEnabled = false;
        ContextMenu = null;
        Loaded += (_, _) => Refresh();
    }

    public KeyStroke? Key
    {
        get => (KeyStroke?)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public ICommand? KeyCapturedCommand
    {
        get => (ICommand?)GetValue(KeyCapturedCommandProperty);
        set => SetValue(KeyCapturedCommandProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (_capturing) return;
        _capturing = true;
        KeyCaptureCoordinator.Begin();
        Text = "Appuie sur une touche… (Échap = annuler)";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        StopCapture();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_capturing) { base.OnPreviewKeyDown(e); return; }
        e.Handled = true;

        var key = KeyCaptureHelper.RealKey(e);
        if (key == System.Windows.Input.Key.Escape) { EndCapture(); return; }

        var vk = KeyCaptureHelper.ToVirtualKey(key);
        if (vk == 0) return;

        // Win32 : scan code + nom localisé selon la disposition clavier (AZERTY…)
        var stroke = KeyboardLayout.Describe(vk);
        if (KeyCapturedCommand is { } cmd)
        {
            if (cmd.CanExecute(stroke)) cmd.Execute(stroke);
        }
        else
        {
            Key = stroke;
        }
        EndCapture();
    }

    // Empêche le menu Alt/F10 de la fenêtre de voler le focus pendant la capture
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_capturing) e.Handled = true;
        base.OnPreviewKeyUp(e);
    }

    private void EndCapture()
    {
        StopCapture();
        // Rend le focus à la fenêtre (sinon le champ resterait en mode capture)
        var scope = FocusManager.GetFocusScope(this);
        FocusManager.SetFocusedElement(scope, null);
        Keyboard.ClearFocus();
    }

    private void StopCapture()
    {
        if (!_capturing) return;
        _capturing = false;
        KeyCaptureCoordinator.End();
        Refresh();
    }

    private void Refresh()
    {
        if (_capturing) return;
        Text = KeyCapturedCommand is not null || Key is null ? Placeholder : Key.DisplayName;
    }
}
