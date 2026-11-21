namespace ConectaRemoteAgent;

/// <summary>
/// Tradução das teclas físicas do navegador (KeyboardEvent.code) para virtual keys do Windows.
/// Usar o código físico mantém acentos e combinações iguais às do computador assistido.
/// </summary>
internal static class KeyMap
{
    public static readonly Dictionary<string, ushort> Map = new(StringComparer.Ordinal)
    {
        // Letras
        ["KeyA"] = 0x41, ["KeyB"] = 0x42, ["KeyC"] = 0x43, ["KeyD"] = 0x44, ["KeyE"] = 0x45,
        ["KeyF"] = 0x46, ["KeyG"] = 0x47, ["KeyH"] = 0x48, ["KeyI"] = 0x49, ["KeyJ"] = 0x4A,
        ["KeyK"] = 0x4B, ["KeyL"] = 0x4C, ["KeyM"] = 0x4D, ["KeyN"] = 0x4E, ["KeyO"] = 0x4F,
        ["KeyP"] = 0x50, ["KeyQ"] = 0x51, ["KeyR"] = 0x52, ["KeyS"] = 0x53, ["KeyT"] = 0x54,
        ["KeyU"] = 0x55, ["KeyV"] = 0x56, ["KeyW"] = 0x57, ["KeyX"] = 0x58, ["KeyY"] = 0x59,
        ["KeyZ"] = 0x5A,

        // Números (linha superior)
        ["Digit0"] = 0x30, ["Digit1"] = 0x31, ["Digit2"] = 0x32, ["Digit3"] = 0x33, ["Digit4"] = 0x34,
        ["Digit5"] = 0x35, ["Digit6"] = 0x36, ["Digit7"] = 0x37, ["Digit8"] = 0x38, ["Digit9"] = 0x39,

        // Teclado numérico
        ["Numpad0"] = 0x60, ["Numpad1"] = 0x61, ["Numpad2"] = 0x62, ["Numpad3"] = 0x63,
        ["Numpad4"] = 0x64, ["Numpad5"] = 0x65, ["Numpad6"] = 0x66, ["Numpad7"] = 0x67,
        ["Numpad8"] = 0x68, ["Numpad9"] = 0x69,
        ["NumpadAdd"] = 0x6B, ["NumpadSubtract"] = 0x6D, ["NumpadMultiply"] = 0x6A,
        ["NumpadDivide"] = 0x6F, ["NumpadDecimal"] = 0x6E,

        // Controle
        ["Enter"] = 0x0D, ["Escape"] = 0x1B, ["Tab"] = 0x09, ["Backspace"] = 0x08,
        ["Space"] = 0x20, ["Delete"] = 0x2E, ["Insert"] = 0x2D, ["CapsLock"] = 0x14,
        ["ShiftLeft"] = 0xA0, ["ShiftRight"] = 0xA1, ["ControlLeft"] = 0xA2,
        ["ControlRight"] = 0xA3, ["AltLeft"] = 0xA4, ["AltRight"] = 0xA5,
        ["MetaLeft"] = 0x5B, ["MetaRight"] = 0x5C, ["ContextMenu"] = 0x5D,
        ["NumLock"] = 0x90, ["ScrollLock"] = 0x91, ["PrintScreen"] = 0x2C, ["Pause"] = 0x13,

        // Navegação
        ["ArrowUp"] = 0x26, ["ArrowDown"] = 0x28, ["ArrowLeft"] = 0x25, ["ArrowRight"] = 0x27,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,

        // Função
        ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73, ["F5"] = 0x74, ["F6"] = 0x75,
        ["F7"] = 0x76, ["F8"] = 0x77, ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,

        // Símbolos (layout ABNT / US)
        ["Minus"] = 0xBD, ["Equal"] = 0xBB, ["BracketLeft"] = 0xDB, ["BracketRight"] = 0xDD,
        ["Backslash"] = 0xDC, ["Semicolon"] = 0xBA, ["Quote"] = 0xDE, ["Backquote"] = 0xC0,
        ["Comma"] = 0xBC, ["Period"] = 0xBE, ["Slash"] = 0xBF,

        // Teclas de mídia
        ["AudioVolumeMute"] = 0xAD, ["AudioVolumeDown"] = 0xAE, ["AudioVolumeUp"] = 0xAF,
    };

    private static readonly HashSet<string> ExtendedCodes = new(StringComparer.Ordinal)
    {
        "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight",
        "Home", "End", "PageUp", "PageDown", "Insert", "Delete",
        "ControlRight", "AltRight", "MetaLeft", "MetaRight", "ContextMenu",
        "NumLock", "PrintScreen", "NumpadDivide", "NumpadEnter",
    };

    private static readonly HashSet<ushort> ExtendedVirtualKeys = new()
    {
        0x26, 0x28, 0x25, 0x27, 0x24, 0x23, 0x21, 0x22, 0x2D, 0x2E,
        0xA3, 0xA5, 0x5B, 0x5C, 0x5D, 0x90, 0x2C, 0x6F,
    };

    public static bool IsExtended(string code) => ExtendedCodes.Contains(code);

    public static bool IsExtendedVirtualKey(ushort virtualKey) => ExtendedVirtualKeys.Contains(virtualKey);
}