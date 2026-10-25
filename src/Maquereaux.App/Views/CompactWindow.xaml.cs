using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Maquereaux.App.ViewModels;

namespace Maquereaux.App.Views;

public partial class CompactWindow : Window
{
    public CompactWindow()
    {
        InitializeComponent();
    }

    // Purement visuel (déplacement, double-clic) : sa place est bien dans le code-behind
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            (DataContext as MainViewModel)?.ToggleCompactCommand.Execute(null);
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnOpacity(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && DataContext is MainViewModel vm)
            vm.CompactOpacity = double.Parse(tag, CultureInfo.InvariantCulture);
    }
}
