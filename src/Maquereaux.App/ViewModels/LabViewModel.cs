using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Maquereaux.App.Services;

namespace Maquereaux.App.ViewModels;

/// <summary>Onglet Labo de mesure (J4).</summary>
public sealed partial class LabViewModel : ObservableObject, IDisposable
{
    private readonly AppHost _host;
    private readonly Dispatcher _dispatcher;
    private readonly IDialogService _dialogs;
    private readonly MainViewModel _main;

    public LabViewModel(AppHost host, Dispatcher dispatcher, IDialogService dialogs, MainViewModel main)
    {
        _host = host;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _main = main;
    }

    public void Dispose()
    {
    }
}
