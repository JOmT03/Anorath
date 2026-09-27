using System;
using System.Drawing;
using System.Windows.Forms;

namespace ERP
{
    public class WelcomeForm : Form
    {
        private TextBox txtCompanyCode = null!;

        public WelcomeForm()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            this.Text = "Anorath Resort ERP";
            this.Width = 520;
            this.Height = 480;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = AppTheme.DarkTeal;

            var lblTitle = new Label { Text = "Anorath Resort", Font = AppTheme.HeadingFont, ForeColor = Color.White, Left = 40, Top = 60, Width = 440, Height = 40 };
            var lblSubtitle = new Label { Text = "Multi-tenant hospitality management", Font = AppTheme.BodyFont, ForeColor = AppTheme.LightTeal, Left = 40, Top = 105, Width = 440, Height = 25 };
            var lblCompanyCode = new Label { Text = "Company Code", Font = AppTheme.BodyFont, ForeColor = AppTheme.LightTeal, Left = 40, Top = 160, Width = 200 };
            txtCompanyCode = new TextBox { Left = 40, Top = 185, Width = 440, Height = 30, Text = "COMP-001", Font = AppTheme.BodyFont };

            var btnStaffLogin = AppTheme.CreateIconButton("\U0001F510", "Staff Login", 40, 250, 440, 55, AppTheme.Teal, Color.White);
            var btnPublicReservation = AppTheme.CreateIconButton("\U0001F4C5", "Request a Reservation", 40, 320, 440, 55, Color.White, AppTheme.DarkTeal);

            btnStaffLogin.Click += (s, e) => OpenLogin();
            btnPublicReservation.Click += (s, e) => OpenPublicReservation();

            var lblFooter = new Label
            {
                Text = "Walk-in guests: use \"Request a Reservation\" \u2014 no account needed.",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = AppTheme.LightTeal,
                Left = 40,
                Top = 390,
                Width = 440,
                Height = 40
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblSubtitle);
            this.Controls.Add(lblCompanyCode);
            this.Controls.Add(txtCompanyCode);
            this.Controls.Add(btnStaffLogin);
            this.Controls.Add(btnPublicReservation);
            this.Controls.Add(lblFooter);
        }

        private void OpenLogin()
        {
            if (string.IsNullOrWhiteSpace(txtCompanyCode.Text))
            {
                MessageBox.Show("Company Code is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var login = new LoginForm(txtCompanyCode.Text.Trim());
            if (login.ShowDialog(this) == DialogResult.OK)
            {
                this.Hide();
                var main = new Form1(login.LoggedInCompanyCode, login.LoggedInUsername, login.LoggedInFullName, login.LoggedInRole);
                main.FormClosed += (s2, e2) => this.Close();
                main.Show();
            }
        }

        private void OpenPublicReservation()
        {
            if (string.IsNullOrWhiteSpace(txtCompanyCode.Text))
            {
                MessageBox.Show("Company Code is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var pubForm = new PublicReservationForm(txtCompanyCode.Text.Trim());
            pubForm.ShowDialog(this);
        }
    }
}