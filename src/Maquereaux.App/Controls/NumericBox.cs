using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Maquereaux.App.Controls;

/// <summary>
/// Champ numérique entier : flèches ▲▼, molette (Maj = ×10), touches Haut/Bas, validation à Entrée / perte de focus.
/// Une saisie invalide est refusée (bordure corail une seconde) et la dernière valeur valide est rétablie :
/// impossible d'envoyer « -5 » ou « abc » au moteur.
/// <para>
/// <see cref="AllowEmpty"/> : la valeur peut être vide (null) — utilisé pour « délai = intervalle global ».
/// </para>
/// </summary>
[TemplatePart(Name = PartText, Type = typeof(TextBox))]
[TemplatePart(Name = PartUp, Type = typeof(ButtonBase))]
[TemplatePart(Name = PartDown, Type = typeof(ButtonBase))]
public sealed class NumericBox : Control
{
    private const string PartText = "PART_Text";
    private const string PartUp = "PART_Up";
    private const string PartDown = "PART_Down";

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int?), typeof(NumericBox),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(NumericBox), new PropertyMetadata(0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(NumericBox), new PropertyMetadata(int.MaxValue));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(int), typeof(NumericBox), new PropertyMetadata(1));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(NumericBox), new PropertyMetadata(null));

    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(
        nameof(AllowEmpty), typeof(bool), typeof(NumericBox), new PropertyMetadata(false));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(NumericBox), new PropertyMetadata("auto"));

    private static readonly DependencyPropertyKey IsInvalidPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsInvalid), typeof(bool), typeof(NumericBox), new PropertyMetadata(false));

    public static readonly DependencyProperty IsInvalidProperty = IsInvalidPropertyKey.DependencyProperty;

    private TextBox? _text;
    private System.Windows.Threading.DispatcherTimer? _errorTimer;

    static NumericBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NumericBox), new FrameworkPropertyMetadata(typeof(NumericBox)));
        FocusableProperty.OverrideMetadata(typeof(NumericBox), new FrameworkPropertyMetadata(false));
    }

    public int? Value { get => (int?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string? Unit { get => (string?)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>Vrai brièvement après une saisie refusée (anime la bordure en corail).</summary>
    public bool IsInvalid => (bool)GetValue(IsInvalidProperty);

    public override void OnApplyTemplate()
    {
        if (_text is not null)
        {
            _text.LostKeyboardFocus -= OnTextLostFocus;
            _text.PreviewKeyDown -= OnTextKeyDown;
        }
        base.OnApplyTemplate();
        _text = GetTemplateChild(PartText) as TextBox;
        if (_text is not null)
        {
            _text.LostKeyboardFocus += OnTextLostFocus;
            _text.PreviewKeyDown += OnTextKeyDown;
            UpdateText();
        }
        if (GetTemplateChild(PartUp) is ButtonBase up) up.Click += (_, _) => Increment(Step);
        if (GetTemplateChild(PartDown) is ButtonBase down) down.Click += (_, _) => Increment(-Step);
    }

    /// <summary>Ajoute <paramref name="delta"/> (borné). Public pour les tests et les raccourcis.</summary>
    public void Increment(int delta)
    {
        var current = Value ?? Math.Clamp(0, Minimum, Maximum);
        Value = (int)Math.Clamp((long)current + delta, Minimum, Maximum);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!IsEnabled || _text is null || !_text.IsKeyboardFocusWithin) return; // pas de changement involontaire en faisant défiler la page
        int factor = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        Increment(Math.Sign(e.Delta) * Step * factor);
        e.Handled = true;
    }

    private void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        int factor = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Enter:
                Commit();
                _text?.SelectAll();
                e.Handled = true;
                break;
            case Key.Escape:
                UpdateText();
                e.Handled = true;
                break;
            case Key.Up:
                Commit();
                Increment(Step * factor);
                e.Handled = true;
                break;
            case Key.Down:
                Commit();
                Increment(-Step * factor);
                e.Handled = true;
                break;
        }
    }

    private void OnTextLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();

    /// <summary>Valide le texte saisi : nombre entier dans les bornes, ou vide si autorisé.</summary>
    public void Commit()
    {
        if (_text is null) return;
        var raw = _text.Text.Trim();
        if (TryParse(raw, AllowEmpty, Minimum, Maximum, out var parsed))
        {
            Value = parsed;
            UpdateText();
        }
        else
        {
            FlashError();
            UpdateText();
        }
    }

    /// <summary>Analyse une saisie (logique pure, testée unitairement).</summary>
    public static bool TryParse(string raw, bool allowEmpty, int min, int max, out int? value)
    {
        value = null;
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return allowEmpty;
        // Tolère « 16 ms », « 1 000 » et les espaces insécables
        var digits = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != ' ' && c != ' ').ToArray());
        if (digits.EndsWith("ms", StringComparison.OrdinalIgnoreCase)) digits = digits[..^2];
        if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return false;
        if (v < min || v > max) return false;
        value = v;
        return true;
    }

    private void FlashError()
    {
        SetValue(IsInvalidPropertyKey, true);
        _errorTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _errorTimer.Stop();
        _errorTimer.Tick += Clear;
        _errorTimer.Start();

        void Clear(object? s, EventArgs a)
        {
            _errorTimer!.Stop();
            _errorTimer.Tick -= Clear;
            SetValue(IsInvalidPropertyKey, false);
        }
    }

    private static object? CoerceValue(DependencyObject d, object? baseValue)
    {
        var box = (NumericBox)d;
        if (baseValue is int v) return Math.Clamp(v, box.Minimum, Math.Max(box.Minimum, box.Maximum));
        return box.AllowEmpty ? null : box.Minimum;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NumericBox)d).UpdateText();

    private void UpdateText()
    {
        if (_text is null) return;
        _text.Text = Value?.ToString(CultureInfo.CurrentCulture) ?? "";
    }
}
