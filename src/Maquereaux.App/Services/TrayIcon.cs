using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Maquereaux.App.ViewModels;
using Maquereaux.Core.Engine;
using WinForms = System.Windows.Forms;

namespace Maquereaux.App.Services;

/// <summary>
/// Icône dans la zone de notification (à côté de l'horloge) : pastille verte/orange/rouge selon l'état,
/// menu avec profil actif, Start/Stop, Afficher, Quitter.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly Action _show;
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _profileItem;
    private readonly WinForms.ToolStripMenuItem _startStopItem;
    private readonly Icon _baseIcon;
    private readonly Dictionary<EngineState, Icon> _stateIcons = new();

    public TrayIcon(MainViewModel vm, Action show, Action quit)
    {
        _vm = vm;
        _show = show;
        _baseIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application;

        _profileItem = new WinForms.ToolStripMenuItem { Enabled = false };
        _startStopItem = new WinForms.ToolStripMenuItem("Démarrer", null, (_, _) => _vm.StartStopCommand.Execute(null));
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(_profileItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_startStopItem);
        menu.Items.Add(new WinForms.ToolStripMenuItem("Arrêt d'urgence", null, (_, _) => _vm.EmergencyStopCommand.Execute(null)));
        menu.Items.Add(new WinForms.ToolStripMenuItem("Afficher Maquereaux", null, (_, _) => _show()));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Quitter", null, (_, _) => quit()));

        _icon = new WinForms.NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => _show();
        _vm.PropertyChanged += OnVmChanged;
        Update();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.EngineState) or nameof(MainViewModel.ActiveProfile) or nameof(MainViewModel.RunningProfileName))
            Update();
    }

    private void Update()
    {
        var state = _vm.EngineState;
        var profile = _vm.RunningProfileName ?? _vm.ActiveProfile?.Name ?? "—";
        var stateText = state switch { EngineState.Running => "en marche", EngineState.Paused => "en pause", _ => "à l'arrêt" };
        // Le texte d'infobulle est limité à 63 caractères par Windows
        var tip = $"Maquereaux — {stateText} — {profile}";
        _icon.Text = tip.Length > 63 ? tip[..63] : tip;
        _icon.Icon = GetStateIcon(state);
        _profileItem.Text = $"Profil : {profile}";
        _startStopItem.Text = _vm.IsRunning ? "Arrêter" : "Démarrer";
    }

    /// <summary>Icône de l'appli + pastille de couleur en bas à droite (générée une fois par état).</summary>
    private Icon GetStateIcon(EngineState state)
    {
        if (_stateIcons.TryGetValue(state, out var cached)) return cached;
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawIcon(new Icon(_baseIcon, 32, 32), new Rectangle(0, 0, 32, 32));
            var color = state switch
            {
                // Couleurs d'état du design system : turquoise = en marche, ambre = en pause, neutre = à l'arrêt
                EngineState.Running => Color.FromArgb(0x33, 0xE8, 0xD0),
                EngineState.Paused => Color.FromArgb(0xF2, 0xB2, 0x4A),
                _ => Color.FromArgb(0x7D, 0x8F, 0xA6),
            };
            using var brush = new SolidBrush(color);
            using var pen = new Pen(Color.FromArgb(0x0A, 0x12, 0x20), 2);
            g.FillEllipse(brush, 17, 17, 14, 14);
            g.DrawEllipse(pen, 17, 17, 14, 14);
        }
        var hIcon = bmp.GetHicon();
        // Icon.FromHandle ne possède pas le handle : on clone puis on libère le HICON natif
        using var tmp = Icon.FromHandle(hIcon);
        var icon = (Icon)tmp.Clone();
        DestroyIcon(hIcon);
        _stateIcons[state] = icon;
        return icon;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void ShowBalloon(string title, string text)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = text;
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _vm.PropertyChanged -= OnVmChanged;
        _icon.Visible = false;
        _icon.Dispose();
        foreach (var i in _stateIcons.Values) i.Dispose();
    }
}
