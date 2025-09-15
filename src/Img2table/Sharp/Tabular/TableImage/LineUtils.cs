using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using System.Drawing;

namespace img2table.sharp.Img2table.Sharp.Tabular.TableImage
{
    public class LineUtils
    {
        public static bool IntersectAnyLine(Line vLine, IEnumerable<Line> hLines)
        {
            foreach (var hLine in hLines)
            {
                if (vLine.Y1 <= hLine.Y1 && vLine.Y2 >= hLine.Y1
                    && vLine.X1 >= hLine.X1 && vLine.X1 <= hLine.X2)
                {
                    return true;
                }
            }
            return false;
        }

        public static void RemoveLinesInBox(List<Line> hLines, IEnumerable<TextRect> textBoxes)
        {
            hLines.RemoveAll(line =>
                textBoxes.Any(box =>
                    IsPointInBox(line.X1, line.Y1, box) &&
                    IsPointInBox(line.X2, line.Y2, box)
                )
            );
        }

        private static bool IsPointInBox(int x, int y, Rect box, int deltaX = 4, int deltaY = 2)
        {
            return (x >= box.Left - deltaX) && (x <= box.Right + deltaX)
                && (y >= box.Top - deltaY) && (y <= box.Bottom + deltaY);
        }

        public static bool IntersectTextBoxes(Line line, IEnumerable<TextRect> textBoxes, int delta = 4)
        {
            if (line.Y1 == line.Y2)
            {
                return textBoxes.Any(textBox => 
                    line.Y1 > textBox.Top + delta && line.Y2 < textBox.Bottom - delta
                        && line.X1 < textBox.Left + delta && line.X2 > textBox.Right - delta);
            }
            else if (line.X1 == line.X2)
            {
                return textBoxes.Any(textBox => 
                    line.X1 > textBox.Left + delta && line.X2 < textBox.Right - delta
                        && line.Y1 < textBox.Top + delta && line.Y2 > textBox.Bottom - delta);
            }
            else
            {
                return true; //TODO: should remove not straight lines
                //throw new Exception("Not a straight line " + line.ToString());
            }
        }

        public static bool ContainsTextBox(Rect rect, IEnumerable<TextRect> textBoxes, int delta = 4)
        {
            return textBoxes.Any(textBox =>
                (textBox.Left >= rect.Left - delta) && (textBox.Right <= rect.Right + delta)
                && (textBox.Top >= rect.Top - delta) && (textBox.Bottom <= rect.Bottom + delta)
            );
        }

        public static bool ContainsTextBox(RectangleF rect, IEnumerable<TextRect> textBoxes, int delta = 4)
        {
            return textBoxes.Any(textBox =>
                (textBox.Left >= rect.Left - delta) && (textBox.Right <= rect.Right + delta)
                && (textBox.Top >= rect.Top - delta) && (textBox.Bottom <= rect.Bottom + delta)
            );
        }
    }
}
