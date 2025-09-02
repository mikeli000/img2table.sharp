using OpenCvSharp;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage.TableElement;

public class PostionedTableCellDetector
{
    public static bool TryDetectLines(List<Line> srcHLines, List<Line> srcVLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, double charWidth, out List<Line> detectedHLines, out List<Line> detectedVLines)
    {
        detectedHLines = new List<Line>();
        detectedHLines.AddRange(srcHLines);
        detectedVLines = new List<Line>();
        detectedVLines.AddRange(srcVLines);

        detectedHLines = DetecteHorLines(detectedHLines, tableBbox, textBoxes);
        var topLine = detectedHLines[0];
        Line? secLine = detectedHLines.Count > 2 ? detectedHLines[1] : null;
        
        var columnPositions = DetectColumnPositions(textBoxes, tableBbox, charWidth);
        var finalVPos = MergeColumnPositions(detectedVLines, columnPositions, textBoxes);
        ExtendVLines(finalVPos, detectedHLines, detectedVLines, textBoxes, tableBbox);
        if (secLine != null)
        {
            var l_sec = secLine.X1;
            var r_sec = secLine.X2;

            if (l_sec > tableBbox.Left)
            {
                var exLeftLine = new Line(tableBbox.Left, secLine.Y1, l_sec, secLine.Y2);
                if (!LineUtils.IntersectTextBoxes(exLeftLine, textBoxes))
                {
                    secLine.X1 = tableBbox.Left;
                }
            }
            if (r_sec < tableBbox.Right)
            {
                var exRightLine = new Line(r_sec, secLine.Y1, tableBbox.Right, secLine.Y2);
                if (!LineUtils.IntersectTextBoxes(exRightLine, textBoxes))
                {
                    secLine.X2 = tableBbox.Right;
                }
            }

            var tbodyBbox = new Rect
            {
                Left =  secLine.X1,
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

                /**
                var firstFinalVPos = detectedVLines.Select(line => line.X1).ToList();
                int? secLeft = null, secRight = null;
                for (int i = 0; i < secFinalVPos.Count; i++)
                {
                    if (!firstFinalVPos.Contains(secFinalVPos[i]))
                    {
                        if (i > 0)
                        {
                            secLeft = secFinalVPos[i - 1];
                        }
                        else
                        {
                            secLeft = secFinalVPos[i];
                        }
                        break;
                    }
                }
                for (int i = secFinalVPos.Count - 1; i >= 0; i--)
                {
                    if (!firstFinalVPos.Contains(secFinalVPos[i]))
                    {
                        if (i < secFinalVPos.Count - 1)
                        {
                            secRight = secFinalVPos[i + 1];
                        }
                        else
                        {
                            secRight = secFinalVPos[i];
                        }
                        break;
                    }
                }

                if (secLeft != null && secLeft < secLine.X1)
                {
                    secLine.X1 = secLeft.Value;
                }
                if (secRight != null && secRight > secLine.X2)
                {
                    secLine.X2 = secRight.Value;
                }

                for (int i = 0; i < secFinalVPos.Count() - 1; i++)
                {
                    if (secFinalVPos[i] == secLine.X1)
                    {
                        break;
                    }
                    int leftVLine = secFinalVPos[i];
                    int rightVLine = secFinalVPos[i + 1];
                    if (secLine.X1 > leftVLine && secLine.X1 < rightVLine)
                    {
                        double midPoint = (leftVLine + rightVLine) / 2.0;
                        if (secLine.X1 < midPoint)
                        {
                            secLine.X1 = leftVLine;
                        }
                        break;
                    }
                }

                for (int i = 0; i < secFinalVPos.Count() - 1; i++)
                {
                    if (secFinalVPos[i] == secLine.X2)
                    {
                        break;
                    }
                    int leftVLine = secFinalVPos[i];
                    int rightVLine = secFinalVPos[i + 1];
                    if (secLine.X2 > leftVLine && secLine.X2 < rightVLine)
                    {
                        double midPoint = (leftVLine + rightVLine) / 2.0;
                        if (secLine.X2 > midPoint)
                        {
                            secLine.X2 = rightVLine;
                        }
                        break;
                    }
                }
                */
                ExtendVLines(secFinalVPos, detectedHLines, detectedVLines, tBodyTextBoxes, tbodyBbox);
            }
        }

        return true;
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

    private static List<Line> DetecteHorLines(List<Line> srcHLines, Rect tableBbox, IEnumerable<TextRect> textBoxes)
    {
        if (textBoxes == null || textBoxes.Count() <= 0)
        {
            return srcHLines;
        }

        bool detectLines = false;
        var groupLines = GroupTextRectsByLine(textBoxes, removeOneBoxLine: true, true);
        if (srcHLines.Count > 1)
        {
            if (groupLines.Count > 2 && groupLines.Count / (float)srcHLines.Count >= 2)
            {
                detectLines = true;
            }
        }
        else
        {
            detectLines = true;
        }

        if (detectLines)
        {
            groupLines = GroupTextRectsByLine(textBoxes, srcHLines, tableBbox);
            var detectHPos = CalcHorPos(groupLines);
            var hLines = DetectHorLines(srcHLines, detectHPos, tableBbox, textBoxes);
            ResolveTopBottomBorder(hLines, tableBbox, textBoxes);
            return hLines;
        }

        ResolveTopBottomBorder(srcHLines, tableBbox, textBoxes);
        return srcHLines;
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

        var delta = 2;
        foreach (var p in finalVPos)
        {
            var vLine = vLines.FirstOrDefault(line => line.X1 == p);
            if (vLine != null)
            {
                if (vLine.Y2 >= bottomLine)
                {
                    continue;
                }

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
            else
            {
                var newLine = new Line(p, tableBbox.Top, p, bottomLine);
                vLines.Add(newLine);
            }
        }
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

    private static List<Line> DetectHorLines(List<Line> srcHLines, List<int> detectHPos, Rect tableBbox, IEnumerable<TextRect> textBoxes)
    {
        // temp implementation
        var left = tableBbox.Left;
        var right = tableBbox.Right;

        var hLines = new List<Line>();
        hLines.Add(new Line(left, tableBbox.Top, right, tableBbox.Top));

        for (int i = 0; i < detectHPos.Count; i++)
        {
            int y = detectHPos[i];
            srcHLines.Add(new Line(left, y, right, y));
        }

        hLines.Add(new Line(left, tableBbox.Bottom, right, tableBbox.Bottom));
        return hLines;
    }

    public static bool TryDetectKVTable(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, double charWidth, out KeyValueTable keyValueTable)
    {
        keyValueTable = null;
        bool isKVTablePossible = false;
        var minGap = charWidth * 2; // TODO
        var columnPositions = ScanForGapsBetweenBoxes(textBoxes, minGap);
        if (columnPositions.Count <= 1)
        {
            isKVTablePossible = true;
        }
        else
        {
            return false;
        }

        bool useGroupLines = false;
        var groupLines = GroupTextRectsByLine(textBoxes);
        if (hLines.Count > 1)
        {
            if (groupLines.Count > 2 && groupLines.Count / (float)hLines.Count >= 2)
            {
                useGroupLines = true;
            }
        }
        else
        {
            useGroupLines = true;
        }

        if (!useGroupLines)
        {
            groupLines = GroupTextRectsByLine(textBoxes, hLines, tableBbox);
        }
        
        var kvLines = new List<KVLine>();
        for (int i = 0; i < groupLines.Count(); i++)
        {
            var line = groupLines[i];
            var cols = ScanForGapsBetweenBoxes(line, minGap);
            var kvLine = new KVLine
            {
                LineIndex = i,
                ColGaps = cols,
                TextRects = line
            };
            kvLines.Add(kvLine);
        }

        if (kvLines.Any(c => c.ColGaps.Count() > 1) || kvLines.All(c => c.ColGaps.Count() <= 0))
        {
            return false;
        }

        var d_lines = new List<Line>();
        d_lines.Add(new Line(tableBbox.Left, tableBbox.Top, tableBbox.Right, tableBbox.Top));
        int last_bottom = tableBbox.Top;
        for (int i = 0; i < kvLines.Count; i++)
        {
            var rects = kvLines[i].TextRects;
            if (rects.Count == 0)
            {
                continue;
            }

            int top = rects.Min(r => r.Top);
            int bottom = rects.Max(r => r.Bottom);
            if (i == 0)
            {
                last_bottom = bottom;
                continue;
            }

            int middle = (top + last_bottom) / 2;
            d_lines.Add(new Line(tableBbox.Left, middle, tableBbox.Right, middle));
            last_bottom = bottom;
        }
        d_lines.Add(new Line(tableBbox.Left, tableBbox.Bottom, tableBbox.Right, tableBbox.Bottom));

        var kv_rows = new List<Row>();
        var k_merge_cells_possible = new Dictionary<int, Cell>();
        var v_merge_cells_possible = new Dictionary<int, Cell>();
        for (int i = 0; i < kvLines.Count; i++)
        {
            int cols = kvLines[i].ColGaps.Count + 1;
            var x1 = d_lines[i].X1;
            var y1 = d_lines[i].Y1;
            var x2 = d_lines[i + 1].X2;
            var y2 = d_lines[i + 1].Y2;
            if (cols == 1)
            {
                var l_mid = x1 + (x2 - x1) / 2;
                var rects_l = kvLines[i].TextRects[0].Left;
                var rects_r = kvLines[i].TextRects[kvLines[i].TextRects.Count - 1].Right;
                var rects_mid = rects_l + (rects_r - rects_l) / 2;
                int del = 2; // (int)(2 * charWidth);
                if (rects_mid < l_mid) // k side
                {
                    var k_cell = new Cell(x1, y1, rects_r + del, y2);
                    var v_cell = new Cell(rects_r + del, y1, x2, y2);
                    kv_rows.Add(new Row(new List<Cell>() { k_cell, v_cell }));
                    k_merge_cells_possible.Add(i, k_cell);
                }
                else // v side
                {
                    var k_cell = new Cell(x1, y1, rects_l - del, y2);
                    var v_cell = new Cell(rects_l - del, y1, x2, y2);
                    kv_rows.Add(new Row(new List<Cell>() { k_cell, v_cell }));
                    v_merge_cells_possible.Add(i, v_cell);
                }
            }
            else
            {
                int v_pos = kvLines[i].ColGaps[0];
                var k_cell = new Cell(x1, y1, v_pos, y2);
                var v_cell = new Cell(x1, y1, d_lines[i + 1].X2, y2);
                kv_rows.Add(new Row(new List<Cell>() { k_cell, v_cell }));
            }
        }

        // TODO: merge possible k/v line

        keyValueTable = new KeyValueTable(kv_rows);

        return true;
    }

    class KVLine 
    {
        public int LineIndex { get; set; }
        public List<int> ColGaps { get; set; }
        public List<TextRect> TextRects { get; set; }
    }

    private static List<List<TextRect>> GroupTextRectsByLine(IEnumerable<TextRect> rects, List<Line> hLines, Rect tableBbox)
    {
        var lines = new List<List<TextRect>>();
        if (!rects.Any())
        {
            return lines;
        }

        var rectList = rects.ToList();
        var hYs = hLines.Select(l => l.Y1).OrderBy(y => y).ToList();

        if (hYs.Any())
        {
            int firstLineY = hYs.First();
            bool hasTextAboveFirstLine = rectList.Any(r => (r.Top + r.Bottom) / 2.0 < firstLineY);
            if (hasTextAboveFirstLine)
            {
                var topLine = new Line(tableBbox.Left, tableBbox.Top, tableBbox.Right, tableBbox.Top);
                hLines.Insert(0, topLine);
                hYs.Insert(0, tableBbox.Y);
            }
        }
        else
        {
            hYs.Add(tableBbox.Top);
        }

        if (hYs.Count() > 1 || (hYs.Count() == 1 && hYs[0] == tableBbox.Y))
        {
            int lastLineY = hYs.Last();
            int tableBboxBottom = tableBbox.Y + tableBbox.Height;
            bool hasTextBelowLastLine = rectList.Any(r => (r.Top + r.Bottom) / 2.0 > lastLineY);
            if (hasTextBelowLastLine)
            {
                var bottomLine = new Line(tableBbox.Left, tableBbox.Bottom, tableBbox.Right, tableBbox.Bottom);
                hLines.Add(bottomLine);
                hYs.Add(tableBbox.Bottom);
            }
        }

        hYs = hYs.Distinct().OrderBy(y => y).ToList();
        for (int i = 0; i < hYs.Count - 1; i++)
        {
            int topLine = hYs[i];
            int bottomLine = hYs[i + 1];

            var group = rectList
                .Where(r =>
                {
                    double midY = (r.Top + r.Bottom) / 2.0;
                    return midY > topLine && midY < bottomLine;
                })
                .OrderBy(r => r.Left)
                .ToList();

            if (group.Count > 0)
            { 
                lines.Add(group); 
            }
        }

        return lines;
    }

    private static List<List<TextRect>> GroupTextRectsByLine(IEnumerable<TextRect> rects, bool removeOneBoxLine = false, bool removeLowcaseStartedLine = false)
    {
        var lines = new List<List<TextRect>>();
        if (rects.Count() == 0)
        {
            return lines;
        }
        if (rects.Count() == 1)
        {
            lines.Add(rects.ToList());
            return lines;
        }

        var copy = rects.ToList();
        int top = copy.Min(c => Math.Min(c.Top, c.Bottom));
        int bottom = copy.Max(c => Math.Max(c.Top, c.Bottom));

        var line = new List<TextRect>();
        for (int i = top + 1; i <= bottom; i++)
        {
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
                int currBottom = temp.Max(c => Math.Max(c.Top, c.Bottom));
                foreach (var c in copy)
                {
                    if (c.Top <= currBottom)
                    {
                        temp.Add(c);
                    }
                }
                copy.RemoveAll(c => temp.Contains(c));
                line.AddRange(temp);

                if (copy.Count() > 0)
                {
                    i = temp.Max(c => Math.Max(c.Top, c.Bottom)) + 1;
                }
                else
                {
                    lines.Add(line.OrderBy(c => c.Left).ToList());
                    break;
                }
            }
            else
            {
                if (line.Count() > 0)
                {
                    lines.Add(line.OrderBy(c => c.Left).ToList());
                    line = new List<TextRect>();
                }
            }
        }

        if (removeOneBoxLine)
        {
            lines = lines.Where(ll => ll.Count() > 1).ToList();
        }

        if (removeLowcaseStartedLine)
        {
            lines = lines.Where(ll => !char.IsLower(ll[0].Text.Trim()[0])).ToList();
        }
        
        return lines;
    }

    private static List<int> CalcHorPos(List<List<TextRect>> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            return new List<int>();
        }

        var hPos = new List<int>();
        for (int i = 0; i < lines.Count() - 1; i++)
        {
            var currLine = lines[i];
            var nextLine = lines[i + 1];

            var currBottom = currLine.Max(r => r.Bottom);
            var nextTop = nextLine.Min(r => r.Top);

            var mid = (int)((currBottom + nextTop) / 2.0 + 0.5);
            hPos.Add(mid);
        }

        return hPos;
    }

    private static List<Line> RemoveRedundantLines(List<Line> vLines, IEnumerable<TextRect> ocrBoxes)
    {
        vLines = vLines.OrderBy(vl => vl.X1).ToList();
        for (int i = 0; i < vLines.Count - 1; i++)
        {
            var line1 = vLines[i];
            var line2 = vLines[i + 1];

            var bx = ocrBoxes.Where(box => box.Left > line1.X1 && box.Right < line2.X1).ToList();
            if (bx.Count() == 0)
            {
                if (line1.Height < line2.Height)
                {
                    vLines.RemoveAt(i);
                    i--;
                }
                else
                {
                    vLines.RemoveAt(i + 1);
                }
            }
        }

        return vLines;
    }

    private static bool PostionEqual(List<int> first, List<int> second)
    {
        if (first == null && second == null)
        {
            return true;
        }
            
        if (first == null || second == null)
        {
            return false;
        }
            
        return first.SequenceEqual(second);
    }

    private static void RemoveLinesInBox(List<LineSegmentPoint> hLines, IEnumerable<Rect> textBoxes)
    {
        hLines.RemoveAll(line =>
            textBoxes.Any(box =>
                IsPointInBox(line.P1.X, line.P1.Y, box) &&
                IsPointInBox(line.P2.X, line.P2.Y, box)
            )
        );
    }

    private static bool IsPointInBox(int x, int y, Rect box)
    {
        return (x >= box.Left) && (x <= box.Right) && (y >= box.Top) && (y <= box.Bottom);
    }

    public static List<Rect> MaskTexts(Mat srcImage, float scale = 1.0f)
    {
        var ocrBoxes = new List<Rect>();
        using PaddleOcrAll paddle = new PaddleOcrAll(LocalFullModels.ChineseV3);
        PaddleOcrResult ocrResult = paddle.Run(srcImage);

        for (int i = 0; i < ocrResult.Regions.Length; ++i)
        {
            PaddleOcrResultRegion region = ocrResult.Regions[i];
            Rect ocrBox = Extend(region.Rect.BoundingRect(), -2);
            ocrBoxes.Add(ocrBox);
        }
        
        return ocrBoxes;
    }

    public static Rect Extend(Rect rect, int extendLength)
    {
        return Rect.FromLTRB(rect.Left - extendLength, rect.Top - extendLength, rect.Right + extendLength, rect.Bottom + extendLength);
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

    private static bool IsContains(Rect box, Rect tableRect)
    {
        return box.X >= tableRect.X
            && box.Y >= tableRect.Y
            && box.X + box.Width <= tableRect.X + tableRect.Width
            && box.Y + box.Height <= tableRect.Y + tableRect.Height;
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

    private static List<LineSegmentPoint> RemoveNearbyLines(List<LineSegmentPoint> lines, Rect tableBbox, double distanceThreshold = 10.0, double angleThreshold = 5.0)
    {
        if (lines.Count <= 1) 
        { 
            return lines; 
        }
        
        var filteredLines = new List<LineSegmentPoint>();
        var processed = new bool[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            if (processed[i])
            {
                continue;
            }

            var currentLine = lines[i];
            var similarLines = new List<LineSegmentPoint> { currentLine };
            processed[i] = true;

            for (int j = i + 1; j < lines.Count; j++)
            {
                if (processed[j]) 
                {
                    continue;
                }
                
                var otherLine = lines[j];
                if (IsLinesNearby(currentLine, otherLine, distanceThreshold, angleThreshold))
                {
                    similarLines.Add(otherLine);
                    processed[j] = true;
                }
            }

            var mergedLine = MergeLinesWithTableBoundary(similarLines, tableBbox);
            filteredLines.Add(mergedLine);
        }

        return filteredLines;
    }

    private static bool IsLinesNearby(LineSegmentPoint line1, LineSegmentPoint line2, double distanceThreshold, double angleThreshold)
    {
        double angle1 = Math.Atan2(line1.P2.Y - line1.P1.Y, line1.P2.X - line1.P1.X) * 180.0 / Math.PI;
        double angle2 = Math.Atan2(line2.P2.Y - line2.P1.Y, line2.P2.X - line2.P1.X) * 180.0 / Math.PI;

        double angleDiff = Math.Abs(angle1 - angle2);
        angleDiff = Math.Min(angleDiff, 180 - angleDiff);

        if (angleDiff > angleThreshold)
        {
            return false;
        }

        double distance = CalculateLineDistance(line1, line2);
        return distance <= distanceThreshold;
    }

    private static double CalculateLineDistance(LineSegmentPoint line1, LineSegmentPoint line2)
    {
        var center1 = new Point2f((line1.P1.X + line1.P2.X) / 2.0f, (line1.P1.Y + line1.P2.Y) / 2.0f);
        var center2 = new Point2f((line2.P1.X + line2.P2.X) / 2.0f, (line2.P1.Y + line2.P2.Y) / 2.0f);

        return Math.Sqrt(Math.Pow(center1.X - center2.X, 2) + Math.Pow(center1.Y - center2.Y, 2));
    }

    private static LineSegmentPoint MergeLines(List<LineSegmentPoint> lines)
    {
        if (lines.Count == 1) 
        { 
            return lines[0]; 
        }
        
        int minX = lines.SelectMany(l => new[] { l.P1.X, l.P2.X }).Min();
        int maxX = lines.SelectMany(l => new[] { l.P1.X, l.P2.X }).Max();
        int minY = lines.SelectMany(l => new[] { l.P1.Y, l.P2.Y }).Min();
        int maxY = lines.SelectMany(l => new[] { l.P1.Y, l.P2.Y }).Max();

        double totalAngle = 0;
        foreach (var line in lines)
        {
            double angle = Math.Atan2(line.P2.Y - line.P1.Y, line.P2.X - line.P1.X);
            totalAngle += angle;
        }
        double avgAngle = totalAngle / lines.Count;
        Point startPoint, endPoint;

        double angleDegrees = Math.Abs(avgAngle * 180.0 / Math.PI);
        if (angleDegrees < 45 || angleDegrees > 135)
        {
            int avgY = (int)lines.SelectMany(l => new[] { l.P1.Y, l.P2.Y }).Average();
            startPoint = new Point(minX, avgY);
            endPoint = new Point(maxX, avgY);
        }
        else 
        {
            int avgX = (int)lines.SelectMany(l => new[] { l.P1.X, l.P2.X }).Average();
            startPoint = new Point(avgX, minY);
            endPoint = new Point(avgX, maxY);
        }

        return new LineSegmentPoint(startPoint, endPoint);
    }

    private static LineSegmentPoint MergeLinesWithTableBoundary(List<LineSegmentPoint> lines, Rect tableBbox, double boundaryTolerance = 20.0)
    {
        if (lines.Count == 1)
        {
            return AdjustLineToBoundary(lines[0], tableBbox, boundaryTolerance);
        }

        var mergedLine = MergeLines(lines);
        return AdjustLineToBoundary(mergedLine, tableBbox, boundaryTolerance);
    }

    private static LineSegmentPoint AdjustLineToBoundary(LineSegmentPoint line, Rect tableBbox, double boundaryTolerance = 20.0)
    {
        var startPoint = line.P1;
        var endPoint = line.P2;

        bool isHorizontal = Math.Abs(line.P1.Y - line.P2.Y) < Math.Abs(line.P1.X - line.P2.X);

        if (isHorizontal)
        {
            int lineY = (line.P1.Y + line.P2.Y) / 2;
            
            if (Math.Abs(lineY - tableBbox.Y) <= boundaryTolerance)
            {
                startPoint = new Point(Math.Min(line.P1.X, line.P2.X), tableBbox.Y);
                endPoint = new Point(Math.Max(line.P1.X, line.P2.X), tableBbox.Y);
            }
            else if (Math.Abs(lineY - (tableBbox.Y + tableBbox.Height)) <= boundaryTolerance)
            {
                int bottomY = tableBbox.Y + tableBbox.Height;
                startPoint = new Point(Math.Min(line.P1.X, line.P2.X), bottomY);
                endPoint = new Point(Math.Max(line.P1.X, line.P2.X), bottomY);
            }
        }
        else
        {
            int lineX = (line.P1.X + line.P2.X) / 2;
            if (Math.Abs(lineX - tableBbox.X) <= boundaryTolerance)
            {
                startPoint = new Point(tableBbox.X, Math.Min(line.P1.Y, line.P2.Y));
                endPoint = new Point(tableBbox.X, Math.Max(line.P1.Y, line.P2.Y));
            }
            else if (Math.Abs(lineX - (tableBbox.X + tableBbox.Width)) <= boundaryTolerance)
            {
                int rightX = tableBbox.X + tableBbox.Width;
                startPoint = new Point(rightX, Math.Min(line.P1.Y, line.P2.Y));
                endPoint = new Point(rightX, Math.Max(line.P1.Y, line.P2.Y));
            }
        }

        return new LineSegmentPoint(startPoint, endPoint);
    }

    private static bool IsHorizontalLine(LineSegmentPoint line, double angleThreshold = 10.0)
    {
        var deltaX = line.P2.X - line.P1.X;
        var deltaY = line.P2.Y - line.P1.Y;
        var angle = Math.Abs(Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI);

        return angle <= angleThreshold || angle >= (180.0 - angleThreshold);
    }
}
