using Maquereaux.Core.Engine;
using Maquereaux.Core.Input;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

/// <summary>
/// Hook souris bas niveau (<c>WH_MOUSE_LL</c>), limité aux BOUTONS : sert aux raccourcis « Maintenir » sur un clic
/// (ex. la macro tourne tant que le clic droit est enfoncé).
/// <list type="bullet">
/// <item>Jamais aucun clic avalé : le jeu / l'appli reçoit toujours le clic normalement.</item>
/// <item>Déplacements et molette ignorés dès la première ligne du callback (coût quasi nul).</item>
/// <item>Les boutons sont traduits en <see cref="RawKeyEvent"/> (VK 0x01 clic gauche, 0x02 clic droit, 0x04 molette,
/// 0x05/0x06 boutons latéraux) pour réutiliser la même logique que le clavier.</item>
/// <item>Un appui sur une fenêtre de Maquereaux (celle sous le curseur) est ignoré : sinon cliquer dans l'appli lancerait la macro ;
/// les relâchements passent toujours (un clic commencé dans le jeu s'arrête même si on relâche ailleurs).</item>
/// </list>
/// Installé seulement si un profil « Maintenir » utilise un bouton de souris.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private readonly MessageLoopThread _loop;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // gardé en champ : sinon le GC collecte le délégué → crash
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private IntPtr _hook;
    private bool _disposed;

    public MouseHook(MessageLoopThread loop)
    {
        _loop = loop;
        _proc = HookProc;
    }

    /// <summary>Appelé DANS le callback (thread de la boucle Win32) pour chaque appui/relâchement de bouton. Doit être instantané.</summary>
    public Action<RawKeyEvent>? Button { get; set; }

    public bool IsInstalled => _hook != IntPtr.Zero;

    public bool Install()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _loop.Invoke(() =>
        {
            if (_hook != IntPtr.Zero) return true;
            _hook = NativeMethods.SetWindowsHookExW(NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle("user32.dll"), 0);
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

    /// <summary>Message souris → (VK, appui ?). VK 0 = pas un bouton (déplacement, molette…).</summary>
    internal static (ushort Vk, bool Down) Translate(uint msg, uint mouseData) => msg switch
    {
        NativeMethods.WM_LBUTTONDOWN => (VirtualKeys.LButton, true),
        NativeMethods.WM_LBUTTONUP => (VirtualKeys.LButton, false),
        NativeMethods.WM_RBUTTONDOWN => (VirtualKeys.RButton, true),
        NativeMethods.WM_RBUTTONUP => (VirtualKeys.RButton, false),
        NativeMethods.WM_MBUTTONDOWN => (VirtualKeys.MButton, true),
        NativeMethods.WM_MBUTTONUP => (VirtualKeys.MButton, false),
        // Boutons latéraux : le mot haut de mouseData vaut 1 (XBUTTON1) ou 2 (XBUTTON2)
        NativeMethods.WM_XBUTTONDOWN => ((mouseData >> 16) == 2 ? VirtualKeys.XButton2 : VirtualKeys.XButton1, true),
        NativeMethods.WM_XBUTTONUP => ((mouseData >> 16) == 2 ? VirtualKeys.XButton2 : VirtualKeys.XButton1, false),
        _ => ((ushort)0, false),
    };

    private unsafe nint HookProc(int nCode, nuint wParam, nint lParam)
    {
        if (nCode >= 0 && Button is { } handler)
        {
            var m = (MSLLHOOKSTRUCT*)lParam;
            var (vk, down) = Translate((uint)wParam, m->mouseData);
            if (vk != 0 && !(down && IsOwnWindowAt(m->pt)))
            {
                var e = new RawKeyEvent(StopwatchClock.Instance.NowMs, vk, 0, down,
                    (m->flags & NativeMethods.LLMHF_INJECTED) != 0, m->dwExtraInfo == SendInputSender.ExtraInfoSignature);
                try { handler(e); }
                catch { /* jamais d'exception qui remonte dans Windows */ }
            }
        }
        // Toujours transmis : on n'avale jamais un clic
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private bool IsOwnWindowAt(POINT pt)
    {
        var hwnd = NativeMethods.WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == _ownPid;
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { Uninstall(); } catch { /* boucle déjà arrêtée */ }
        _disposed = true;
    }
}
