using System.Runtime.InteropServices;

namespace Maquereaux.Win32.Native;

// Déclarations P/Invoke. [LibraryImport] génère le code de marshalling à la compilation
// (plus rapide et plus sûr que [DllImport] : pas de stub généré au runtime).

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public nuint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public nuint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}

/// <summary>
/// ⚠️ Piège classique : l'union DOIT contenir MOUSEINPUT (la plus grosse), sinon sizeof(INPUT) est faux
/// (32 au lieu de 40 octets en x64) et SendInput échoue silencieusement (ERROR_INVALID_PARAMETER).
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion U;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public nuint wParam;
    public nint lParam;
    public uint time;
    public POINT pt;
    public uint lPrivate;
}

/// <summary>Données reçues par un hook WH_KEYBOARD_LL.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public nuint dwExtraInfo;
}

/// <summary>Données reçues par un hook WH_MOUSE_LL.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData;
    public uint flags;
    public uint time;
    public nuint dwExtraInfo;
}

internal static partial class NativeMethods
{
    // --- Constantes ---
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    public const uint MAPVK_VK_TO_VSC_EX = 4;

    public const uint WM_QUIT = 0x0012;
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_USER = 0x0400;
    public const uint WM_APP = 0x8000;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint PM_NOREMOVE = 0x0000;

    public const uint MOD_NOREPEAT = 0x4000;
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const uint LLMHF_INJECTED = 0x01;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_XBUTTONDOWN = 0x020B;
    public const uint WM_XBUTTONUP = 0x020C;
    public const uint LLKHF_EXTENDED = 0x01;
    public const uint LLKHF_INJECTED = 0x10;
    public const uint LLKHF_UP = 0x80;

    public const uint CREATE_WAITABLE_TIMER_MANUAL_RESET = 0x00000001;
    public const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;
    public const uint TIMER_ALL_ACCESS = 0x1F0003;

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint TOKEN_QUERY = 0x0008;
    public const int TokenElevation = 20;

    // --- user32 : clavier ---
    [LibraryImport("user32.dll", SetLastError = true)]
    public static unsafe partial uint SendInput(uint cInputs, INPUT* pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    public static partial uint MapVirtualKeyExW(uint uCode, uint uMapType, IntPtr dwhkl);

    [LibraryImport("user32.dll")]
    public static partial IntPtr GetKeyboardLayout(uint idThread);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial int GetKeyNameTextW(int lParam, char* lpString, int cchSize);

    // --- user32 : fenêtres ---
    [LibraryImport("user32.dll")]
    public static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    public static partial IntPtr WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    public static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // --- user32 : hotkeys & messages ---
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessageW(uint idThread, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport("user32.dll")]
    public static partial nint DispatchMessageW(in MSG lpMsg);

    // --- user32 : hooks bas niveau (clavier et souris : même signature de callback) ---
    public delegate nint LowLevelKeyboardProc(int nCode, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(IntPtr hhk, int nCode, nuint wParam, nint lParam);

    // --- kernel32 ---
    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr GetModuleHandle(string? lpModuleName);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateWaitableTimerExW(IntPtr lpTimerAttributes, string? lpTimerName, uint dwFlags, uint dwDesiredAccess);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWaitableTimer(IntPtr hTimer, in long lpDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, [MarshalAs(UnmanagedType.Bool)] bool fResume);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, char* lpExeName, ref uint lpdwSize);

    // --- advapi32 ---
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, void* tokenInformation, uint tokenInformationLength, out uint returnLength);

    // --- winmm : résolution du timer système ---
    [LibraryImport("winmm.dll")]
    public static partial uint timeBeginPeriod(uint uPeriod);

    [LibraryImport("winmm.dll")]
    public static partial uint timeEndPeriod(uint uPeriod);
}
