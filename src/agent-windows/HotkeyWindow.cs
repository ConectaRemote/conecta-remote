using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConectaRemoteAgent;

/// <summary>Janela invisível que recebe o atalho global de interrupção imediata.</summary>
internal sealed class HotkeyWindow : Form
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_F12 = 0x7B;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    private readonly Action _onHotkey;

    public HotkeyWindow(Action onHotkey)
    {
        _onHotkey = onHotkey;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-4000, -4000);
        Size = new System.Drawing.Size(1, 1);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, 1, MOD_CONTROL | MOD_ALT | MOD_SHIFT, VK_F12);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == 1)
        {
            _onHotkey();
            return;
        }
        base.WndProc(ref m);
    }
}