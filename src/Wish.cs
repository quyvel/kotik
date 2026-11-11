using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kotik
{
    /// New Year's eve: write down a wish. Ember keeps it and reminds you in January.
    class WishDialog : Form
    {
        readonly TextBox box = new TextBox();
        public string Wish { get { return box.Text; } }

        public WishDialog()
        {
            float k;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            Text = L.T("Желание на Новый год", "A New Year wish");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 28, 26);
            ForeColor = Color.FromArgb(235, 230, 220);
            Font = new Font("Segoe UI", 11f);
            ClientSize = new Size((int)(440 * k), (int)(210 * k));
            var l = new Label { Text = L.T("Загадай желание. Его увидит только Огонёк — и напомнит тебе в январе.", "Make a wish. Only Ember will see it, and will remind you in January."), AutoSize = false };
            l.SetBounds((int)(20 * k), (int)(16 * k), (int)(400 * k), (int)(50 * k));
            box.SetBounds((int)(20 * k), (int)(76 * k), (int)(400 * k), (int)(30 * k));
            box.BackColor = Color.FromArgb(50, 47, 44);
            box.ForeColor = ForeColor;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.MaxLength = 140;
            var ok = new Button { Text = L.T("Загадать", "Make it"), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(200, 120, 50), ForeColor = Color.White };
            ok.FlatAppearance.BorderSize = 0;
            ok.SetBounds((int)(150 * k), (int)(130 * k), (int)(140 * k), (int)(40 * k));
            Controls.Add(l);
            Controls.Add(box);
            Controls.Add(ok);
            AcceptButton = ok;
        }
    }
}
