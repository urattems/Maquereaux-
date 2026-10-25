using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Maquereaux.App.Services;
using Maquereaux.Core.Models;

namespace Maquereaux.App.Controls;

/// <summary>
/// Champ de saisie d'un raccourci global : clique, puis appuie sur la combinaison (ex. Ctrl+Alt+F8).
/// Échap annule ; Retour arrière / Suppr (sans modificateur) efface si <see cref="AllowNone"/>.
/// Avec <see cref="AllowMouse"/>, un clic (gauche, droit, molette, boutons 4/5) pendant la capture devient le
/// raccourci : le premier clic gauche donne le focus, le suivant est capturé.
/// </summary>
public class HotkeyBox : TextBox
{
    public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
        nameof(Hotkey), typeof(Hotkey), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).Refresh()));

    public static readonly DependencyProperty AllowNoneProperty = DependencyProperty.Register(
        nameof(AllowNone), typeof(bool), typeof(HotkeyBox), new PropertyMetadata(true));

    public static readonly DependencyProperty AllowMouseProperty = DependencyProperty.Register(
        nameof(AllowMouse), typeof(bool), typeof(HotkeyBox), new PropertyMetadata(false));

    private bool _capturing;

    public HotkeyBox()
    {
        // Les styles implicites WPF ne s'appliquent qu'au type EXACT : on reprend explicitement celui des TextBox
        SetResourceReference(StyleProperty, typeof(TextBox));
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = Cursors.Hand;
        ContextMenu = null;
        Loaded += (_, _) => Refresh();
    }

    public Hotkey? Hotkey
    {
        get => (Hotkey?)GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public bool AllowNone
    {
        get => (bool)GetValue(AllowNoneProperty);
        set => SetValue(AllowNoneProperty, value);
    }

    /// <summary>Accepte un bouton de souris comme raccourci (profils en mode « Maintenir »).</summary>
    public bool AllowMouse
    {
        get => (bool)GetValue(AllowMouseProperty);
        set => SetValue(AllowMouseProperty, value);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        // Pas encore en capture : ce clic sert juste à donner le focus (comportement normal)
        if (!_capturing || !AllowMouse) { base.OnPreviewMouseDown(e); return; }
        ushort vk = e.ChangedButton switch
        {
            MouseButton.Left => Core.Input.VirtualKeys.LButton,
            MouseButton.Right => Core.Input.VirtualKeys.RButton,
            MouseButton.Middle => Core.Input.VirtualKeys.MButton,
            MouseButton.XButton1 => Core.Input.VirtualKeys.XButton1,
            MouseButton.XButton2 => Core.Input.VirtualKeys.XButton2,
            _ => 0,
        };
        if (vk == 0) return;
        e.Handled = true;
        Hotkey = new Hotkey(ToModifiers(Keyboard.Modifiers), vk);
        EndCapture();
    }

    // Le relâchement du clic capturé ne doit pas ouvrir de menu ni redonner le focus
    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        if (AllowMouse && e.ChangedButton != MouseButton.Left) e.Handled = true;
        base.OnPreviewMouseUp(e);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (_capturing) return;
        _capturing = true;
        KeyCaptureCoordinator.Begin();
        Text = AllowMouse ? "Touche ou clic…" : "Appuie sur la combinaison…";
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
        var mods = ToModifiers(Keyboard.Modifiers);

        if (key == Key.Escape && mods == HotkeyModifiers.None) { EndCapture(); return; }
        if ((key is Key.Back or Key.Delete) && mods == HotkeyModifiers.None && AllowNone)
        {
            Hotkey = null;
            EndCapture();
            return;
        }
        if (KeyCaptureHelper.IsModifier(key))
        {
            // Affiche la combinaison en cours de construction (ex. "Ctrl+Alt+…")
            Text = new Hotkey(mods, 0).ToString().Replace("VK 0x00", "…");
            return;
        }

        var vk = KeyCaptureHelper.ToVirtualKey(key);
        if (vk == 0) return;
        Hotkey = new Hotkey(mods, vk);
        EndCapture();
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_capturing) e.Handled = true;
        base.OnPreviewKeyUp(e);
    }

    // ModifierKeys WPF et MOD_* Win32 ont les mêmes valeurs (Alt=1, Ctrl=2, Shift=4, Win=8)
    private static HotkeyModifiers ToModifiers(ModifierKeys m) => (HotkeyModifiers)(int)m;

    private void EndCapture()
    {
        StopCapture();
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
        Text = Hotkey?.ToString() ?? "(aucun)";
    }
}
