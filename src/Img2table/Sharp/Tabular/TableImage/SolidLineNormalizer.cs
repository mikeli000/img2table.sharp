using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Tabular.TableImage
{
    public sealed class SolidLineNormalizer
    {
        public static (List<Line>, List<Line>) Normalize(List<Line> hSolidLines, List<Line> vSolidLines, IEnumerable<TextRect> textBoxes, Rect tableBox)
        {
            var hLines = hSolidLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();
            var vLines = vSolidLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();

            LineUtils.RemoveLinesInBox(hLines, textBoxes);
            LineUtils.RemoveLinesInBox(vLines, textBoxes);

            ResoveEdges(hLines, vLines, textBoxes, tableBox);

            ResolveHLines(hLines, vLines, textBoxes);
            ResolveVLines(hLines, vLines, textBoxes);

            return (hLines, vLines);
        }

        private static void ResolveVLines(List<Line> hLines, List<Line> vLines, IEnumerable<TextRect> textBoxes, int delta = 4)
        {
            vLines = vLines.OrderBy(l => l.X1).ToList();
            hLines = hLines.OrderBy(l => l.Y1).ToList();

            Line prevLine = null;

            var tops = hLines.Select(l => l.Y1).ToList();
            int topMost = tops.FirstOrDefault();
            int? topSecond = null;
            if (tops.Count() > 3)
            {
                topSecond = tops[1];
            }

            var bottoms = tops.OrderByDescending(p => p);
            int bottomMost = bottoms.FirstOrDefault();

            for (int i = 0; i < vLines.Count - 1; i++)
            {
                var line = vLines[i];
                if (i == 0)
                {
                    prevLine = line;
                    continue;
                }

                if (Math.Abs(line.Y1 - topMost) <= delta)
                {
                    line.Y1 = topMost;
                    prevLine = line;
                }
                else
                {
                    foreach (var top in tops)   
                    {
                        if (Math.Abs(line.Y1 - top) <= delta)
                        {
                            line.Y1 = top;
                            prevLine = line;
                            break;
                        }

                        if (topSecond != null && Math.Abs(line.Y1 - topSecond.Value) <= delta)
                        {
                            line.Y1 = topSecond.Value;
                            break;
                        }

                        int t = top;
                        int b = line.Y1;
                        int l = prevLine.X1;
                        int r = line.X1;
                        if (b > t && r > l)
                        {
                            bool intersected = LineUtils.IntersectTextBoxes(new Line(r, t, r, b), textBoxes, delta);
                            if (!intersected)
                            {
                                bool containBox = LineUtils.ContainsTextBox(Rect.FromLTRB(l, t, r, b), textBoxes, delta);
                                if (containBox)
                                {
                                    line.Y1 = top;
                                    prevLine = line;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (Math.Abs(line.Y2 - bottomMost) <= delta)
                {
                    line.Y2 = bottomMost;
                    prevLine = line;
                }
                else
                {
                    foreach (var bottom in bottoms)
                    {
                        if (Math.Abs(line.Y2 - bottom) <= delta)
                        {
                            line.Y2 = bottom;
                            prevLine = line;
                            break;
                        }
                        int t = line.Y2;
                        int b = bottom;
                        int l = prevLine.X1;
                        int r = line.X1;
                        if (b > t && r > l)
                        {
                            bool intersected = LineUtils.IntersectTextBoxes(new Line(r, t, r, b), textBoxes, delta);
                            if (!intersected)
                            {
                                bool containBox = LineUtils.ContainsTextBox(Rect.FromLTRB(l, t, r, b), textBoxes, delta);
                                if (containBox)
                                {
                                    line.Y2 = bottom;
                                    prevLine = line;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ResolveHLines(IList<Line> hLines, IList<Line> vLines, IEnumerable<TextRect> textBoxes, int delta = 4)
        {
            hLines = hLines.OrderBy(l => l.Y1).ToList();
            vLines = vLines.OrderBy(l => l.X1).ToList();

            Line prevLine = null;
            int left = vLines.First().X1;
            int right = vLines.Last().X1;

            var leftMost = textBoxes.Min(rect => rect.Left);
            var rightMost = textBoxes.Max(rect => rect.Right);

            for (int i = 0; i < hLines.Count - 1; i++)
            {
                var line = hLines[i];
                if (i == 0)
                {
                    prevLine = line;
                    continue;
                }

                if (line.X1 <= leftMost || Math.Abs(line.X1 - leftMost) <= delta || line.X1 < left || Math.Abs(line.X1 - left) <= delta)
                {
                    line.X1 = left;
                    prevLine = line;
                }
                else
                {
                    int l = left;
                    int r = line.X1;
                    int t = prevLine.Y1;
                    int b = line.Y1;
                    bool intersected = LineUtils.IntersectTextBoxes(new Line(l, b, r, b), textBoxes, delta);
                    if (!intersected)
                    {
                        bool containBoxOnTop = LineUtils.ContainsTextBox(Rect.FromLTRB(l, t, r, b), textBoxes, delta);
                        bool containBoxOnBottom = true;
                        if (i < hLines.Count - 1)
                        {
                            var nextLine = hLines[i + 1];
                            containBoxOnBottom = LineUtils.ContainsTextBox(Rect.FromLTRB(l, b, r, nextLine.Y1), textBoxes, delta);
                        }

                        if (containBoxOnTop && containBoxOnBottom)
                        {
                            line.X1 = left;
                            prevLine = line;
                        }
                    }
                }

                if (line.X2 > rightMost || Math.Abs(line.X2 - rightMost) <= delta || line.X2 > right || Math.Abs(line.X2 - right) <= delta)
                {
                    line.X2 = right;
                    prevLine = line;
                }
                else
                {
                    int l = line.X2;
                    int r = right;
                    int t = prevLine.Y1;
                    int b = line.Y1;
                    bool intersected = LineUtils.IntersectTextBoxes(new Line(l, b, r, b), textBoxes, delta);
                    if (!intersected)
                    {
                        bool containBoxOnTop = LineUtils.ContainsTextBox(Rect.FromLTRB(l, t, r, b), textBoxes, delta);
                        bool containBoxOnBottom = true;
                        if (i < hLines.Count - 1)
                        {
                            var nextLine = hLines[i + 1];
                            containBoxOnBottom = LineUtils.ContainsTextBox(Rect.FromLTRB(l, b, r, nextLine.Y1), textBoxes, delta);
                        }
                        if (containBoxOnTop && containBoxOnBottom)
                        {
                            line.X2 = right;
                            prevLine = line;
                        }
                    }
                }
            }
        }

        private static void ResoveEdges(IList<Line> hLines, IList<Line> vLines, IEnumerable<TextRect> textBoxes, Rect tableBox)
        {
            ResolveTop(hLines, vLines, textBoxes, tableBox);
            ResolveBottom(hLines, vLines, textBoxes, tableBox);
            ResolveLeft(hLines, vLines, textBoxes, tableBox);
            ResolveRight(hLines, vLines, textBoxes, tableBox);

            var leftEdge = vLines[0];
            var rightEdge = vLines[vLines.Count - 1];
            var topEdge = hLines[0];
            var bottomEdge = hLines[hLines.Count - 1];

            leftEdge.Y1 = topEdge.Y1;
            leftEdge.Y2 = bottomEdge.Y1;

            rightEdge.Y1 = topEdge.Y1;
            rightEdge.Y2 = bottomEdge.Y1;

            topEdge.X1 = leftEdge.X1;
            topEdge.X2 = rightEdge.X1;

            bottomEdge.X1 = leftEdge.X1;
            bottomEdge.X2 = rightEdge.X1;   
            }

        private static void ResolveLeft(IList<Line> hLines, IList<Line> vLines, IEnumerable<TextRect> textBoxes, Rect tableBox)
        {
            if (vLines == null)
            {
                throw new ArgumentNullException(nameof(vLines));
            }

            var tableLeftLine = new Line(tableBox.Left, tableBox.Top, tableBox.Left, tableBox.Bottom);
            if (vLines.Count == 0)
            {
                vLines.Add(tableLeftLine);
                return;
            }

            var leftLines = FindLeftEdgeLines(vLines);
            var leftLine = leftLines.OrderBy(l => l.X1).First();
            int leftX = leftLine.X1;

            var textBetweenLeftLines = textBoxes.Where(textBox =>
            {
                double midX = (textBox.Left + textBox.Right) / 2.0;
                return midX > Math.Min(leftX, tableBox.Left) && midX < Math.Max(leftX, tableBox.Left);
            });

            if (textBetweenLeftLines.Any())
            {
                vLines.Insert(0, tableLeftLine);
                AlignLeftHLines(hLines, leftX, tableBox.Left, true, textBetweenLeftLines);
            }
            else
            {
                leftLine = new Line(leftX, tableBox.Top, leftX, tableBox.Bottom);
                foreach (var line in leftLines)
                {
                    vLines.Remove(line);
                }
                vLines.Insert(0, leftLine);
                AlignLeftHLines(hLines, leftX, leftX, false);
            }
        }

        private static void ResolveBottom(IList<Line> hLines, IList<Line> vLines, IEnumerable<TextRect> textBoxes, Rect tableBox)
        {
            if (hLines == null)
            {
                throw new ArgumentNullException(nameof(hLines));
            }

            var tableBottomLine = new Line(tableBox.Left, tableBox.Bottom, tableBox.Right, tableBox.Bottom);
            if (hLines.Count == 0)
            {
                hLines.Add(tableBottomLine);
                return;
            }

            var bottomLines = FindBottomEdgeLines(hLines);
            var bottomLine = bottomLines.OrderByDescending(l => l.Y1).First();
            int bottomY = bottomLine.Y1;
            var textBetweenBottomLines = textBoxes.Where(textBox =>
            {
                double midY = (textBox.Top + textBox.Bottom) / 2.0;
                return midY > Math.Min(bottomY, tableBox.Bottom) && midY < Math.Max(bottomY, tableBox.Bottom);
            });

            if (textBetweenBottomLines.Any())
            {
                hLines.Add(tableBottomLine);
                AlignBottomVLines(vLines, bottomY, tableBox.Bottom, true, textBetweenBottomLines);
            }
            else
            {
                bottomLine = new Line(tableBox.Left, bottomY, tableBox.Right, bottomY);
                foreach (var line in bottomLines)
                {
                    hLines.Remove(line);
                }
                hLines.Add(bottomLine);
                AlignBottomVLines(vLines, bottomY, bottomY, false);
            }
        }

        private static void ResolveRight(IList<Line> hLines, IList<Line> vLines, IEnumerable<TextRect> textBoxes, Rect tableBox)
        {
            if (vLines == null)
            {
                throw new ArgumentNullException(nameof(vLines));
            }

            var tableRightLine = new Line(tableBox.Right, tableBox.Top, tableBox.Right, tableBox.Bottom);
            if (vLines.Count == 0)
            {
                vLines.Add(tableRightLine);
                return;
            }

            var rightLines = FindRightEdgeLines(vLines);
            var rightLine = rightLines.OrderByDescending(l => l.X2).First();
            int rightX = rightLine.X2;

            var textBetweenRightLines = textBoxes.Where(textBox =>
            {
                double midX = (textBox.Left + textBox.Right) / 2.0;
                return midX > Math.Min(rightX, tableBox.Right) && midX < Math.Max(rightX, tableBox.Right);
            });

            if (textBetweenRightLines.Any())
            {
                vLines.Add(tableRightLine);
                AlignRightHLines(hLines, rightX, tableBox.Right, true, textBetweenRightLines);
            }
            else
            {
                rightLine = new Line(rightX, tableBox.Top, rightX, tableBox.Bottom);
                foreach (var line in rightLines)
                {
                    vLines.Remove(line);
                }
                vLines.Add(rightLine);
                AlignRightHLines(hLines, rightX, rightX, false);
            }
        }

        private static void ResolveTop(IList<Line> hLines, IList<Line> vLines, IEnumerable<TextRect> textBoxes, Rect tableBox)
        {
            if (hLines == null)
            {
                throw new ArgumentNullException(nameof(hLines));
            }
            
            var tableTopLine = new Line(tableBox.Left, tableBox.Top, tableBox.Right, tableBox.Top);
            if (hLines.Count == 0)
            {
                hLines.Add(tableTopLine);
                return;
            }

            var topLines = FindTopEdgeLines(hLines);
            var topLine = topLines.OrderBy(l => l.Y1).First();
            int topY = topLine.Y1;

            var textBetweenTopLines = textBoxes.Where(textBox =>
            {
                double midY = (textBox.Top + textBox.Bottom) / 2.0;
                return midY > Math.Min(topY, tableBox.Top) && midY < Math.Max(topY, tableBox.Top);
            });

            if (textBetweenTopLines.Any())
            {
                hLines.Insert(0, tableTopLine);
                AlignTopVLines(vLines, topY, tableBox.Top, true, textBetweenTopLines);
            }
            else
            {
                topLine = new Line(tableBox.Left, topY, tableBox.Right, topY);
                foreach (var line in topLines)
                {
                    hLines.Remove(line);
                }
                hLines.Insert(0, topLine);
                AlignTopVLines(vLines, topY, topY, false);
            }
        }

        private static void AlignTopVLines(IList<Line> vLines, int topY, int alignmentY, bool isNewTopLine, IEnumerable<TextRect> textRects = null, int delta = 4)
        {
            if (vLines == null || vLines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < vLines.Count; i++)
            {
                var line = vLines[i];
                if (Math.Abs(line.Y1 - topY) < delta)
                {
                    if (isNewTopLine && textRects?.Any() == true)
                    {
                        var extendedLine = new Line(line.X1, alignmentY, line.X2, line.Y2);
                        var intersected = LineUtils.IntersectTextBoxes(extendedLine, textRects, delta);
                        if (!intersected)
                        {
                            line.Y1 = alignmentY;
                        }
                    }
                    else
                    {
                        line.Y1 = alignmentY;
                    }
                }
            }
        }

        private static void AlignBottomVLines(IList<Line> vLines, int bottomY, int alignmentY, bool isNewBottomLine, IEnumerable<TextRect> textRects = null, int delta = 4)
        {
            if (vLines == null || vLines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < vLines.Count; i++)
            {
                var line = vLines[i];
                if (Math.Abs(line.Y2 - bottomY) < delta)
                {
                    if (isNewBottomLine && textRects?.Any() == true)
                    {
                        var extendedLine = new Line(line.X1, line.Y1, line.X2, alignmentY);
                        var intersected = LineUtils.IntersectTextBoxes(extendedLine, textRects, delta);
                        if (!intersected)
                        {
                            line.Y2 = alignmentY;
                        }
                    }
                    else
                    {
                        line.Y2 = alignmentY;
                    }
                }
            }
        }

        private static void AlignLeftHLines(IList<Line> hLines, int leftX, int alignmentX, bool isNewLeftEdge, IEnumerable<TextRect> textBetweenLeftLines = null, int delta = 4)
        {
            if (hLines == null || hLines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < hLines.Count; i++)
            {
                var line = hLines[i];
                if (Math.Abs(line.X1 - leftX) < delta)
                {
                    if (isNewLeftEdge && textBetweenLeftLines?.Any() == true)
                    {
                        var extendedLine = new Line(alignmentX, line.Y1, line.X2, line.Y2);
                        var intersected = LineUtils.IntersectTextBoxes(extendedLine, textBetweenLeftLines, delta);
                        if (!intersected)
                        {
                            line.X1 = alignmentX;
                        }
                    }
                    else
                    {
                        line.X1 = alignmentX;
                    }
                }
            }
        }

        private static void AlignRightHLines(IList<Line> hLines, int rightX, int alignmentX, bool isNewRightEdge, IEnumerable<TextRect> textBetweenLeftLines = null, int delta = 4)
        {
            if (hLines == null || hLines.Count == 0)
            {
                return;
            }

            for (int i = 0; i < hLines.Count; i++)
            {
                var line = hLines[i];
                if (Math.Abs(line.X2 - rightX) < delta)
                {
                    if (isNewRightEdge && textBetweenLeftLines?.Any() == true)
                    {
                        var extendedLine = new Line(line.X1, line.Y1, alignmentX, line.Y2);
                        var intersected = LineUtils.IntersectTextBoxes(extendedLine, textBetweenLeftLines, delta);
                        if (!intersected)
                        {
                            line.X2 = alignmentX;
                        }
                    }
                    else
                    {
                        line.X2 = alignmentX;
                    }
                }
            }
        }

        private static IList<Line> FindTopEdgeLines(IList<Line> hLines, int delta = 4)
        {
            var ascLines = hLines.OrderBy(l => l.Y1).ToList();
            var topY = ascLines.First().Y1;
            var topLines = new List<Line>();
            foreach (var line in ascLines)
            {
                if (Math.Abs(line.Y1 - topY) <= delta)
                {
                    topLines.Add(line);
                }
            }
            return topLines;
        }

        private static IList<Line> FindBottomEdgeLines(IList<Line> hLines, int delta = 4)
        {
            var descLines = hLines.OrderByDescending(l => l.Y2).ToList();
            var bottomY = descLines.First().Y2;
            var bottomLines = new List<Line>();
            foreach (var line in descLines)
            {
                if (Math.Abs(line.Y2 - bottomY) <= delta)
                {
                    bottomLines.Add(line);
                }
            }
            return bottomLines;
        }

        private static IList<Line> FindLeftEdgeLines(IList<Line> vLines, int delta = 4)
        {
            var ascLines = vLines.OrderBy(l => l.X1).ToList();
            var leftX = ascLines.First().X1;
            var leftLines = new List<Line>();
            foreach (var line in ascLines)
            {
                if (Math.Abs(line.X1 - leftX) <= delta)
                {
                    leftLines.Add(line);
                }
            }
            return leftLines;
        }

        private static IList<Line> FindRightEdgeLines(IList<Line> vLines, int delta = 4)
        {
            var descLines = vLines.OrderByDescending(l => l.X2).ToList();
            var rightX = descLines.First().X2;
            var rightLines = new List<Line>();
            foreach (var line in descLines)
            {
                if (Math.Abs(line.X2 - rightX) <= delta)
                {
                    rightLines.Add(line);
                }
            }
            return rightLines;
        }
    }
}
