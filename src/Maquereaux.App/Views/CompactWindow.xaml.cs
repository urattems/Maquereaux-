using System.Windows;
using System.Windows.Input;

namespace Maquereaux.App.Views;

public partial class CompactWindow : Window
{
    public CompactWindow()
    {
        InitializeComponent();
    }

    // Purement visuel (déplacement de la fenêtre sans bordure) : sa place est bien dans le code-behind
    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
