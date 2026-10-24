using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Maquereaux.Core.Engine;

namespace Maquereaux.App.Converters;

/// <summary>État moteur → ressource (brosse) : Running → vert, Paused → orange, Stopped → rouge.</summary>
public sealed class EngineStateToBrushConverter : IValueConverter
{
    /// <summary>Suffixe ajouté à la clé (ex. "Bg" → Brush.RunningBg).</summary>
    public string Suffix { get; set; } = "";

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is EngineState s ? s switch
        {
            EngineState.Running => "Brush.Running",
            EngineState.Paused => "Brush.Paused",
            _ => "Brush.Stopped",
        } : "Brush.Stopped";
        return Application.Current?.TryFindResource(key + Suffix);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>État moteur → texte affiché.</summary>
public sealed class EngineStateToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        EngineState.Running => "RUNNING",
        EngineState.Paused => "PAUSED",
        _ => "STOPPED",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Enum ↔ bool pour les RadioButton (ConverterParameter = nom de la valeur).</summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string s ? Enum.Parse(targetType, s) : Binding.DoNothing;
}

/// <summary>bool → Visibility (Invert = true pour inverser).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>null/chaîne vide → Collapsed (Invert = true pour inverser).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value is null || (value is string s && string.IsNullOrEmpty(s));
        return isNull ^ Invert ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool → !bool.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
