using System.Runtime.InteropServices;

namespace ConectaRemoteAgent;

/// <summary>
/// Executa entradas de mouse e teclado no Windows usando SendInput.
/// Somente comandos de uma sessão já autorizada chegam até aqui.
/// </summary>
internal sealed class InputExecutor
{
    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_HWHEEL = 0x01000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private readonly object _gate = new();
    private readonly HashSet<ushort> _pressedKeys = new();
    private readonly HashSet<string> _pressedButtons = new();

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    public static (int width, int height) ScreenSize()
    {
        var width = Math.Max(1, GetSystemMetrics(SM_CXSCREEN));
        var height = Math.Max(1, GetSystemMetrics(SM_CYSCREEN));
        return (width, height);
    }

    public void MovePointer(double normalizedX, double normalizedY)
    {
        var (width, height) = ScreenSize();
        var x = (int)Math.Round(Math.Clamp(normalizedX, 0, 1) * (width - 1));
        var y = (int)Math.Round(Math.Clamp(normalizedY, 0, 1) * (height - 1));
        SetCursorPos(x, y);
    }

    public void PointerButton(string action, string button)
    {
        var (flags, name) = button switch
        {
            "right" => (action == "down" ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP, "right"),
            "middle" => (action == "down" ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP, "middle"),
            _ => (action == "down" ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP, "left"),
        };

        lock (_gate)
        {
            if (action == "down") _pressedButtons.Add(name);
            else _pressedButtons.Remove(name);
        }

        SendMouse(flags);
    }

    public void Wheel(double deltaX, double deltaY)
    {
        if (Math.Abs(deltaY) > 0)
        {
            SendMouse(MOUSEEVENTF_WHEEL, unchecked((uint)(int)Math.Round(-deltaY)));
        }
        if (Math.Abs(deltaX) > 0)
        {
            SendMouse(MOUSEEVENTF_HWHEEL, unchecked((uint)(int)Math.Round(deltaX)));
        }
    }

    public bool Key(string action, string code)
    {
        if (!KeyMap.TryGetValue(code, out var virtualKey)) return false;

        var extended = KeyMap.IsExtended(code);
        lock (_gate)
        {
            if (action == "down") _pressedKeys.Add(virtualKey);
            else _pressedKeys.Remove(virtualKey);
        }
        SendKey(virtualKey, action == "up", extended);
        return true;
    }

    /// <summary>Solta tudo o que estiver pressionado. Chamado ao revogar, encerrar ou parar.</summary>
    public void ReleaseAll()
    {
        ushort[] keys;
        string[] buttons;
        lock (_gate)
        {
            keys = _pressedKeys.ToArray();
            buttons = _pressedButtons.ToArray();
            _pressedKeys.Clear();
            _pressedButtons.Clear();
        }

        foreach (var key in keys) SendKey(key, true, KeyMap.IsExtendedVirtualKey(key));
        foreach (var button in buttons) PointerButton("up", button);
    }

    private static void SendMouse(uint flags, uint data = 0)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT { dx = 0, dy = 0, mouseData = data, dwFlags = flags },
            },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void SendKey(ushort virtualKey, bool keyUp, bool extended)
    {
        uint flags = 0;
        if (keyUp) flags |= KEYEVENTF_KEYUP;
        if (extended) flags |= KEYEVENTF_EXTENDEDKEY;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT { wVk = virtualKey, wScan = 0, dwFlags = flags },
            },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}