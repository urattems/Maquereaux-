using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

/// <summary>
/// Hook clavier bas niveau (<c>WH_KEYBOARD_LL</c>) : voit TOUTES les frappes du système, physiques et injectées.
/// <para>
/// Règle d'or : le callback doit être ultra léger. Windows attend sa réponse avant de livrer la frappe aux
/// applis ; au-delà de ~1 s (LowLevelHooksTimeout), il retire le hook silencieusement. Ici le callback :
/// horodate, appelle un filtre synchrone minuscule (décision « avaler ? »), pousse l'événement dans une file,
/// et c'est tout. Le traitement se fait sur un autre thread.
/// </para>
/// <para>
/// Vie privée : le hook n'est installé QUE si nécessaire (profil en mode Maintenir, ou session du Labo en cours).
/// </para>
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private readonly MessageLoopThread _loop;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // gardé en champ : sinon le GC collecte le délégué → crash
    private readonly BlockingCollection<RawKeyEvent> _queue = new(boundedCapacity: 200_000);
    private readonly Thread _dispatcher;
    private IntPtr _hook;
    private volatile bool _disposed;

    public KeyboardHook(MessageLoopThread loop)
    {
        _loop = loop;
        _proc = HookProc;
        _dispatcher = new Thread(DispatchLoop) { Name = "Maquereaux.HookEvents", IsBackground = true, Priority = ThreadPriority.AboveNormal };
        _dispatcher.Start();
    }

    /// <summary>
    /// Décision synchrone appelée DANS le callback du hook (thread de la boucle) : true = avaler la frappe
    /// (elle n'atteindra pas les applis). Doit être quasi instantanée.
    /// </summary>
    public Func<RawKeyEvent, bool>? Filter { get; set; }

    /// <summary>Événements clavier, levés sur un thread dédié (jamais dans le callback du hook).</summary>
    public event Action<RawKeyEvent>? KeyEvent;

    public bool IsInstalled => _hook != IntPtr.Zero;

    /// <summary>Événements perdus (file pleine — ne devrait jamais arriver).</summary>
    public long DroppedEvents { get; private set; }

    public bool Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _loop.Invoke(() =>
        {
            if (_hook != IntPtr.Zero) return true;
            _hook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle("user32.dll"), 0);
            return _hook != IntPtr.Zero;
        });
    }

    public void Uninstall()
    {
        if (_disposed) return;
        _loop.Invoke(() =>
        {
            if (_hook == IntPtr.Zero) return;
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        });
    }

    private unsafe nint HookProc(int nCode, nuint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var now = StopwatchClock.Instance.NowMs; // horodatage haute résolution, au plus tôt
            var k = (KBDLLHOOKSTRUCT*)lParam;
            uint msg = (uint)wParam;
            bool down = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            var e = new RawKeyEvent(
                now,
                (ushort)k->vkCode,
                (ushort)k->scanCode,
                down,
                (k->flags & NativeMethods.LLKHF_INJECTED) != 0,
                k->dwExtraInfo == SendInputSender.ExtraInfoSignature,
                (k->flags & NativeMethods.LLKHF_EXTENDED) != 0);

            bool swallow = false;
            try { swallow = Filter?.Invoke(e) ?? false; }
            catch { /* jamais d'exception qui remonte dans Windows */ }

            if (KeyEvent is not null && !_queue.TryAdd(e)) DroppedEvents++;
            if (swallow) return 1;
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void DispatchLoop()
    {
        foreach (var e in _queue.GetConsumingEnumerable())
        {
            try { KeyEvent?.Invoke(e); }
            catch { /* un abonné ne doit pas tuer le thread */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { Uninstall(); } catch { /* boucle déjà arrêtée */ }
        _disposed = true;
        _queue.CompleteAdding();
        _dispatcher.Join(TimeSpan.FromSeconds(1));
    }
}
