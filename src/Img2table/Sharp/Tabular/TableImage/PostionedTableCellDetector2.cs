using Img2table.Sharp.Tabular.TableImage.TableElement;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Tabular.TableImage
{
    internal class PostionedTableCellDetector2
    {
        public static bool TryDetectLines2(List<Line> solidHLines, List<Line> solidVLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, double charWidth, out List<Line> detectedHLines, out List<Line> detectedVLines)
        {
            detectedHLines = new List<Line>();
            detectedHLines.AddRange(solidHLines);
            detectedVLines = new List<Line>();
            detectedVLines.AddRange(solidVLines);

            var columnPositions = DetectColumnPositions(textBoxes, tableBbox, charWidth: 1);
            var finalVPos = MergeColumnPositions(detectedVLines, columnPositions, textBoxes);

            detectedHLines = DetecteHorLines2(detectedHLines, tableBbox, textBoxes, finalVPos);
            var topLine = detectedHLines[0];
            Line? secLine = detectedHLines.Count > 2 ? detectedHLines[1] : null;

            //var columnPositions = DetectColumnPositions(textBoxes, tableBbox, charWidth);
            //var finalVPos = MergeColumnPositions(detectedVLines, columnPositions, textBoxes);
            ExtendVLines(finalVPos, detectedHLines, detectedVLines, textBoxes, tableBbox);
            if (secLine != null)
            {
                var l_sec = secLine.X1;
                var r_sec = secLine.X2;

                if (l_sec > tableBbox.Left)
                {
                    var exLeftLine = new Line(tableBbox.Left, secLine.Y1, l_sec, secLine.Y2);
                    if (!LineUtils.IntersectTextBoxes(exLeftLine, textBoxes, -4)) // left extension, strict mode
                    {
                        secLine.X1 = tableBbox.Left;
                    }
                }
                if (r_sec < tableBbox.Right)
                {
                    var exRightLine = new Line(r_sec, secLine.Y1, tableBbox.Right, secLine.Y2);
                    if (!LineUtils.IntersectTextBoxes(exRightLine, textBoxes, -4)) // left extension, strict mode
                    {
                        secLine.X2 = tableBbox.Right;
                    }
                }

                var tbodyBbox = new Rect
                {
                    Left = secLine.X1,
                    Top = secLine.Y1,
                    Width = secLine.X2 - secLine.X1,
                    Height = tableBbox.Bottom - secLine.Y2
                };
                var tBodyTextBoxes = textBoxes.Where(textBox =>
                {
                    double midX = (textBox.Left + textBox.Right) / 2.0;
                    double midY = (textBox.Top + textBox.Bottom) / 2.0;
                    return midY > Math.Min(tbodyBbox.Top, tbodyBbox.Bottom) && midY < Math.Max(tbodyBbox.Top, tbodyBbox.Bottom)
                            && midX > Math.Min(tbodyBbox.Left, tbodyBbox.Right) && midX < Math.Max(tbodyBbox.Left, tbodyBbox.Right);
                }).ToList();

                if (tBodyTextBoxes.Count <= 0)
                {
                    return true;
                }

                var secColumnPositions = DetectBodyVerLines(tBodyTextBoxes, tbodyBbox, columnPositions, charWidth);
                int topVLineCount = CountTopVLine(detectedVLines, topLine.Y1, secLine.X1, secLine.X2);

                if (topVLineCount != secColumnPositions.Count())
                {
                    var secFinalVPos = MergeColumnPositions(detectedVLines, secColumnPositions, tBodyTextBoxes);
                    secFinalVPos = secFinalVPos.OrderBy(p => p).ToList();

                    ExtendVLines(secFinalVPos, detectedHLines, detectedVLines, tBodyTextBoxes, tbodyBbox);
                }
            }

            return true;
        }

        private static List<Line> DetecteHorLines2(List<Line> solidHLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, List<int> vPoses)
        {
            if (textBoxes == null || textBoxes.Count() <= 0)
            {
                return solidHLines;
            }

            var groupLines = DetecteHorLinesBetweenTextBox(textBoxes, solidHLines, tableBbox, vPoses, minGap: 12, removeOneBoxLine: true, removeLowcaseStartedLine: true);
            ResolveTopBottomBorder(groupLines, tableBbox, textBoxes);
            return groupLines;
        }

        private static List<Line> DetecteHorLinesBetweenTextBox(IEnumerable<TextRect> rects, List<Line> solidHLines, Rect tableBbox, List<int> vPoses, int minGap = 12, bool removeOneBoxLine = false, bool removeLowcaseStartedLine = false)
        {
            var lines = new List<List<TextRect>>();
            if (rects.Count() == 0)
            {
                return solidHLines;
            }
            if (rects.Count() == 1)
            {
                lines.Add(rects.ToList());
                return solidHLines;
            }

            var copy = rects.OrderBy(c => c.Top).ToList();
            int top = copy.Min(c => c.Top);
            int bottom = copy.Max(c => c.Bottom);
            for (int i = top + 1; i <= bottom; i++)
            {
                var line = new List<TextRect>();
                var temp = new List<TextRect>();
                foreach (var rect in copy)
                {
                    if (i >= rect.Top && i <= rect.Bottom)
                    {
                        temp.Add(rect);
                    }
                }

                if (temp.Count() > 0)
                {
                    copy.RemoveAll(c => temp.Contains(c));

                    int lineTop = temp.Min(c => c.Top);
                    int lineBottom = temp.Max(c => c.Bottom);
                    foreach (var c in copy)
                    {
                        if (InLine(lineTop, lineBottom, c.Top, 2, solidHLines))
                        {
                            temp.Add(c);
                            lineBottom = Math.Max(lineBottom, c.Bottom);
                        }
                    }

                    copy.RemoveAll(c => temp.Contains(c));
                    line.AddRange(temp);
                    lines.Add(line.OrderBy(c => c.Left).ToList());

                    if (copy.Count() > 0)
                    {
                        i = copy.Min(c => c.Top) + 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            vPoses = vPoses.OrderBy(v => v).ToList();
            List<Line> joinLines = new List<Line>();
            joinLines.AddRange(solidHLines);
            List<TextRect> lastLine = null;
            int thick_line_delta = 2;
            List<List<TextRect>> orphanLines = new List<List<TextRect>>();
            for (int i = 0; i < lines.Count(); i++)
            {
                var t_line = lines[i];
                if (i == 0)
                {
                    lastLine = t_line;
                    continue;
                }

                var currTop = t_line.Min(c => c.Top);
                var lastBottom = lastLine.Max(c => c.Bottom);

                bool isOneLine = false;
                var hasSolidLine = solidHLines.Any(l => l.Y1 >= lastBottom - thick_line_delta && l.Y1 <= currTop + thick_line_delta);
                if (!hasSolidLine)
                {
                    //var cellLines = GroupTextRectsInCols(vPoses, lastLine, t_line);
                    //isOneLine = IsOneLine(cellLines);
                    
                    if (isOneLine)
                    {
                        lastLine.AddRange(t_line);
                    }
                    else
                    {
                        var left = tableBbox.Left;
                        var right = tableBbox.Right;
                        var middle = (lastBottom + currTop) / 2;
                        var newLine = new Line(left, middle, right, middle);
                        joinLines.Add(newLine);
                    }
                }

                if (!isOneLine)
                {
                    lastLine = t_line;
                }
            }

            return joinLines;
        }

        private static List<(int, int, List<TextRect>, List<TextRect>)> GroupTextRectsInCols(List<int> vPoses, List<TextRect> topLine, List<TextRect> bottomLine)
        {
            if (vPoses == null || vPoses.Count < 2 || (topLine == null && bottomLine == null))
            {
                return new List<(int, int, List<TextRect>, List<TextRect>)>();
            }

            var intervals = new List<(int start, int end, List<TextRect> topTextRects, List<TextRect> bottomTextRects)>();
            for (int i = 0; i < vPoses.Count - 1; i++)
            {
                intervals.Add((vPoses[i], vPoses[i + 1], new List<TextRect>(), new List<TextRect>()));
            }

            if (topLine != null)
            {
                foreach (var textRect in topLine)
                {
                    int centerX = (textRect.Left + textRect.Right) / 2;
                    for (int i = 0; i < intervals.Count; i++)
                    {
                        var interval = intervals[i];
                        if (centerX > interval.start && centerX < interval.end)
                        {
                            interval.topTextRects.Add(textRect);
                            break;
                        }
                    }
                }
            }

            if (bottomLine != null)
            {
                foreach (var textRect in bottomLine)
                {
                    int centerX = (textRect.Left + textRect.Right) / 2;
                    for (int i = 0; i < intervals.Count; i++)
                    {
                        var interval = intervals[i];
                        if (centerX >= interval.start && centerX < interval.end)
                        {
                            interval.bottomTextRects.Add(textRect);
                            break;
                        }
                    }
                }
            }

            return intervals;
        }

        private static bool IsOneLine(List<(int, int, List<TextRect>, List<TextRect>)> cellTextRects)
        {
            bool gridSame = true;
            foreach (var cell in cellTextRects)
            {
                if (cell.Item3.Count() != cell.Item4.Count())
                {
                    gridSame = false;
                }
            }
            if (gridSame)
            {
                return false;
            }

            foreach (var cell in cellTextRects)
            {
                if (cell.Item3.Count() <= 0 || cell.Item4.Count() <= 0)
                {
                    continue;
                }

                var topLine = cell.Item3;
                var bottomLine = cell.Item4;
                var avgTextHeight = CalculateAverageLineHeight(new List<List<TextRect>> { topLine, bottomLine });

                var currTop = bottomLine.Min(c => c.Top);
                var lastBottom = topLine.Max(c => c.Bottom);

                if (currTop - lastBottom < avgTextHeight * 2 / 3d)
                {
                    return true; // once merged, all merged
                }
            }

            return false;
        }

        private static double CalculateAverageLineHeight(List<List<TextRect>> lines)
        {
            if (lines == null || lines.Count() == 0)
            {
                return 0;
            }
            var heights = new List<int>();
            foreach (var line in lines)
            {
                foreach (var rect in line)
                {
                    heights.Add(rect.Height);
                }
            }
            if (heights.Count() == 0)
            {
                return 0;
            }
            return heights.Average();
        }

        private static bool InLine(int lineTop, int lineBottom, int currTop, int minGap, List<Line> solidHLines)
        {
            if (currTop >= lineTop && currTop <= lineBottom)
            {
                return true;
            }

            if (currTop > lineBottom && currTop - lineBottom <= minGap)
            {
                foreach (var line in solidHLines)
                {
                    if (line.Y1 >= lineBottom && line.Y1 <= currTop)
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        private static List<int> DetectColumnPositions(IEnumerable<TextRect> textBoxes, Rect tableRect, double charWidth)
        {
            var columnPositions = ScanForGapsBetweenBoxes(textBoxes, charWidth * 1.5);
            columnPositions.Insert(0, tableRect.Left);
            columnPositions.Insert(columnPositions.Count, tableRect.Right);

            return columnPositions;
        }

        private static List<int> ScanForGapsBetweenBoxes(IEnumerable<TextRect> textBoxes, double minGapW)
        {
            var gaps = new List<int>();
            if (textBoxes == null || textBoxes.Count() == 0)
            {
                return gaps;
            }

            int step = 1;
            var textBoxesCopy = new List<TextRect>(textBoxes);
            int minX = textBoxesCopy.Min(r => r.Left);
            int maxX = textBoxesCopy.Max(r => r.Right);

            int currentX = minX + 1;
            while (currentX < maxX)
            {
                if (TryGetIntersectingBox(currentX, textBoxesCopy, out var intersectingBox))
                {
                    currentX = intersectingBox.Right + 1;
                    textBoxesCopy.RemoveAll(box => box.Right <= currentX);
                }
                else
                {
                    int gapStart = currentX;

                    while (currentX < maxX)
                    {
                        if (TryGetIntersectingBox(currentX, textBoxesCopy, out intersectingBox))
                        {
                            break;
                        }
                        else
                        {
                            currentX += step;
                        }
                    }

                    if (currentX == maxX - 1)
                    {
                        break;
                    }

                    int gapEnd = currentX - 1;
                    int gapWidth = gapEnd - gapStart + 1;
                    if (gapWidth >= minGapW)
                    {
                        int gapCenter = (gapStart + gapEnd) / 2;
                        gaps.Add(gapCenter);
                    }

                    currentX = intersectingBox.Right + 1;
                    textBoxesCopy.RemoveAll(box => box.Right <= currentX);
                }
            }

            return gaps;
        }


        private static bool TryGetIntersectingBox(int x, List<TextRect> textBoxes, out Rect intersectBox)
        {
            intersectBox = default;
            foreach (var box in textBoxes)
            {
                if (x >= box.Left && x <= box.Right)
                {
                    intersectBox = box;
                    return true;
                }
            }
            return false;
        }

        private static List<int> MergeColumnPositions(List<Line> srcVLines, List<int> detectVPos, IEnumerable<TextRect> textBoxes)
        {
            var srcPos = srcVLines.Select(line => line.X1).Distinct().OrderBy(p => p).ToList();
            if (srcPos.Count() >= detectVPos.Count())
            {
                return srcPos;
            }

            var allPositions = srcPos.Concat(detectVPos)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            int minGap = 4;
            List<int> vPos = new List<int>();
            for (int i = 1; i < allPositions.Count; i++)
            {
                int currPoint = allPositions[i];
                int prePoint = vPos.Count <= 0 ? allPositions[i - 1] : vPos.Last();

                Line currVLine = null;
                Line preVLine = null;
                if (srcPos.Contains(currPoint))
                {

                    currVLine = srcVLines.FirstOrDefault(line => line.X1 == currPoint);
                }
                if (srcPos.Contains(prePoint))
                {
                    preVLine = srcVLines.FirstOrDefault(line => line.X1 == prePoint);
                }

                if (currVLine != null && preVLine != null)
                {
                    if (i == 1)
                    {
                        vPos.Add(prePoint);
                    }
                    vPos.Add(currPoint);
                    continue;
                }

                if (currVLine == null && preVLine == null)
                {
                    if (i == 1)
                    {
                        vPos.Add(prePoint);
                    }
                    else
                    {
                        if (vPos.Count > 0 && vPos.Last() != prePoint)
                        {
                            vPos.Add(prePoint);
                        }
                    }
                    vPos.Add(currPoint);
                    continue;
                }

                Rect? gapRect = null;
                if (currVLine != null && preVLine == null)
                {
                    gapRect = new Rect(prePoint, currVLine.Y1, currPoint - prePoint, currVLine.Height);
                    if (gapRect.Value.Width < minGap)
                    {
                        vPos.Add(currPoint);
                        continue;
                    }

                    bool hasTextBox = textBoxes.Any(box =>
                        box.Left >= gapRect.Value.Left &&
                        box.Right <= gapRect.Value.Right &&
                        box.Top >= gapRect.Value.Top &&
                        box.Bottom <= gapRect.Value.Bottom);
                    if (hasTextBox)
                    {
                        if (i == 1)
                        {
                            vPos.Add(prePoint);
                        }
                        vPos.Add(currPoint);
                    }
                    else
                    {
                        if (vPos.Count > 0)
                        {
                            vPos.RemoveAt(vPos.Count() - 1);
                        }
                        vPos.Add(currPoint);
                    }
                }
                else if (currVLine == null && preVLine != null)
                {
                    gapRect = new Rect(prePoint, preVLine.Y1, currPoint - prePoint, preVLine.Height);
                    if (gapRect.Value.Width < minGap)
                    {
                        vPos.Add(prePoint);
                        continue;
                    }

                    bool hasTextBox = textBoxes.Any(box =>
                        box.Left >= gapRect.Value.Left &&
                        box.Right <= gapRect.Value.Right &&
                        box.Top >= gapRect.Value.Top &&
                        box.Bottom <= gapRect.Value.Bottom);
                    if (hasTextBox)
                    {
                        if (i == 1)
                        {
                            vPos.Add(prePoint);
                        }
                        vPos.Add(currPoint);
                    }
                    else
                    {
                        if (vPos.Count > 0)
                        {
                            if (vPos.Last() != prePoint)
                            {
                                vPos.Add(prePoint);
                            }
                        }
                        else
                        {
                            vPos.Add(prePoint);
                        }
                    }
                }
            }

            if (vPos.Count > 0)
            {
                vPos = vPos.Distinct().OrderBy(p => p).ToList();
            }
            return vPos;
        }

        private static void ExtendVLines(List<int> finalVPos, List<Line> hLines, List<Line> vLines, IEnumerable<TextRect> textBoxes, Rect tableBbox)
        {
            if (finalVPos == null)
            {
                return;
            }
            var textBoxList = textBoxes.ToList();

            var bottomLine = hLines.Max(ll => ll.Y2);
            var bottomTable = tableBbox.Bottom;

            if (bottomLine < bottomTable)
            {
                var intersectingTextBoxes = textBoxList.Where(textBox =>
                            (textBox.Top + textBox.Bottom) / 2 >= bottomLine && (textBox.Top + textBox.Bottom) / 2 <= bottomTable
                        ).ToList();
                if (intersectingTextBoxes.Any())
                {
                    bottomLine = bottomTable;
                }
            }

            var topLine = hLines.Min(ll => ll.Y1);
            var topTable = tableBbox.Top;
            if (topLine > topTable)
            {
                var intersectingTextBoxes = textBoxList.Where(textBox =>
                            (textBox.Top + textBox.Bottom) / 2 <= topLine && (textBox.Top + textBox.Bottom) / 2 >= topTable
                        ).ToList();
                if (intersectingTextBoxes.Any())
                {
                    topLine = topTable;
                }
            }

            var delta = 2;
            foreach (var p in finalVPos)
            {
                var vLine = vLines.FirstOrDefault(line => line.X1 == p);
                if (vLine != null)
                {
                    if (vLine.Y2 < bottomLine)
                    {
                        var intersectingTextBoxes = textBoxList.Where(textBox =>
                            textBox.Left + delta <= p && textBox.Right - delta >= p && textBox.Top > vLine.Y1
                        ).ToList();

                        if (intersectingTextBoxes.Any())
                        {
                            var nearestTextBox = intersectingTextBoxes.OrderBy(tb => tb.Top).First();

                            var nearestHLine = hLines
                                .Where(hLine => hLine.Y1 <= nearestTextBox.Top && hLine.Y1 >= vLine.Y2)
                                .OrderByDescending(hLine => hLine.Y1)
                                .FirstOrDefault();

                            if (nearestHLine != null)
                            {
                                vLine.Y2 = nearestHLine.Y1;
                            }
                            else
                            {
                                vLine.Y2 = nearestTextBox.Top - nearestTextBox.Height / 2;
                            }
                        }
                        else
                        {
                            vLine.Y2 = bottomLine;
                        }
                    }

                    if (vLine.Y1 > topLine)
                    {
                        var intersectingTextBoxes = textBoxList.Where(textBox =>
                            textBox.Left + delta <= p && textBox.Right - delta >= p && textBox.Bottom < vLine.Y2
                        ).ToList();
                        if (intersectingTextBoxes.Any())
                        {
                            var nearestTextBox = intersectingTextBoxes.OrderByDescending(tb => tb.Bottom).First();
                            var nearestHLine = hLines
                                .Where(hLine => hLine.Y1 >= nearestTextBox.Bottom && hLine.Y1 <= vLine.Y1)
                                .OrderBy(hLine => hLine.Y1)
                                .FirstOrDefault();
                            if (nearestHLine != null)
                            {
                                vLine.Y1 = nearestHLine.Y1;
                            }
                            else
                            {
                                vLine.Y1 = nearestTextBox.Bottom + nearestTextBox.Height / 2;
                            }
                        }
                        else
                        {
                            vLine.Y1 = topLine;
                        }
                    }
                }
                else
                {
                    var newLine = new Line(p, tableBbox.Top, p, bottomLine);
                    vLines.Add(newLine);
                }
            }
        }

        private static List<int> DetectBodyVerLines(IEnumerable<TextRect> ocrBoxes, Rect bodyRect, List<int> topColumnPositions, double charWidth)
        {
            var bodyBoxes = ocrBoxes.Where(box => IsContains(box, bodyRect)).ToList();
            var bodyColumnPositions = DetectColumnPositions(bodyBoxes, bodyRect, charWidth);

            var validBodyPositions = new List<int>();
            foreach (var pos in bodyColumnPositions)
            {
                if (topColumnPositions.Contains(pos))
                {
                    continue;
                }

                int leftNeighbor = -1;
                int rightNeighbor = -1;
                for (int i = 0; i < topColumnPositions.Count; i++)
                {
                    if (topColumnPositions[i] < pos)
                    {
                        leftNeighbor = topColumnPositions[i];
                    }
                    else if (topColumnPositions[i] > pos && rightNeighbor == -1)
                    {
                        rightNeighbor = topColumnPositions[i];
                        break;
                    }
                }

                bool hasBoxInLeftRegion = false;
                bool hasBoxInRightRegion = false;
                foreach (var box in bodyBoxes)
                {
                    if (leftNeighbor != -1)
                    {
                        if (box.X >= leftNeighbor && box.X + box.Width <= pos)
                        {
                            hasBoxInLeftRegion = true;
                        }
                    }

                    if (rightNeighbor != -1)
                    {
                        if (box.X >= pos && box.X + box.Width <= rightNeighbor)
                        {
                            hasBoxInRightRegion = true;
                        }
                    }
                }

                if (hasBoxInLeftRegion && hasBoxInRightRegion)
                {
                    validBodyPositions.Add(pos);
                }
            }

            var result = new List<int>(topColumnPositions);
            result.AddRange(validBodyPositions);
            result.Sort();

            return result;
        }

        private static bool IsContains(Rect box, Rect tableRect)
        {
            return box.X >= tableRect.X
                && box.Y >= tableRect.Y
                && box.X + box.Width <= tableRect.X + tableRect.Width
                && box.Y + box.Height <= tableRect.Y + tableRect.Height;
        }

        private static void ResolveTopBottomBorder(List<Line> srcHLines, Rect tableBox, IEnumerable<TextRect> textBoxes)
        {
            var top = srcHLines.Min(line => line.Y1);
            var bottom = srcHLines.Max(line => line.Y2);
            var topLine = new Line(tableBox.Left, top, tableBox.Right, top);
            var bottomLine = new Line(tableBox.Left, bottom, tableBox.Right, bottom);

            var tableTopLine = new Line(tableBox.Left, tableBox.Top, tableBox.Right, tableBox.Top);
            var tableBottomLine = new Line(tableBox.Left, tableBox.Bottom, tableBox.Right, tableBox.Bottom);

            bool hasTextBetweenTopLines = textBoxes.Any(textBox =>
            {
                double midY = (textBox.Top + textBox.Bottom) / 2.0;
                return midY > Math.Min(top, tableBox.Top) && midY < Math.Max(top, tableBox.Top);
            });
            if (hasTextBetweenTopLines)
            {
                srcHLines.Insert(0, tableTopLine);
            }

            bool hasTextBetweenBottomLines = textBoxes.Any(textBox =>
            {
                double midY = (textBox.Top + textBox.Bottom) / 2.0;
                return midY > Math.Min(bottom, tableBox.Bottom) && midY < Math.Max(bottom, tableBox.Bottom);
            });
            if (hasTextBetweenBottomLines)
            {
                srcHLines.Add(tableBottomLine);
            }
        }

        private static int CountTopVLine(List<Line> vLines, int yTop, int xLeftX, int xRight)
        {
            if (vLines == null || vLines.Count() == 0)
            {
                return 0;
            }

            var topLines = vLines.Where(l => Math.Abs(l.Y1 - yTop) <= 5);
            topLines = topLines.Where(l => l.X1 >= xLeftX && l.X1 <= xRight);

            return topLines.Count();
        }
    }
}
