using System.Runtime.InteropServices;
using Maquereaux.Core.Input;
using Maquereaux.Core.Models;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

/// <summary>
/// Envoi réel des touches via <c>SendInput</c> en mode <b>scan code</b> (KEYEVENTF_SCANCODE).
/// <para>
/// Pourquoi les scan codes : beaucoup d'applis (surtout les jeux, via DirectInput/RawInput) lisent la
/// position physique de la touche et ignorent le VK. Le scan code est résolu depuis le VK avec la
/// disposition clavier de la fenêtre cible au démarrage (<see cref="Prepare"/>).
/// </para>
/// <para>
/// Chaque événement porte une signature dans <c>dwExtraInfo</c> : le labo de mesure peut ainsi distinguer
/// « injecté par Maquereaux » de « injecté par un autre logiciel ». C'est de l'identification, pas du masquage :
/// Windows marque toujours ces événements comme injectés (LLKHF_INJECTED), et c'est voulu.
/// </para>
/// </summary>
public sealed class SendInputSender : IInputSender
{
    /// <summary>Signature "MQRX" placée dans dwExtraInfo de chaque événement envoyé.</summary>
    public const uint ExtraInfoSignature = 0x4D515258;

    private static readonly int InputSize = Marshal.SizeOf<INPUT>();
    private volatile Dictionary<ushort, (ushort Scan, bool Extended)> _resolved = new();
    private IntPtr _layout = IntPtr.Zero;
    private long _failedEvents;

    /// <summary>Nombre d'événements que SendInput a refusés (ex. bloqués par UIPI, bureau sécurisé…).</summary>
    public long FailedEvents => Interlocked.Read(ref _failedEvents);

    /// <summary>Méthode d'envoi (réglage global, lu à chaque envoi).</summary>
    public KeySendMethod Method { get; set; } = KeySendMethod.ScanCode;

    /// <summary>Dernier code d'erreur Win32 lors d'un échec de SendInput (0 si aucun).</summary>
    public int LastError { get; private set; }

    public void Prepare(IEnumerable<KeyStroke> keys)
    {
        _layout = KeyboardLayout.GetForegroundLayout();
        var map = new Dictionary<ushort, (ushort, bool)>();
        foreach (var k in keys) map[k.VirtualKey] = ResolveKey(k);
        _resolved = map; // remplacement atomique de la référence : pas de verrou côté Send
    }

    private (ushort Scan, bool Extended) ResolveKey(KeyStroke key)
    {
        var (scan, ext) = KeyboardLayout.Resolve(key.VirtualKey, _layout);
        // Repli sur le scan code mémorisé à la capture si la disposition courante n'en donne pas
        if (scan == 0 && key.ScanCode != 0 && key.VirtualKey is not (VirtualKeys.Pause or VirtualKeys.Cancel))
            return (key.ScanCode, key.IsExtended);
        return (scan, ext || key.IsExtended);
    }

    public unsafe void Send(IReadOnlyList<KeyAction> actions)
    {
        int n = actions.Count;
        if (n == 0) return;

        // stackalloc : pas d'allocation sur le tas à chaque tick (le GC ne viendra pas perturber le timing)
        INPUT* inputs = stackalloc INPUT[n];
        var resolved = _resolved;
        bool scanMode = Method == KeySendMethod.ScanCode;
        for (int i = 0; i < n; i++)
        {
            var a = actions[i];
            if (!resolved.TryGetValue(a.Key.VirtualKey, out var r)) r = ResolveKey(a.Key);
            inputs[i] = Build(a.Key.VirtualKey, r.Scan, r.Extended, a.IsDown, scanMode);
        }

        uint sent = NativeMethods.SendInput((uint)n, inputs, InputSize);
        if (sent != n)
        {
            LastError = Marshal.GetLastPInvokeError();
            Interlocked.Add(ref _failedEvents, n - sent);
        }
    }

    internal static INPUT Build(ushort vk, ushort scan, bool extended, bool down, bool scanMode = true)
    {
        uint flags = down ? 0 : NativeMethods.KEYEVENTF_KEYUP;
        if (extended) flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

        var ki = new KEYBDINPUT { dwExtraInfo = ExtraInfoSignature };
        if (!scanMode)
        {
            // Mode code virtuel : VK + scan code renseignés, sans KEYEVENTF_SCANCODE (Windows se fie au VK)
            ki.wVk = vk;
            ki.wScan = scan;
        }
        else if (scan != 0)
        {
            // Mode scan code : wVk est ignoré par Windows (on le laisse à 0)
            ki.wScan = scan;
            flags |= NativeMethods.KEYEVENTF_SCANCODE;
        }
        else
        {
            // Repli mode VK (touches sans scan code exploitable, ex. Pause)
            ki.wVk = vk;
        }
        ki.dwFlags = flags;
        return new INPUT { type = NativeMethods.INPUT_KEYBOARD, U = new InputUnion { ki = ki } };
    }
}
