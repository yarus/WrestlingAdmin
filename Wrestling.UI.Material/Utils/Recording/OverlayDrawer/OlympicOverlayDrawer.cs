using System;
using System.Drawing;
using System.Drawing.Text;
using Wrestling.UI.Material.ScoreScreen;

namespace Wrestling.UI.Material.Utils.Recording.OverlayDrawer
{
    public class OlympicOverlayDrawer : BaseOverlayDrawer
    {
        private ScoreScreenViewModel _currentMatch;

        private static int transparency = 255;

        private static SolidBrush redBrush = new SolidBrush(Color.FromArgb(transparency, 205, 45, 45));
        private static SolidBrush blueBrush = new SolidBrush(Color.FromArgb(transparency, 40, 60, 150));
        private static SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(transparency, 255, 255, 255));
        private static SolidBrush yellowBrush = new SolidBrush(Color.FromArgb(transparency, Color.Yellow));
        private static SolidBrush blackBrush = new SolidBrush(Color.FromArgb(transparency, Color.Black));
        private static SolidBrush darkGrayBrush = new SolidBrush(Color.FromArgb(transparency, 70, 70, 70));
        //SolidBrush cyanBrush = new SolidBrush(Color.FromArgb(transparency, Color.Cyan));

        private static int _fontSize = 10;
        private static int _largeSize = 20;

        private static Font mainFont = new Font("Times New Roman", _fontSize, FontStyle.Regular);
        private static Font boldFont = new Font("Times New Roman", _fontSize, FontStyle.Bold);
        private static Font largeBoldFont = new Font("Times New Roman", _largeSize, FontStyle.Bold);

        public override void DrawOverlay(Bitmap frame, long time, ScoreScreenViewModel currentMatch)
        {
            if (currentMatch == null || frame == null) return;

            _currentMatch = currentMatch;

            using (var g = Graphics.FromImage(frame))
            {
                var imageWidth = frame.Width;
                var imageHeight = frame.Height;

                float startX = (float)imageWidth / 60;
                float startY = (float)imageHeight / 40;

                var wr1 = GetStringInUpper(GetFirstStringBySplit(_currentMatch.Wrestler1, ' '));
                var wr2 = GetStringInUpper(GetFirstStringBySplit(_currentMatch.Wrestler2, ' '));

                var ttt = _currentMatch.TickCounter.ToString("m\\:ss");
                string timeValue = TimeSpan.FromMilliseconds(time).ToString("m\\:ss");//_currentMatch.TickCounter.ToString("m\\:ss");
                var pt1 = _currentMatch.Points1.ToString();
                var pt2 = _currentMatch.Points2.ToString();
                var team1 = GetStringInUpper(_currentMatch.Wrestler1TeamName);
                var team2 = GetStringInUpper(_currentMatch.Wrestler2TeamName);

                if (!string.IsNullOrEmpty(team1) && team1.Length > 3)
                {
                    team1 = team1.Substring(0, 3);
                }

                if (!string.IsNullOrEmpty(team2) && team2.Length > 3)
                {
                    team2 = team2.Substring(0, 3);
                }

                int fontSize = imageHeight / 48;
                int largeSize = fontSize * 2;

                /*var mainFont = new Font("Times New Roman", fontSize, FontStyle.Regular);
                var boldFont = new Font("Times New Roman", fontSize, FontStyle.Bold);
                var largeBoldFont = new Font("Times New Roman", largeSize, FontStyle.Bold);*/

                if (fontSize != _fontSize)
                {
                    _fontSize = fontSize;
                    mainFont = new Font("Times New Roman", fontSize, FontStyle.Regular);
                    boldFont = new Font("Times New Roman", fontSize, FontStyle.Bold);
                    largeBoldFont = new Font("Times New Roman", largeSize, FontStyle.Bold);
                }

                var pointsSize = GetRectSizeForText(g, "99", mainFont);

                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var timeSize = GetRectSizeForText(g, timeValue, largeBoldFont);

                var wr1Size = GetRectSizeForText(g, wr1, boldFont);
                var wr2Size = GetRectSizeForText(g, wr2, boldFont);
                var nameSize = wr1Size.Width > wr2Size.Width ? wr1Size : wr2Size;

                if (string.IsNullOrEmpty(wr1) && string.IsNullOrEmpty(wr2))
                {
                    nameSize = GetRectSizeForText(g, "ИВАНОВ", boldFont);
                }

                var groupText = _currentMatch.GroupLabel;
                if (groupText != null && groupText.Length > 17)
                {
                    groupText = groupText.Replace(".", string.Empty);

                    if (groupText.Length > 17)
                    {
                        groupText = groupText.Substring(0, groupText.Length - 2);
                    }
                }

                var groupSize = string.IsNullOrEmpty(groupText) ? GetRectSizeForText(g, "ГРУППА", boldFont) : GetRectSizeForText(g, groupText, boldFont);
                var stageSize = string.IsNullOrEmpty(_currentMatch.RoundName) ? GetRectSizeForText(g, "ФИНАЛ", boldFont) : GetRectSizeForText(g, _currentMatch.RoundName, boldFont);
                var roundSize = string.IsNullOrEmpty(_currentMatch.MatchFullNumber) ? GetRectSizeForText(g, "11", boldFont) : GetRectSizeForText(g, _currentMatch.MatchFullNumber, boldFont);
                var team1Size = GetRectSizeForText(g, team1, boldFont);
                var team2Size = GetRectSizeForText(g, team2, boldFont);
                var teamSize = new SizeF(team1Size.Width > team2Size.Width ? team1Size.Width : team2Size.Width, team1Size.Height > team2Size.Height ? team1Size.Height : team2Size.Height);

                if (_currentMatch.IsTimeout)
                {
                    groupText = "ПЕРЕРЫВ";
                }

                if (roundSize.Width < timeSize.Width) roundSize.Width = timeSize.Width;

                if (groupSize.Width < roundSize.Width + stageSize.Width) groupSize.Width = roundSize.Width + stageSize.Width;
                else stageSize.Width = groupSize.Width - roundSize.Width;

                DrawRectWithStringCenter(g, team1, boldFont, startX, startY, teamSize, darkGrayBrush, whiteBrush);
                DrawRectWithStringCenter(g, team2, boldFont, startX, startY + teamSize.Height, teamSize, darkGrayBrush, whiteBrush);

                DrawRectWithStringCenter(g, groupText, boldFont, imageWidth - startX - (timeSize.Width + groupSize.Width), startY, groupSize, darkGrayBrush, (_currentMatch.IsTimeout ? yellowBrush : whiteBrush));

                DrawRectWithStringCenter(g, _currentMatch.MatchFullNumber, boldFont, imageWidth - startX - (timeSize.Width + groupSize.Width), startY + groupSize.Height, roundSize, darkGrayBrush, yellowBrush);

                DrawRectWithStringCenter(g, _currentMatch.RoundName, boldFont, imageWidth - startX - (timeSize.Width + groupSize.Width) + timeSize.Width, startY + groupSize.Height, stageSize, darkGrayBrush, whiteBrush);

                DrawRectWithStringCenter(g, timeValue, largeBoldFont, imageWidth - startX - timeSize.Width, startY, new SizeF(timeSize.Width, groupSize.Height + groupSize.Height), darkGrayBrush, whiteBrush);

                DrawRectWithStringLeft(g, wr1, boldFont, startX + teamSize.Width, startY, nameSize, redBrush, whiteBrush);
                DrawRectWithStringLeft(g, wr2, boldFont, startX + teamSize.Width, startY + nameSize.Height, nameSize, blueBrush, whiteBrush);

                if (_currentMatch.IsAction1TimerEnabled || _currentMatch.IsAction2TimerEnabled)
                {
                    if (_currentMatch.IsAction1TimerEnabled)
                    {
                        DrawRectWithStringCenter(g, _currentMatch.TickCounterAction1.ToString("ss"), mainFont, startX + teamSize.Width + nameSize.Width + pointsSize.Width, startY, pointsSize, darkGrayBrush, yellowBrush);
                    }
                    else if (_currentMatch.IsAction2TimerEnabled)
                    {
                        DrawRectWithStringCenter(g, _currentMatch.TickCounterAction2.ToString("ss"), mainFont, startX + teamSize.Width + nameSize.Width + pointsSize.Width, startY + pointsSize.Height, pointsSize, darkGrayBrush, yellowBrush);
                    }
                }

                DrawRectWithStringCenter(g, pt1, boldFont, startX + teamSize.Width + nameSize.Width, startY, pointsSize, whiteBrush, blackBrush);
                DrawRectWithStringCenter(g, pt2, boldFont, startX + teamSize.Width + nameSize.Width, startY + pointsSize.Height, pointsSize, whiteBrush, blackBrush);

                int linesWidth = imageHeight / 480;

                using (var whitePen = new Pen(Color.FromArgb(transparency, Color.GhostWhite), linesWidth))
                {
                    g.DrawLine(whitePen, startX + teamSize.Width, startY, startX + teamSize.Width, startY + teamSize.Height * 2);
                    g.DrawLine(whitePen, startX, startY + teamSize.Height, startX + teamSize.Width, startY + pointsSize.Height);
                    g.DrawLine(whitePen, startX + teamSize.Width + nameSize.Width + (float)(pointsSize.Width * 0.1), startY + pointsSize.Height, (float)(startX + teamSize.Width + nameSize.Width + pointsSize.Width - pointsSize.Width * 0.1), startY + pointsSize.Height);

                    g.DrawRectangle(whitePen, startX, startY, teamSize.Width + nameSize.Width + pointsSize.Width, pointsSize.Height + pointsSize.Height);

                    g.DrawRectangle(whitePen, imageWidth - startX - timeSize.Width - groupSize.Width, startY, timeSize.Width + groupSize.Width, groupSize.Height + groupSize.Height);
                    g.DrawLine(whitePen, imageWidth - startX - timeSize.Width - groupSize.Width, startY + groupSize.Height, imageWidth - startX - timeSize.Width, startY + groupSize.Height);
                    g.DrawLine(whitePen, imageWidth - startX - timeSize.Width - groupSize.Width + timeSize.Width, startY + groupSize.Height, imageWidth - startX - timeSize.Width - groupSize.Width + timeSize.Width, startY + groupSize.Height + groupSize.Height);
                    g.DrawLine(whitePen, imageWidth - startX - timeSize.Width, startY, imageWidth - startX - timeSize.Width, startY + groupSize.Height + groupSize.Height);
                }

                if (_currentMatch.Points1 == _currentMatch.Points2 && _currentMatch.Points1 > 0)
                {
                    using (var blackPen = new Pen(Color.Black, linesWidth))
                    {
                        if (_currentMatch.BestActionRed > _currentMatch.BestActionBlue || (_currentMatch.BestActionRed == _currentMatch.BestActionBlue && _currentMatch.IsLastActionRed))
                        {
                            // draw advantage for red
                            g.DrawLine(blackPen, (startX + teamSize.Width + nameSize.Width + (float)(pointsSize.Width * 0.2)), ((startY + pointsSize.Height) - (float)(pointsSize.Height * 0.2)), (float)((startX + teamSize.Width + nameSize.Width + pointsSize.Width) - pointsSize.Width * 0.2), ((startY + pointsSize.Height) - (float)(pointsSize.Height * 0.2)));
                        }
                        else if (_currentMatch.BestActionBlue > _currentMatch.BestActionRed ||
                                 (_currentMatch.BestActionRed == _currentMatch.BestActionBlue &&
                                  !_currentMatch.IsLastActionRed))
                        {
                            // draw advantage for blue
                            g.DrawLine(blackPen, startX + teamSize.Width + nameSize.Width + (float)(pointsSize.Width * 0.2), (startY + pointsSize.Height + pointsSize.Height - (float)(pointsSize.Height * 0.2)), (float)((startX + teamSize.Width + nameSize.Width + pointsSize.Width) - pointsSize.Width * 0.2), ((startY + pointsSize.Height + pointsSize.Height) - (float)(pointsSize.Height * 0.2)));
                        }
                    }
                }

                float pointX = startX + teamSize.Width + nameSize.Width;
                pointX = (float)(pointX - pointX * 0.05);

                DrawWarningDots(g, _currentMatch.Wrestler1WarningsNumber, pointX, startY, nameSize.Height, nameSize.Height / 5);
                DrawWarningDots(g, _currentMatch.Wrestler2WarningsNumber, pointX, startY + nameSize.Height, nameSize.Height, (nameSize.Height / 5));

                /*
                var imgStartPoint = new PointF(400, 200);
                var imgSize = new RectangleF(50, 50, 150, 150);
                DrawImage(g, "Images//RosbosLogo.png", imgStartPoint, imgSize);
                */

                if (_currentMatch.LogoImage != null && !_currentMatch.LogoRectangle.IsEmpty)
                {
                    var rc = _currentMatch.LogoRectangle;

                    switch (_currentMatch.LogoPosition)
                    {
                        case Model.LogoPositionEnum.LEFT_BOTTOM:
                            rc = new RectangleF(
                                rc.Left, 
                                imageHeight - rc.Top - rc.Height, 
                                rc.Width, 
                                rc.Height);
                            break;

                        case Model.LogoPositionEnum.RIGHT_BOTTOM:
                            rc = new RectangleF(
                                imageWidth - rc.Width - rc.Left, 
                                imageHeight - rc.Top - rc.Height, 
                                rc.Width, 
                                rc.Height);
                            break;

                        case Model.LogoPositionEnum.RIGHT_TOP:
                            rc = new RectangleF(
                                imageWidth - rc.Width - rc.Left,
                                rc.Top,
                                rc.Width,
                                rc.Height);
                            break;
                    }

                    g.DrawImage(_currentMatch.LogoImage, rc);
                }

                //redBrush.Dispose();
                //blueBrush.Dispose();
                //whiteBrush.Dispose();
                //blackBrush.Dispose();
                //cyanBrush.Dispose();
                //mainFont.Dispose();
                //boldFont.Dispose();
                //largeBoldFont.Dispose();
                //yellowBrush.Dispose();
                //darkGrayBrush.Dispose();
            }
        }
    }
}