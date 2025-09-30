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

        public static void RemoveDuplicateHorizontalLines(List<Line> hLines, IEnumerable<TextRect> textBoxes)
        {
            if (hLines == null || hLines.Count <= 1 || textBoxes == null)
            {
                return;
            }

            var linesToRemove = new List<Line>();
            var sortedHLines = hLines.OrderBy(l => l.Y1).ToList();

            for (int i = 0; i < sortedHLines.Count - 1; i++)
            {
                var currentLine = sortedHLines[i];
                var nextLine = sortedHLines[i + 1];

                bool hasTextBoxBetween = HasTextBoxBetweenLines(currentLine, nextLine, textBoxes);

                if (!hasTextBoxBetween)
                {
                    if (currentLine.Length >= nextLine.Length)
                    {
                        if (!linesToRemove.Contains(nextLine))
                        {
                            linesToRemove.Add(nextLine);
                        }
                    }
                    else
                    {
                        if (!linesToRemove.Contains(currentLine))
                        {
                            linesToRemove.Add(currentLine);
                        }
                    }
                }
            }

            foreach (var lineToRemove in linesToRemove)
            {
                hLines.Remove(lineToRemove);
            }
        }

        private static bool HasTextBoxBetweenLines(Line line1, Line line2, IEnumerable<TextRect> textBoxes)
        {
            var upperLine = line1.Y1 <= line2.Y1 ? line1 : line2;
            var lowerLine = line1.Y1 <= line2.Y1 ? line2 : line1;

            foreach (var textBox in textBoxes)
            {
                if (textBox.Top > upperLine.Y1 && textBox.Bottom < lowerLine.Y1)
                {
                    if (HasHorizontalOverlap(upperLine, lowerLine, textBox))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasHorizontalOverlap(Line line1, Line line2, TextRect textBox)
        {
            var lineMinX = Math.Max(Math.Min(line1.X1, line1.X2), Math.Min(line2.X1, line2.X2));
            var lineMaxX = Math.Min(Math.Max(line1.X1, line1.X2), Math.Max(line2.X1, line2.X2));

            if (lineMinX >= lineMaxX)
            {
                return false;
            }
            return textBox.Left < lineMaxX && textBox.Right > lineMinX;
        }

        public static void RemoveDuplicateVerticalLines(List<Line> vLines, IEnumerable<TextRect> textBoxes)
        {
            if (vLines == null || vLines.Count <= 1 || textBoxes == null)
            {
                return;
            }

            var linesToRemove = new List<Line>();
            var sortedVLines = vLines.OrderBy(l => l.X1).ToList();

            for (int i = 0; i < sortedVLines.Count - 1; i++)
            {
                var currentLine = sortedVLines[i];
                var nextLine = sortedVLines[i + 1];

                bool hasTextBoxBetween = HasTextBoxBetweenVerticalLines(currentLine, nextLine, textBoxes);

                if (!hasTextBoxBetween)
                {
                    if (currentLine.Length >= nextLine.Length)
                    {
                        if (!linesToRemove.Contains(nextLine))
                        {
                            linesToRemove.Add(nextLine);
                        }
                    }
                    else
                    {
                        if (!linesToRemove.Contains(currentLine))
                        {
                            linesToRemove.Add(currentLine);
                        }
                    }
                }
            }

            foreach (var lineToRemove in linesToRemove)
            {
                vLines.Remove(lineToRemove);
            }
        }

        private static bool HasTextBoxBetweenVerticalLines(Line line1, Line line2, IEnumerable<TextRect> textBoxes)
        {
            var leftLine = line1.X1 <= line2.X1 ? line1 : line2;
            var rightLine = line1.X1 <= line2.X1 ? line2 : line1;

            foreach (var textBox in textBoxes)
            {
                if (textBox.Left > leftLine.X1 && textBox.Right < rightLine.X1)
                {
                    if (HasVerticalOverlap(leftLine, rightLine, textBox))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasVerticalOverlap(Line line1, Line line2, TextRect textBox)
        {
            var lineMinY = Math.Max(Math.Min(line1.Y1, line1.Y2), Math.Min(line2.Y1, line2.Y2));
            var lineMaxY = Math.Min(Math.Max(line1.Y1, line1.Y2), Math.Max(line2.Y1, line2.Y2));

            if (lineMinY >= lineMaxY)
            {
                return false;
            }
            return textBox.Top < lineMaxY && textBox.Bottom > lineMinY;
        }
    }
}
