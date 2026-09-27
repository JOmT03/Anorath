using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ERP
{
    // =====================================================
    // API CLIENT — one place that adds X-Company-Code and
    // turns server errors into readable messages.
    // =====================================================
    internal class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
    }

    internal class ApiClient
    {
        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("http://localhost:5166/") };
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private readonly string _companyCode;
        public ApiClient(string companyCode) { _companyCode = companyCode; }

        private HttpRequestMessage Build(HttpMethod method, string url, object? body)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-Company-Code", _companyCode);
            if (body != null)
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return request;
        }

        public async Task<T> GetAsync<T>(string url)
        {
            using var response = await Http.SendAsync(Build(HttpMethod.Get, url, null));
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new ApiException(ErrorText(response, text));
            return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new ApiException("Empty response from server.");
        }

        public Task PostAsync(string url, object body) => SendAsync(HttpMethod.Post, url, body);
        public Task PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body);
        public Task DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, null);

        private async Task SendAsync(HttpMethod method, string url, object? body)
        {
            using var response = await Http.SendAsync(Build(method, url, body));
            if (!response.IsSuccessStatusCode)
                throw new ApiException(ErrorText(response, await response.Content.ReadAsStringAsync()));
        }

        private static string ErrorText(HttpResponseMessage response, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return $"Server returned {(int)response.StatusCode} ({response.ReasonPhrase}).";

            var trimmed = text.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = new List<string>();
                        foreach (var prop in errors.EnumerateObject())
                            foreach (var msg in prop.Value.EnumerateArray())
                                messages.Add(msg.GetString() ?? "");
                        if (messages.Count > 0) return string.Join(" ", messages);
                    }
                    if (doc.RootElement.TryGetProperty("title", out var title))
                        return title.GetString() ?? trimmed;
                }
                catch (JsonException) { }
            }
            return trimmed.Trim('"');
        }
    }

    // Simple combo box item
    internal class OptionItem
    {
        public int Id { get; }
        public string Display { get; }
        public decimal Extra { get; }

        public OptionItem(int id, string display, decimal extra = 0)
        {
            Id = id;
            Display = display;
            Extra = extra;
        }

        public override string ToString() => Display;
    }

    // =====================================================
    // UI KIT — shared styling so every module looks the same
    // =====================================================
    internal static class UiKit
    {
        public static readonly Color Green = ColorTranslator.FromHtml("#2E8B57");
        public static readonly Color Amber = ColorTranslator.FromHtml("#D08C2E");
        public static readonly Color Red = ColorTranslator.FromHtml("#B94A48");
        public static readonly Color Slate = ColorTranslator.FromHtml("#5A6F7A");

        public static string Peso(decimal value) => "₱" + value.ToString("N2");

        public static string? NullIfEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        public static Label Heading(string text, int left = 20, int top = 12)
            => new Label { Text = text, Font = AppTheme.SubHeadingFont, ForeColor = AppTheme.TextDark, AutoSize = true, Left = left, Top = top };

        public static Label Caption(string text, int left, int top, int width = 150)
            => new Label { Text = text, Font = AppTheme.BodyFont, ForeColor = AppTheme.TextMuted, Left = left, Top = top, Width = width, Height = 20 };

        public static void AddField(Control parent, string caption, Control input, int left, int width, int top = 12)
        {
            parent.Controls.Add(Caption(caption, left, top, width));
            input.Font = AppTheme.BodyFont;
            input.SetBounds(left, top + 22, width, 28);
            parent.Controls.Add(input);
        }

        public static Button ActionButton(string icon, string text, int left, int top, int width = 130, bool primary = true)
            => AppTheme.CreateIconButton(icon, text, left, top, width, 34,
                primary ? AppTheme.Teal : AppTheme.LightTeal,
                primary ? Color.White : AppTheme.DarkTeal);

        public static DataGridView Grid()
        {
            var grid = new DataGridView
            {
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                Font = AppTheme.BodyFont,
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            grid.RowTemplate.Height = 28;
            grid.ColumnHeadersDefaultCellStyle.BackColor = AppTheme.DarkTeal;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = AppTheme.ButtonFont;
            grid.DefaultCellStyle.SelectionBackColor = AppTheme.LightTeal;
            grid.DefaultCellStyle.SelectionForeColor = AppTheme.TextDark;
            grid.AlternatingRowsDefaultCellStyle.BackColor = AppTheme.Background;
            return grid;
        }

        public static void FormatMoney(DataGridView grid, params string[] columns)
        {
            foreach (var name in columns)
            {
                var col = grid.Columns[name];
                if (col == null) continue;
                col.DefaultCellStyle.Format = "N2";
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }

        public static void FormatDate(DataGridView grid, params string[] columns)
        {
            foreach (var name in columns)
            {
                var col = grid.Columns[name];
                if (col != null) col.DefaultCellStyle.Format = "MMM d, yyyy";
            }
        }

        public static void HideColumns(DataGridView grid, params string[] columns)
        {
            foreach (var name in columns)
            {
                var col = grid.Columns[name];
                if (col != null) col.Visible = false;
            }
        }

        // Colors a "Status" column so the state is visible at a glance
        public static void ColorStatus(DataGridView grid, string column = "Status")
        {
            grid.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != column || e.Value is not string status) return;
                e.CellStyle!.ForeColor = status switch
                {
                    "Received" or "CheckedOut" or "Confirmed" => Green,
                    "Pending" => Amber,
                    "Cancelled" => Red,
                    "CheckedIn" => AppTheme.Teal,
                    _ => AppTheme.TextDark
                };
                e.CellStyle.Font = AppTheme.ButtonFont;
            };
        }

        // Top area (form fields) + a grid that fills the rest and resizes with the window
        public static void StackLayout(Control parent, Control top, int topHeight, Control fill)
        {
            top.Dock = DockStyle.Top;
            top.Height = topHeight;
            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 0, 20, 20) };
            fill.Dock = DockStyle.Fill;
            host.Controls.Add(fill);
            parent.Controls.Add(host);
            parent.Controls.Add(top);
            host.BringToFront();
        }

        public static Panel KpiCard(string caption, Color accent, out Label valueLabel, int width = 200)
        {
            var card = new Panel { Width = width, Height = 90, BackColor = Color.White, Margin = new Padding(0, 0, 12, 12) };
            var strip = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent };
            var lblCaption = new Label
            {
                Text = caption,
                Font = AppTheme.BodyFont,
                ForeColor = AppTheme.TextMuted,
                Left = 16,
                Top = 12,
                Width = width - 22,
                Height = 20
            };
            valueLabel = new Label
            {
                Text = "—",
                Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
                ForeColor = AppTheme.TextDark,
                Left = 16,
                Top = 38,
                Width = width - 22,
                Height = 36
            };
            card.Controls.Add(valueLabel);
            card.Controls.Add(lblCaption);
            card.Controls.Add(strip);
            return card;
        }

        public static void ShowError(Exception ex)
            => MessageBox.Show(ex is ApiException ? ex.Message : "Connection error: " + ex.Message,
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static void Warn(string message)
            => MessageBox.Show(message, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static bool Confirm(string message)
            => MessageBox.Show(message, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    // =====================================================
    // BAR CHART — drawn with GDI+, no NuGet package needed
    // =====================================================
    internal class BarChart : Control
    {
        private List<(string Label, decimal Value)> _data = new();

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string ChartTitle { get; set; } = "";

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Func<decimal, string> ValueFormat { get; set; } = v => v.ToString("N0");

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Color BarColor { get; set; } = AppTheme.Teal;

        public BarChart()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        public void SetData(IEnumerable<(string Label, decimal Value)> data)
        {
            _data = data.ToList();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using var titleFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            using var smallFont = new Font("Segoe UI", 8.5F);
            using var textBrush = new SolidBrush(AppTheme.TextDark);
            using var mutedBrush = new SolidBrush(AppTheme.TextMuted);
            using var barBrush = new SolidBrush(BarColor);
            using var axisPen = new Pen(AppTheme.LightTeal, 1.5f);

            g.DrawString(ChartTitle, titleFont, textBrush, 12, 10);

            if (_data.Count == 0 || _data.All(d => d.Value == 0))
            {
                g.DrawString("No data yet.", smallFont, mutedBrush, 12, 45);
                return;
            }

            int left = 20, top = 55, right = Width - 20, bottom = Height - 30;
            int plotHeight = bottom - top;
            if (plotHeight < 20 || right - left < 20) return;

            decimal max = _data.Max(d => d.Value);
            if (max <= 0) max = 1;

            float slot = (right - left) / (float)_data.Count;
            float barWidth = Math.Max(6, Math.Min(60, slot * 0.6f));

            g.DrawLine(axisPen, left, bottom, right, bottom);

            for (int i = 0; i < _data.Count; i++)
            {
                var (label, value) = _data[i];
                float height = (float)(value / max) * (plotHeight - 20);
                float x = left + i * slot + (slot - barWidth) / 2;
                float y = bottom - height;

                if (height > 0) g.FillRectangle(barBrush, x, y, barWidth, height);

                var valueText = ValueFormat(value);
                var valueSize = g.MeasureString(valueText, smallFont);
                g.DrawString(valueText, smallFont, textBrush, x + barWidth / 2 - valueSize.Width / 2, y - valueSize.Height - 2);

                var labelSize = g.MeasureString(label, smallFont);
                g.DrawString(label, smallFont, mutedBrush, x + barWidth / 2 - labelSize.Width / 2, bottom + 5);
            }
        }
    }
}