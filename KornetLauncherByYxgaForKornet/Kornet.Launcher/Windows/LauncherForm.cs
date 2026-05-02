using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Kornet.Launcher.Windows
{
    public partial class LauncherForm : Form
    {
        private static byte[] ReadAllBytes(Stream s)
        {
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        private static Image LoadAsset(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(name)
                ?? throw new Exception($"Asset not found: {name}");
            return Image.FromStream(new MemoryStream(ReadAllBytes(stream)));
        }

        private static Icon LoadIcon(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(name)
                ?? throw new Exception($"Asset not found: {name}");
            return new Icon(stream);
        }

        private static Image GetIconImage(Icon icon, int width, int height)
            => new Icon(icon, new Size(width, height)).ToBitmap();

        private static readonly Image _cancelNormal = LoadAsset("Kornet.Launcher.Assets.CancelButton.png");
        private static readonly Image _cancelHover = LoadAsset("Kornet.Launcher.Assets.CancelButtonHover.png");
        private static readonly Icon _appIcon = LoadIcon("Kornet.Launcher.Assets.appicon.ico");

        public LauncherForm()
        {
            InitializeComponent();
            this.Icon = _appIcon;
            IconBox.BackgroundImage = GetIconImage(_appIcon, 128, 128);
            buttonCancel.Image = _cancelNormal;
        }

        private void ButtonCancel_Click(object? sender, EventArgs e) => Close();

        private void ButtonCancel_MouseEnter(object? sender, EventArgs e)
            => buttonCancel.Image = _cancelHover;

        private void ButtonCancel_MouseLeave(object? sender, EventArgs e)
            => buttonCancel.Image = _cancelNormal;
    }
}