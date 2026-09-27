using System.Drawing;
using System.Windows.Forms;

namespace ERP
{
    internal static class AppTheme
    {
        public static readonly Color DarkTeal = ColorTranslator.FromHtml("#1B3B4C");
        public static readonly Color Teal = ColorTranslator.FromHtml("#2E6171");
        public static readonly Color LightTeal = ColorTranslator.FromHtml("#DCEAEE");
        public static readonly Color Background = ColorTranslator.FromHtml("#F7F9FA");
        public static readonly Color TextDark = ColorTranslator.FromHtml("#1B3B4C");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6C8A94");

        public static readonly Font HeadingFont = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
        public static readonly Font SubHeadingFont = new Font("Segoe UI Semibold", 12F, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Segoe UI", 10F);
        public static readonly Font ButtonFont = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);

        public static Button CreateIconButton(string icon, string text, int left, int top, int width, int height, Color backColor, Color foreColor)
        {
            var btn = new Button
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                Text = $"{icon}   {text}",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = ButtonFont,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.15f);
            return btn;
        }
    }
}