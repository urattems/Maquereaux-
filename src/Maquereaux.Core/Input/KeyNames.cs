namespace Maquereaux.Core.Input;

/// <summary>
/// Codes de touches virtuelles (VK) Windows utiles + noms affichables par défaut.
/// Dupliqués ici (plutôt que lus via Win32) pour que Core reste indépendant de Windows et testable.
/// </summary>
public static class VirtualKeys
{
    // Boutons de souris : utilisables comme raccourci en mode « Maintenir » (détectés par le hook souris)
    public const ushort LButton = 0x01;
    public const ushort RButton = 0x02;
    public const ushort MButton = 0x04;
    public const ushort XButton1 = 0x05;
    public const ushort XButton2 = 0x06;
    public const ushort Cancel = 0x03;      // Ctrl+Pause génère souvent VK_CANCEL au lieu de VK_PAUSE !
    public const ushort Back = 0x08;
    public const ushort Tab = 0x09;
    public const ushort Return = 0x0D;
    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Menu = 0x12;        // Alt
    public const ushort Pause = 0x13;
    public const ushort Capital = 0x14;
    public const ushort Escape = 0x1B;
    public const ushort Space = 0x20;
    public const ushort Prior = 0x21;       // Page Up
    public const ushort Next = 0x22;        // Page Down
    public const ushort End = 0x23;
    public const ushort Home = 0x24;
    public const ushort Left = 0x25;
    public const ushort Up = 0x26;
    public const ushort Right = 0x27;
    public const ushort Down = 0x28;
    public const ushort Snapshot = 0x2C;
    public const ushort Insert = 0x2D;
    public const ushort Delete = 0x2E;
    public const ushort D0 = 0x30;
    public const ushort A = 0x41;
    public const ushort E = 0x45;
    public const ushort Z = 0x5A;
    public const ushort LWin = 0x5B;
    public const ushort RWin = 0x5C;
    public const ushort Apps = 0x5D;
    public const ushort NumPad0 = 0x60;
    public const ushort Multiply = 0x6A;
    public const ushort Add = 0x6B;
    public const ushort Separator = 0x6C;
    public const ushort Subtract = 0x6D;
    public const ushort Decimal = 0x6E;
    public const ushort Divide = 0x6F;
    public const ushort F1 = 0x70;
    public const ushort F6 = 0x75;
    public const ushort F7 = 0x76;
    public const ushort F8 = 0x77;
    public const ushort F9 = 0x78;
    public const ushort F13 = 0x7C;
    public const ushort F24 = 0x87;
    public const ushort NumLock = 0x90;
    public const ushort Scroll = 0x91;
    public const ushort LShift = 0xA0;
    public const ushort RShift = 0xA1;
    public const ushort LControl = 0xA2;
    public const ushort RControl = 0xA3;
    public const ushort LMenu = 0xA4;
    public const ushort RMenu = 0xA5;

    /// <summary>Vrai pour les boutons de souris (clic gauche, droit, molette, boutons latéraux 4 et 5).</summary>
    public static bool IsMouseButton(ushort vk) => vk is LButton or RButton or MButton or XButton1 or XButton2;

    /// <summary>Vrai pour les touches modificatrices (Ctrl, Alt, Shift, Win, gauche/droite).</summary>
    public static bool IsModifier(ushort vk) => vk is Shift or Control or Menu or LWin or RWin
        or LShift or RShift or LControl or RControl or LMenu or RMenu;
}

/// <summary>Noms par défaut des touches et table des touches « étendues ».</summary>
public static class KeyNames
{
    private static readonly Dictionary<ushort, string> Names = BuildNames();

    private static Dictionary<ushort, string> BuildNames()
    {
        var d = new Dictionary<ushort, string>
        {
            [VirtualKeys.LButton] = "Clic gauche",
            [VirtualKeys.RButton] = "Clic droit",
            [VirtualKeys.MButton] = "Clic molette",
            [VirtualKeys.XButton1] = "Souris 4",
            [VirtualKeys.XButton2] = "Souris 5",
            [VirtualKeys.Cancel] = "Break",
            [VirtualKeys.Back] = "Retour arrière",
            [VirtualKeys.Tab] = "Tab",
            [VirtualKeys.Return] = "Entrée",
            [VirtualKeys.Shift] = "Shift",
            [VirtualKeys.Control] = "Ctrl",
            [VirtualKeys.Menu] = "Alt",
            [VirtualKeys.Pause] = "Pause",
            [VirtualKeys.Capital] = "Verr. Maj",
            [VirtualKeys.Escape] = "Échap",
            [VirtualKeys.Space] = "Espace",
            [VirtualKeys.Prior] = "Page préc.",
            [VirtualKeys.Next] = "Page suiv.",
            [VirtualKeys.End] = "Fin",
            [VirtualKeys.Home] = "Origine",
            [VirtualKeys.Left] = "←",
            [VirtualKeys.Up] = "↑",
            [VirtualKeys.Right] = "→",
            [VirtualKeys.Down] = "↓",
            [VirtualKeys.Snapshot] = "Impr. écran",
            [VirtualKeys.Insert] = "Inser",
            [VirtualKeys.Delete] = "Suppr",
            [VirtualKeys.LWin] = "Win",
            [VirtualKeys.RWin] = "Win droite",
            [VirtualKeys.Apps] = "Menu",
            [VirtualKeys.Multiply] = "Pavé *",
            [VirtualKeys.Add] = "Pavé +",
            [VirtualKeys.Separator] = "Pavé Séparateur",
            [VirtualKeys.Subtract] = "Pavé -",
            [VirtualKeys.Decimal] = "Pavé .",
            [VirtualKeys.Divide] = "Pavé /",
            [VirtualKeys.NumLock] = "Verr. Num",
            [VirtualKeys.Scroll] = "Arrêt défil.",
            [VirtualKeys.LShift] = "Shift gauche",
            [VirtualKeys.RShift] = "Shift droit",
            [VirtualKeys.LControl] = "Ctrl gauche",
            [VirtualKeys.RControl] = "Ctrl droit",
            [VirtualKeys.LMenu] = "Alt gauche",
            [VirtualKeys.RMenu] = "Alt Gr",
        };
        for (ushort vk = 0x30; vk <= 0x39; vk++) d[vk] = ((char)vk).ToString();                  // 0-9
        for (ushort vk = 0x41; vk <= 0x5A; vk++) d[vk] = ((char)vk).ToString();                  // A-Z
        for (ushort i = 0; i <= 9; i++) d[(ushort)(VirtualKeys.NumPad0 + i)] = $"Pavé {i}";
        for (ushort i = 0; i < 24; i++) d[(ushort)(VirtualKeys.F1 + i)] = $"F{i + 1}";
        return d;
    }

    /// <summary>Nom lisible d'un VK (ex. 0x41 → "A", 0x75 → "F6"), ou "VK 0xNN" si inconnu.</summary>
    public static string GetName(ushort vk) => Names.TryGetValue(vk, out var n) ? n : $"VK 0x{vk:X2}";

    /// <summary>
    /// Touches « étendues » : leur scan code est préfixé par 0xE0 sur le clavier physique.
    /// Avec SendInput en mode scan code, il faut ajouter KEYEVENTF_EXTENDEDKEY, sinon une flèche
    /// est interprétée comme la touche du pavé numérique correspondante (4/6/8/2…).
    /// </summary>
    public static bool IsExtendedKey(ushort vk) => vk is
        VirtualKeys.Prior or VirtualKeys.Next or VirtualKeys.End or VirtualKeys.Home or
        VirtualKeys.Left or VirtualKeys.Up or VirtualKeys.Right or VirtualKeys.Down or
        VirtualKeys.Insert or VirtualKeys.Delete or VirtualKeys.Snapshot or
        VirtualKeys.LWin or VirtualKeys.RWin or VirtualKeys.Apps or
        VirtualKeys.Divide or VirtualKeys.NumLock or VirtualKeys.Cancel or
        VirtualKeys.RControl or VirtualKeys.RMenu;
}
