using System.Drawing;
using Wrestling.UI.Material.ScoreScreen;

namespace Wrestling.UI.Material.Utils.Recording.OverlayDrawer
{
    public abstract class BaseOverlayDrawer : IOverlayDrawer
    {
        public abstract void DrawOverlay(Bitmap frame, ScoreScreenViewModel currentMatch);

        protected string GetFirstStringBySplit(string value, char symbol)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var parts = value.Split(symbol);
            return parts.Length > 0 ? parts[0] : string.Empty;
        }

        protected string GetStringInUpper(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToUpper();
        }

        private void DrawRectWithString(Graphics g, string text, Font font, float startX, float startY, SizeF rectSize,
            SolidBrush background, SolidBrush forecolor, StringFormat format)
        {
            var rect = new RectangleF(startX, startY, rectSize.Width, rectSize.Height);

            g.FillRectangle(background, rect);

            g.DrawString(text, font, forecolor, rect, format);
        }

        protected void DrawRectWithStringCenter(Graphics g, string text, Font font, float startX, float startY, SizeF rectSize, SolidBrush background, SolidBrush forecolor)
        {
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            DrawRectWithString(g, text, font, startX, startY, rectSize, background, forecolor, format);
        }

        protected void DrawRectWithStringLeft(Graphics g, string text, Font font, float startX, float startY, SizeF rectSize, SolidBrush background, SolidBrush forecolor)
        {
            var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            };

            DrawRectWithString(g, text, font, startX, startY, rectSize, background, forecolor, format);
        }

        protected SizeF GetRectSizeForText(Graphics g, string text, Font font)
        {
            var textSize = g.MeasureString(text, font);

            return new SizeF((float)(textSize.Width + textSize.Width * 0.1), (float)(textSize.Height + textSize.Height * 0.1));
        }

        protected void DrawWarningDots(Graphics g, int warningPoint, float baseX, float baseY, float panelHeight, float radius)
        {
            if (warningPoint > 0)
            {
                SolidBrush warningBrush = new SolidBrush(Color.Yellow);
                for (int i = 0; i < warningPoint; i++)
                {
                    float y = (float)(baseY + panelHeight * 0.1 + i * (panelHeight / 3));

                    g.FillEllipse(warningBrush, baseX, y, radius, radius);
                }
                warningBrush.Dispose();
            }
        }
    }
}