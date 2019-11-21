using System.Drawing;
using System.Drawing.Text;
using Wrestling.UI.Material.ScoreScreen;

namespace Wrestling.UI.Material.Utils.Recording.OverlayDrawer
{
    public class SimpleOverlayDrawer : BaseOverlayDrawer
    {
        public override void DrawOverlay(Bitmap frame, long time, ScoreScreenViewModel currentMatch)
        {
            if (currentMatch == null || frame == null) return;

            using (Graphics g = Graphics.FromImage(frame))
            {
                var imageWidth = frame.Width;
                var imageHeight = frame.Height;

                float startX = (float) imageWidth / 60;
                float startY = (float) imageHeight / 40;

                var wr1 = GetStringInUpper(currentMatch.Wrestler1);
                var wr2 = GetStringInUpper(currentMatch.Wrestler2);
                string timeValue = currentMatch.TickCounter.ToString("m\\:ss");
                var pt1 = currentMatch.Points1.ToString();
                var pt2 = currentMatch.Points2.ToString();
                var round = currentMatch.IsTimeout ? "TO" : currentMatch.Round.ToString();

                int fontSize = imageHeight / 48;
                var mainFont = new Font("Arial", fontSize, FontStyle.Bold);
                var pointsSize = GetRectSizeForText(g, "99", mainFont);

                SolidBrush redBrush = new SolidBrush(Color.DarkRed);
                SolidBrush blueBrush = new SolidBrush(Color.DarkBlue);
                SolidBrush whiteBrush = new SolidBrush(Color.GhostWhite);
                SolidBrush timeBrush = new SolidBrush(Color.FromArgb(100, Color.DarkBlue));

                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var timeSize = GetRectSizeForText(g, timeValue, mainFont);
                var wr1Size = GetRectSizeForText(g, wr1, mainFont);
                var wr2Size = GetRectSizeForText(g, wr2, mainFont);
                var nameSize = wr1Size.Width > wr2Size.Width ? wr1Size : wr2Size;

                if (string.IsNullOrEmpty(wr1) && string.IsNullOrEmpty(wr2))
                {
                    nameSize = GetRectSizeForText(g, "ИВАНОВ", mainFont);
                }

                DrawRectWithStringCenter(g, timeValue, mainFont, startX, startY, timeSize, timeBrush, whiteBrush);
                DrawRectWithStringCenter(g, round, mainFont, startX, startY + timeSize.Height, timeSize, timeBrush, whiteBrush);

                DrawRectWithStringLeft(g, wr1, mainFont, startX + timeSize.Width, startY, nameSize, redBrush, whiteBrush);
                DrawRectWithStringLeft(g, wr2, mainFont, startX + timeSize.Width, startY + nameSize.Height, nameSize, blueBrush, whiteBrush);

                if (currentMatch.IsAction1TimerEnabled || currentMatch.IsAction2TimerEnabled)
                {
                    using (var yellowBrush = new SolidBrush(Color.Yellow))
                    {
                        //g.FillRectangle(transpGreen, startX + timeSize.Width + nameSize.Width + pointsSize.Width - 1, startY, pointsSize.Width, pointsSize.Height + pointsSize.Height);
                        if (currentMatch.IsAction1TimerEnabled)
                        {
                            DrawRectWithStringCenter(g, currentMatch.TickCounterAction1.ToString("ss"), mainFont,
                                startX + timeSize.Width + nameSize.Width + pointsSize.Width, startY, pointsSize,
                                timeBrush, yellowBrush);
                        }
                        else if (currentMatch.IsAction2TimerEnabled)
                        {
                            DrawRectWithStringCenter(g, currentMatch.TickCounterAction2.ToString("ss"), mainFont,
                                startX + timeSize.Width + nameSize.Width + pointsSize.Width, startY + pointsSize.Height,
                                pointsSize, timeBrush, yellowBrush);
                        }
                    }
                }

                using (SolidBrush blackBrush = new SolidBrush(Color.Black))
                {
                    DrawRectWithStringCenter(g, pt1, mainFont, startX + timeSize.Width + nameSize.Width, startY,
                        pointsSize, whiteBrush, blackBrush);
                    DrawRectWithStringCenter(g, pt2, mainFont, startX + timeSize.Width + nameSize.Width,
                        startY + pointsSize.Height, pointsSize, whiteBrush, blackBrush);
                }

                int linesWidth = imageHeight / 480;

                using (var whitePen = new Pen(Color.LightGray, linesWidth))
                {
                    g.DrawLine(whitePen, startX + timeSize.Width, startY, startX + timeSize.Width,
                        startY + timeSize.Height * 2);
                    g.DrawLine(whitePen, startX, startY + timeSize.Height, startX + timeSize.Width,
                        startY + pointsSize.Height);
                    g.DrawLine(whitePen, startX + timeSize.Width + nameSize.Width + (float) (pointsSize.Width * 0.1),
                        startY + pointsSize.Height,
                        (float) (startX + timeSize.Width + nameSize.Width + pointsSize.Width - pointsSize.Width * 0.1),
                        startY + pointsSize.Height);

                    g.DrawRectangle(whitePen, startX, startY, timeSize.Width + nameSize.Width + pointsSize.Width,
                        pointsSize.Height + pointsSize.Height);
                }

                if (currentMatch.Points1 == currentMatch.Points2 && currentMatch.Points1 > 0)
                {
                    using (var blackPen = new Pen(Color.Black, linesWidth))
                    {
                        if (currentMatch.BestActionRed > currentMatch.BestActionBlue ||
                            currentMatch.BestActionRed == currentMatch.BestActionBlue && currentMatch.IsLastActionRed)
                        {
                            // draw advantage for red
                            g.DrawLine(blackPen,
                                (startX + timeSize.Width + nameSize.Width + (float) (pointsSize.Width * 0.2)),
                                ((startY + pointsSize.Height) - (float) (pointsSize.Height * 0.2)),
                                (float) ((startX + timeSize.Width + nameSize.Width + pointsSize.Width) -
                                         pointsSize.Width * 0.2),
                                ((startY + pointsSize.Height) - (float) (pointsSize.Height * 0.2)));
                        }
                        else if (currentMatch.BestActionBlue > currentMatch.BestActionRed ||
                                 (currentMatch.BestActionRed == currentMatch.BestActionBlue &&
                                  !currentMatch.IsLastActionRed))
                        {
                            // draw advantage for blue
                            g.DrawLine(blackPen,
                                startX + timeSize.Width + nameSize.Width + (float) (pointsSize.Width * 0.2),
                                (startY + pointsSize.Height + pointsSize.Height - (float) (pointsSize.Height * 0.2)),
                                (float) ((startX + timeSize.Width + nameSize.Width + pointsSize.Width) -
                                         pointsSize.Width * 0.2),
                                ((startY + pointsSize.Height + pointsSize.Height) - (float) (pointsSize.Height * 0.2)));
                        }
                    }
                }

                float pointX = startX + timeSize.Width + nameSize.Width;
                pointX = (float) (pointX - pointX * 0.05);

                DrawWarningDots(g, currentMatch.Wrestler1WarningsNumber, pointX, startY, nameSize.Height,
                    nameSize.Height / 5);
                DrawWarningDots(g, currentMatch.Wrestler2WarningsNumber, pointX, startY + nameSize.Height,
                    nameSize.Height, (nameSize.Height / 5));

                redBrush.Dispose();
                blueBrush.Dispose();
                whiteBrush.Dispose();
                mainFont.Dispose();
                timeBrush.Dispose();
            }
        }
    }
}