using System.Collections.Concurrent;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

/// <summary>
/// Thread dédié avec sa propre boucle de messages Win32 (GetMessage).
/// <para>
/// Pourquoi un thread à part plutôt que le thread UI WPF : <c>RegisterHotKey</c> et le hook
/// <c>WH_KEYBOARD_LL</c> livrent leurs événements via la file de messages du thread qui les a installés.
/// Si ce thread est occupé (ex. l'UI qui redessine un gros graphe), le hook répond en retard et Windows
/// finit par le retirer (~1 s). Ici le thread ne fait QUE pomper des messages : réactivité maximale.
/// </para>
/// </summary>
public sealed class MessageLoopThread : IDisposable
{
    private const uint WM_INVOKE = NativeMethods.WM_APP + 1;

    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private uint _threadId;
    private volatile bool _disposed;

    public MessageLoopThread(string name = "Maquereaux.MessageLoop")
    {
        _thread = new Thread(Run) { Name = name, IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
        _ready.Wait();
    }

    /// <summary>Message reçu sur le thread (WM_HOTKEY…). Le gestionnaire doit être TRÈS rapide.</summary>
    internal event Action<MSG>? MessageReceived;

    public bool IsOnLoopThread => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;

    /// <summary>Exécute une fonction sur le thread de la boucle et attend son résultat.</summary>
    public T Invoke<T>(Func<T> func)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsOnLoopThread) return func();

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        if (!NativeMethods.PostThreadMessageW(_threadId, WM_INVOKE, 0, 0))
            throw new InvalidOperationException("Impossible de poster un message à la boucle Win32.");
        if (!tcs.Task.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("La boucle de messages Win32 ne répond pas.");
        return tcs.Task.GetAwaiter().GetResult();
    }

    public void Invoke(Action action) => Invoke(() => { action(); return 0; });

    private void Run()
    {
        // Force la création de la file de messages du thread AVANT de signaler qu'on est prêt
        // (sinon un PostThreadMessage trop précoce serait perdu).
        NativeMethods.PeekMessageW(out _, IntPtr.Zero, NativeMethods.WM_USER, NativeMethods.WM_USER, NativeMethods.PM_NOREMOVE);
        _threadId = NativeMethods.GetCurrentThreadId();
        _ready.Set();

        int r;
        while ((r = NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0)) != 0)
        {
            if (r == -1) break;
            if (msg.hwnd == IntPtr.Zero && msg.message == WM_INVOKE)
            {
                while (_pending.TryDequeue(out var action)) action();
                continue;
            }
            try { MessageReceived?.Invoke(msg); }
            catch { /* un abonné ne doit jamais tuer la boucle */ }
            NativeMethods.TranslateMessage(msg);
            NativeMethods.DispatchMessageW(msg);
        }
        // Exécute ce qui reste (ex. nettoyage des hooks demandé juste avant WM_QUIT)
        while (_pending.TryDequeue(out var last)) last();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NativeMethods.PostThreadMessageW(_threadId, NativeMethods.WM_QUIT, 0, 0);
        if (!IsOnLoopThread) _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }
}
