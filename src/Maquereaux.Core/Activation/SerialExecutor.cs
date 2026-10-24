using System.Collections.Concurrent;

namespace Maquereaux.Core.Activation;

/// <summary>
/// Exécute des actions une par une, dans l'ordre, sur un thread dédié.
/// <para>
/// Pourquoi : les hotkeys et le hook clavier arrivent sur le thread de la boucle de messages Win32.
/// Ce thread doit rendre la main TRÈS vite (sinon Windows retire le hook). On y poste donc juste
/// « fais Start/Stop » et c'est ce thread-ci qui fait le travail (qui peut attendre l'arrêt du moteur).
/// </para>
/// </summary>
public sealed class SerialExecutor : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public SerialExecutor(string name = "Maquereaux.Commands")
    {
        _thread = new Thread(Loop) { Name = name, IsBackground = true, Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    /// <summary>Exception levée par une action (l'exécuteur, lui, continue de tourner).</summary>
    public event Action<Exception>? ActionFailed;

    /// <summary>Ajoute une action à la file (non bloquant). Ignoré après Dispose.</summary>
    public void Post(Action action)
    {
        try { _queue.Add(action); }
        catch (InvalidOperationException) { /* file fermée : on s'arrête */ }
    }

    /// <summary>Exécute l'action et attend sa fin (utile pour les tests et l'arrêt de l'appli).</summary>
    public void Invoke(Action action, TimeSpan? timeout = null)
    {
        using var done = new ManualResetEventSlim();
        Exception? error = null;
        Post(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        });
        if (!done.Wait(timeout ?? TimeSpan.FromSeconds(5)))
            throw new TimeoutException("SerialExecutor : délai dépassé.");
        if (error is not null) throw new AggregateException(error);
    }

    private void Loop()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try { action(); }
            catch (Exception ex) { ActionFailed?.Invoke(ex); }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        if (Thread.CurrentThread != _thread) _thread.Join(TimeSpan.FromSeconds(3));
    }
}
