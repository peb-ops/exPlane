using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExPlane.Services;

/// <summary>Notification-area icon. Left click shows/hides; right click opens the menu.</summary>
public sealed class TrayService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly Icon _image;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pinItem;

    public event EventHandler? ShowHideRequested;
    public event EventHandler? TogglePinRequested;
    public event EventHandler? ExitRequested;

    public TrayService(bool pinned, string hotkey)
    {
        _image = CreateIcon();

        _pinItem = new ToolStripMenuItem("Always on top", null, (_, _) => TogglePinRequested?.Invoke(this, EventArgs.Empty))
        {
            Checked = pinned,
            ShortcutKeyDisplayString = hotkey,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / hide", null, (_, _) => ShowHideRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_pinItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new NotifyIcon
        {
            Icon = _image,
            Text = "exPlane",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowHideRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public void SetPinned(bool pinned) => _pinItem.Checked = pinned;

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, ToolTipIcon.Warning);

    /// <summary>Draws a small clock face so we don't need to ship an .ico yet.</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var face = new SolidBrush(Color.FromArgb(0x4E, 0xA1, 0xFF));
            g.FillEllipse(face, 2, 2, 28, 28);
            using var hand = new Pen(Color.White, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(hand, 16, 16, 16, 7);
            g.DrawLine(hand, 16, 16, 23, 16);
        }

        var handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _image.Dispose();
    }
}
