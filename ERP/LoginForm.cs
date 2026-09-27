using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    public class LoginForm : Form
    {
        private readonly HttpClient _http = new HttpClient { BaseAddress = new Uri("http://localhost:5166/") };
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly string _companyCode;
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private Label lblStatus = null!;

        public string LoggedInCompanyCode => _companyCode;
        public string LoggedInUsername { get; private set; } = "";
        public string LoggedInFullName { get; private set; } = "";
        public string LoggedInRole { get; private set; } = "";

        public LoginForm(string companyCode)
        {
            _companyCode = companyCode;
            BuildUi();
        }

        private void BuildUi()
        {
            this.Text = "Staff Login";
            this.Width = 420;
            this.Height = 340;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = AppTheme.Background;

            var lblTitle = new Label { Text = $"Sign in \u2014 {_companyCode}", Font = AppTheme.SubHeadingFont, ForeColor = AppTheme.TextDark, Left = 30, Top = 25, Width = 340 };
            var lblUsername = new Label { Text = "Username", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 75, Width = 150 };
            txtUsername = new TextBox { Left = 30, Top = 98, Width = 340, Height = 30, Font = AppTheme.BodyFont };

            var lblPassword = new Label { Text = "Password", Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = 30, Top = 140, Width = 150 };
            txtPassword = new TextBox { Left = 30, Top = 163, Width = 340, Height = 30, Font = AppTheme.BodyFont, PasswordChar = '*' };

            var btnLogin = AppTheme.CreateIconButton("\u2192", "Login", 30, 210, 340, 45, AppTheme.Teal, System.Drawing.Color.White);
            btnLogin.Click += async (s, e) => await AttemptLoginAsync();

            lblStatus = new Label { Left = 30, Top = 265, Width = 340, Height = 40, ForeColor = System.Drawing.Color.Firebrick, Font = new System.Drawing.Font("Segoe UI", 8.5F), Text = "" };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblUsername);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblPassword);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnLogin);
            this.Controls.Add(lblStatus);

            this.AcceptButton = btnLogin;
        }

        private async Task AttemptLoginAsync()
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                lblStatus.Text = "Username and password are required.";
                return;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "auth/login");
                request.Headers.Add("X-Company-Code", _companyCode);
                var body = new { username = txtUsername.Text.Trim(), password = txtPassword.Text };
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    lblStatus.Text = "Invalid username or password.";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<LoginResultDto>(json, JsonOptions);
                if (result == null)
                {
                    lblStatus.Text = "Unexpected response from server.";
                    return;
                }

                LoggedInUsername = result.Username;
                LoggedInFullName = result.FullName;
                LoggedInRole = result.Role;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Connection error: " + ex.Message;
            }
        }
    }

    internal class LoginResultDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
    }
}