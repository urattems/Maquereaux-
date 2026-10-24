using System.Diagnostics;
using Maquereaux.Core.Input;
using Maquereaux.Win32.Native;

namespace Maquereaux.Win32;

/// <summary>
/// Nom du process de la fenêtre au premier plan. Interrogé à chaque tick (jusqu'à 1 000×/s) :
/// GetForegroundWindow est quasi gratuit, et le nom est mis en cache par PID (OpenProcess coûte plus cher).
/// </summary>
public sealed class ForegroundProcessProvider : IForegroundProcessProvider
{
    private readonly object _lock = new();
    private uint _lastPid;
    private string? _lastName;

    public string? GetForegroundProcessName()
    {
        var pid = GetForegroundProcessId();
        if (pid == 0) return null;
        lock (_lock)
        {
            if (pid == _lastPid) return _lastName;
            _lastPid = pid;
            _lastName = ProcessInfo.GetProcessName(pid);
            return _lastName;
        }
    }

    public static uint GetForegroundProcessId()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }
}

public static class ProcessInfo
{
    /// <summary>Nom d'exe normalisé ("notepad.exe") d'un PID, ou null.</summary>
    public static unsafe string? GetProcessName(uint pid)
    {
        var h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            char* buffer = stackalloc char[1024];
            uint size = 1024;
            if (!NativeMethods.QueryFullProcessImageNameW(h, 0, buffer, ref size)) return null;
            var path = new string(buffer, 0, (int)size);
            return Path.GetFileName(path).ToLowerInvariant();
        }
        finally
        {
            NativeMethods.CloseHandle(h);
        }
    }

    /// <summary>Processus ayant une fenêtre visible (pour la liste « fenêtre cible »), triés par nom.</summary>
    public static IReadOnlyList<string> GetWindowedProcessNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && p.Id != Environment.ProcessId)
                    names.Add(p.ProcessName.ToLowerInvariant() + ".exe");
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // process terminé entre-temps ou accès refusé : on ignore
            }
            finally
            {
                p.Dispose();
            }
        }
        return names.ToList();
    }

    /// <summary>Vrai si Maquereaux tourne en administrateur.</summary>
    public static bool IsCurrentProcessElevated => Environment.IsPrivilegedProcess;

    /// <summary>
    /// Le process est-il élevé (admin) ? Si on ne peut même pas lire son jeton, c'est qu'il est plus privilégié
    /// que nous → on répond « oui » (c'est justement le cas UIPI qu'on veut détecter).
    /// </summary>
    public static unsafe bool IsProcessElevated(uint pid)
    {
        var h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return true;
        try
        {
            if (!NativeMethods.OpenProcessToken(h, NativeMethods.TOKEN_QUERY, out var token)) return true;
            try
            {
                int elevated = 0;
                return NativeMethods.GetTokenInformation(token, NativeMethods.TokenElevation, &elevated, sizeof(int), out _) && elevated != 0;
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(h);
        }
    }

    /// <summary>
    /// Cas UIPI : la fenêtre au premier plan est en admin et pas nous → Windows bloquera silencieusement SendInput.
    /// Retourne le nom du process concerné, ou null si tout va bien.
    /// </summary>
    public static string? DetectUipiBlockedForeground()
    {
        if (IsCurrentProcessElevated) return null;
        var pid = ForegroundProcessProvider.GetForegroundProcessId();
        if (pid == 0 || pid == Environment.ProcessId) return null;
        return IsProcessElevated(pid) ? GetProcessName(pid) ?? $"PID {pid}" : null;
    }
}
