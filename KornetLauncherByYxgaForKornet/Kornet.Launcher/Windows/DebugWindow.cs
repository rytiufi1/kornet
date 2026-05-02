using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kornet.Launcher.Windows;

public class DebugWindow : Form
{
    private readonly RichTextBox _log;

    public DebugWindow()
    {
        Text = "Kornet Debug";
        Size = new Size(700, 400);
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(
            Screen.PrimaryScreen!.WorkingArea.Right - 710,
            Screen.PrimaryScreen!.WorkingArea.Bottom - 410
        );
        BackColor = Color.FromArgb(20, 20, 20);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;

        _log = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.LimeGreen,
            Font = new Font("Consolas", 9f),
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };

        Controls.Add(_log);
    }

    public void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(message));
            return;
        }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        _log.ScrollToCaret();
    }
}