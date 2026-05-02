using System.Windows.Forms;

namespace Kornet.Launcher.Windows
{
    partial class LauncherForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            ProgressBar = new ProgressBar();
            StatusText = new Label();
            IconBox = new PictureBox();
            panel1 = new Panel();
            buttonCancel = new Label();
            ((System.ComponentModel.ISupportInitialize)IconBox).BeginInit();
            panel1.SuspendLayout();
            SuspendLayout();

            // ProgressBar
            ProgressBar.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            ProgressBar.Location = new System.Drawing.Point(29, 241);
            ProgressBar.MarqueeAnimationSpeed = 20;
            ProgressBar.Name = "ProgressBar";
            ProgressBar.Size = new System.Drawing.Size(460, 20);
            ProgressBar.Style = ProgressBarStyle.Marquee;
            ProgressBar.TabIndex = 0;

            // StatusText
            StatusText.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            StatusText.Location = new System.Drawing.Point(29, 199);
            StatusText.Name = "StatusText";
            StatusText.Size = new System.Drawing.Size(460, 18);
            StatusText.TabIndex = 1;
            StatusText.Text = "Loading...";
            StatusText.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            StatusText.UseMnemonic = false;

            // IconBox
            IconBox.BackgroundImageLayout = ImageLayout.Zoom;
            IconBox.Location = new System.Drawing.Point(212, 66);
            IconBox.Name = "IconBox";
            IconBox.Size = new System.Drawing.Size(92, 92);
            IconBox.TabIndex = 2;
            IconBox.TabStop = false;

            // buttonCancel
            buttonCancel.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            buttonCancel.ForeColor = System.Drawing.Color.FromArgb(75, 75, 75);
            buttonCancel.Location = new System.Drawing.Point(194, 264);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(130, 44);
            buttonCancel.TabIndex = 4;
            buttonCancel.Text = "Cancel";
            buttonCancel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            buttonCancel.UseMnemonic = false;
            buttonCancel.Click += ButtonCancel_Click;
            buttonCancel.MouseEnter += ButtonCancel_MouseEnter;
            buttonCancel.MouseLeave += ButtonCancel_MouseLeave;

            // panel1
            panel1.BackColor = System.Drawing.SystemColors.Window;
            panel1.Controls.Add(buttonCancel);
            panel1.Controls.Add(StatusText);
            panel1.Controls.Add(IconBox);
            panel1.Controls.Add(ProgressBar);
            panel1.Location = new System.Drawing.Point(1, 1);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(518, 318);
            panel1.TabIndex = 4;

            // LauncherForm
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = System.Drawing.SystemColors.ActiveBorder;
            ClientSize = new System.Drawing.Size(520, 320);
            Controls.Add(panel1);
            FormBorderStyle = FormBorderStyle.None;
            MaximumSize = new System.Drawing.Size(520, 320);
            MinimumSize = new System.Drawing.Size(520, 320);
            Name = "LauncherForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Kornet";

            ((System.ComponentModel.ISupportInitialize)IconBox).EndInit();
            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        protected ProgressBar ProgressBar;
        protected Label StatusText;
        private PictureBox IconBox;
        private Panel panel1;
        private Label buttonCancel;
    }
}