using System.Runtime.InteropServices;
using Maquereaux.Core.Models;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

public enum HotkeyRegistrationStatus
{
    Success,
    /// <summary>Déjà pris par une autre appli (ou par Maquereaux lui-même).</summary>
    AlreadyInUse,
    /// <summary>Raccourci invalide (pas de touche, ou modificateur seul).</summary>
    Invalid,
    Error,
}

public sealed record HotkeyRegistrationResult(HotkeyRegistrationStatus Status, int Win32Error = 0)
{
    public bool Success => Status == HotkeyRegistrationStatus.Success;

    /// <summary>Message clair pour l'utilisateur.</summary>
    public string Describe(Hotkey hotkey) => Status switch
    {
        HotkeyRegistrationStatus.Success => $"{hotkey} : OK",
        HotkeyRegistrationStatus.AlreadyInUse => $"{hotkey} est déjà utilisé par une autre application. Choisis un autre raccourci.",
        HotkeyRegistrationStatus.Invalid => $"{hotkey} n'est pas un raccourci valide (il faut une touche principale, pas seulement Ctrl/Alt/Shift).",
        _ => $"{hotkey} : enregistrement impossible (erreur Win32 {Win32Error}).",
    };
}

/// <summary>
/// Raccourcis globaux via <c>RegisterHotKey</c> : simple, fiable, et Windows nous dit si la combinaison
/// est déjà prise (conflit). Le message WM_HOTKEY arrive sur le <see cref="MessageLoopThread"/>.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private readonly MessageLoopThread _loop;
    private readonly HashSet<int> _registered = new();
    private readonly object _lock = new();

    public GlobalHotkeyManager(MessageLoopThread loop)
    {
        _loop = loop;
        _loop.MessageReceived += OnMessage;
    }

    /// <summary>Raccourci déclenché (id fourni à <see cref="Register"/>). Levé sur le thread de la boucle : rester bref.</summary>
    public event Action<int>? Pressed;

    public HotkeyRegistrationResult Register(int id, Hotkey hotkey)
    {
        if (!hotkey.IsValid) return new(HotkeyRegistrationStatus.Invalid);
        return _loop.Invoke(() =>
        {
            // MOD_NOREPEAT : maintenir la touche n'envoie pas une rafale de WM_HOTKEY
            var mods = (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT;
            if (NativeMethods.RegisterHotKey(IntPtr.Zero, id, mods, hotkey.VirtualKey))
            {
                lock (_lock) _registered.Add(id);
                return new HotkeyRegistrationResult(HotkeyRegistrationStatus.Success);
            }
            var err = Marshal.GetLastPInvokeError();
            return err == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? new HotkeyRegistrationResult(HotkeyRegistrationStatus.AlreadyInUse, err)
                : new HotkeyRegistrationResult(HotkeyRegistrationStatus.Error, err);
        });
    }

    public void Unregister(int id)
    {
        _loop.Invoke(() =>
        {
            NativeMethods.UnregisterHotKey(IntPtr.Zero, id);
            lock (_lock) _registered.Remove(id);
        });
    }

    public void UnregisterAll()
    {
        int[] ids;
        lock (_lock) ids = _registered.ToArray();
        foreach (var id in ids) Unregister(id);
    }

    public IReadOnlyCollection<int> RegisteredIds
    {
        get { lock (_lock) return _registered.ToArray(); }
    }

    private void OnMessage(MSG msg)
    {
        if (msg.message == NativeMethods.WM_HOTKEY)
            Pressed?.Invoke((int)msg.wParam);
    }

    public void Dispose()
    {
        try { UnregisterAll(); } catch { /* boucle déjà arrêtée */ }
        _loop.MessageReceived -= OnMessage;
    }
}
