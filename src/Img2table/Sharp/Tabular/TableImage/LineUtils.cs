using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Tabular.TableImage
{
    public class LineUtils
    {
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
                return textBoxes.Any(textBox => line.Y1 > textBox.Top + delta && line.Y2 < textBox.Bottom - delta); // TODO, fix it later
            }
            else if (line.X1 == line.X2)
            {
                return textBoxes.Any(textBox => 
                    line.X1 > textBox.Left + delta && line.X2 < textBox.Right - delta
                        && line.Y1 < textBox.Top + delta && line.Y2 > textBox.Bottom - delta);
            }
            else
            {
                return false;
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
    }
}
