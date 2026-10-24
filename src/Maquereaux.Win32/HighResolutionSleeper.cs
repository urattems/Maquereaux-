using Maquereaux.Core.Engine;
using Maquereaux.Win32.Native;
using Microsoft.Win32.SafeHandles;

namespace Maquereaux.Win32;

/// <summary>
/// Attente précise via un « waitable timer » noyau créé avec <c>CREATE_WAITABLE_TIMER_HIGH_RESOLUTION</c>
/// (Windows 10 1803+).
/// <para>
/// Pourquoi : par défaut, Windows ne réveille les threads qu'à chaque « tic » d'horloge (~15,6 ms).
/// Ce type de timer est réveillé à la précision de la centaine de microsecondes SANS changer la
/// résolution globale du système (contrairement à timeBeginPeriod qui impacte tout le PC et la batterie).
/// </para>
/// <para>
/// Repli si l'OS est trop ancien : timer classique + <c>timeBeginPeriod(1)</c>, avec
/// <c>timeEndPeriod(1)</c> garanti dans <see cref="Dispose"/> (le moteur dispose le sleeper dans un finally).
/// </para>
/// </summary>
public sealed class HighResolutionSleeper : ISleeper
{
    private readonly IClock _clock;
    private readonly IntPtr _timer;
    private readonly TimerWaitHandle _timerWait;
    private readonly WaitHandle[] _waitPair = new WaitHandle[2];
    private readonly bool _usesTimePeriod;
    private bool _disposed;

    public HighResolutionSleeper(IClock clock)
    {
        _clock = clock;
        _timer = NativeMethods.CreateWaitableTimerExW(IntPtr.Zero, null,
            NativeMethods.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, NativeMethods.TIMER_ALL_ACCESS);
        IsHighResolution = _timer != IntPtr.Zero;

        if (!IsHighResolution)
        {
            // Repli : résolution système à 1 ms le temps de l'exécution
            NativeMethods.timeBeginPeriod(1);
            _usesTimePeriod = true;
            _timer = NativeMethods.CreateWaitableTimerExW(IntPtr.Zero, null, 0, NativeMethods.TIMER_ALL_ACCESS);
            if (_timer == IntPtr.Zero)
            {
                NativeMethods.timeEndPeriod(1);
                throw new InvalidOperationException("Impossible de créer un waitable timer.");
            }
        }
        _timerWait = new TimerWaitHandle(_timer);
        _waitPair[0] = _timerWait;
    }

    /// <summary>Vrai si le timer haute résolution natif est utilisé (sinon repli timeBeginPeriod).</summary>
    public bool IsHighResolution { get; }

    public string Name => IsHighResolution ? "Haute résolution (waitable timer)" : "Haute résolution (repli timeBeginPeriod)";

    public bool SleepUntil(double targetMs, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _waitPair[1] = ct.WaitHandle;
        while (true)
        {
            if (ct.IsCancellationRequested) return false;
            double remaining = targetMs - _clock.NowMs;
            if (remaining <= 0) return true;

            // Échéance RELATIVE en unités de 100 ns (valeur négative = relative pour SetWaitableTimer)
            long due = -(long)(remaining * 10_000);
            if (due == 0) return true;
            if (!NativeMethods.SetWaitableTimer(_timer, due, 0, IntPtr.Zero, IntPtr.Zero, false))
                throw new InvalidOperationException("SetWaitableTimer a échoué.");

            // Attend le timer OU l'annulation (index 1) → arrêt immédiat même au milieu d'une longue attente
            if (WaitHandle.WaitAny(_waitPair) == 1) return false;
            // Boucle : en cas de réveil un poil en avance, on ré-arme pour le reste.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timerWait.Dispose();
        NativeMethods.CloseHandle(_timer);
        if (_usesTimePeriod) NativeMethods.timeEndPeriod(1);
    }

    /// <summary>Adapte un HANDLE natif en WaitHandle .NET (sans en prendre la propriété).</summary>
    private sealed class TimerWaitHandle : WaitHandle
    {
        public TimerWaitHandle(IntPtr handle) => SafeWaitHandle = new SafeWaitHandle(handle, ownsHandle: false);
    }
}
