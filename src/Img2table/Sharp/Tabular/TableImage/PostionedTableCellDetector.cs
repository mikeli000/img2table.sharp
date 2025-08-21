using OpenCvSharp;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage.TableElement;

public class PostionedTableCellDetector
{
    public static List<Line> DetectVerLines(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, double charWidth)
    {
        Line topLine = null;
        if (hLines.Count == 0)
        {
            topLine = new Line(tableBbox.Left, tableBbox.Top, tableBbox.Right, tableBbox.Top);
        }
        else
        {
            topLine = hLines[0];
        }
        Line? secLine = hLines.Count > 2 ? hLines[1] : null;

        var columnPositions = DetectColumnPositions(textBoxes, tableBbox, charWidth);
        var exists = vLines.Select(line => line.X1).ToList();
        foreach (var p in columnPositions)
        {
            if (exists.Contains(p))
            {
                continue;
            }

            var newLine = new Line(p, tableBbox.Top, p, tableBbox.Bottom);
            vLines.Add(newLine);
            exists.Add(p);
        }

        vLines = RemoveRedundantLines(vLines, textBoxes);
        exists = vLines.Select(line => line.X1).ToList();
        secLine = null;
        if (secLine != null)
        {
            var tbodyBbox = new Rect();
            tbodyBbox.Left = tableBbox.Left;
            tbodyBbox.Top = secLine.Y1;
            tbodyBbox.Width = tableBbox.Width;
            tbodyBbox.Height = tableBbox.Bottom - secLine.Y1;

            var secPositions = DetectBodyVerLines(textBoxes, tbodyBbox, columnPositions, charWidth);
            if (!PostionEqual(columnPositions, secPositions))
            {
                // 这里需要判断 延长 second line 是否会跟某个 text box 相交, 如果相交, 则不延长
                secLine.X1 = tbodyBbox.Left;
                secLine.X2 = tableBbox.Right;


                foreach (var p in secPositions)
                {
                    if (exists.Contains(p))
                    {
                        continue;
                    }
                    vLines.Add(new Line(p, tbodyBbox.Top, p, tbodyBbox.Bottom));
                }
            }
        }

        return vLines;
    }

    public static bool TryDetectKVTable(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, double charWidth, out KeyValueTable keyValueTable)
    {
        keyValueTable = null;
        bool isKVTablePossible = false;
        var minGap = charWidth * 2;
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
                if (i == 0) // || i == kvLines.Count - 1
                {
                    var cell = new Cell(x1, y1, x2, y2);
                    kv_rows.Add(new Row(new List<Cell>() { cell }));
                }
                else
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
            }
            else
            {
                int v_pos = kvLines[i].ColGaps[0];
                var k_cell = new Cell(x1, y1, v_pos, y2);
                var v_cell = new Cell(x1, y1, d_lines[i + 1].X2, y2);
                kv_rows.Add(new Row(new List<Cell>() { k_cell, v_cell }));
            }
        }

        //if (k_merge_cells_possible.Count > 0)
        //{
        //    var k_merge_lines = new List<List<int>>();
        //    foreach (var entry in k_merge_cells_possible)
        //    {
        //        var line = entry.Key;

        //        var m_lines = new List<int> { line };
        //        int i_next = line + 1;
        //        while (i_next < kv_rows.Count)
        //        {
        //            if (k_merge_cells_possible.ContainsKey(i_next))
        //            {
        //                m_lines.Add(i_next);
        //                i_next++;
        //                continue;
        //            }

        //            var nextRow = kv_rows[line + 1];
        //            if (nextRow.NbColumns == 2)
        //            {
        //                break;
        //            }

        //        }
        //    }
        //}

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
            hYs.Add(tableBbox.Y);
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
                hYs.Add(tableBbox.Y);
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


    private static List<List<TextRect>> GroupTextRectsByLine(IEnumerable<TextRect> rects)
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

        return lines;
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

    static void DrawOCRBoxes(IEnumerable<Rect> ocrBoxes, Mat dstImage)
    {
        for (int i = 0; i < ocrBoxes.Count(); ++i)
        {
            Rect ocrBox = Extend(ocrBoxes.ElementAt(i), -1);
            Cv2.Rectangle(dstImage, ocrBox, Scalar.Gray, -1);
        }
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
        var columnPositions = ScanForGapsBetweenBoxes(textBoxes, charWidth);
        columnPositions.Insert(0, tableRect.Left);
        columnPositions.Insert(columnPositions.Count, tableRect.Right);

        return columnPositions;
    }

    private static List<int> ScanForGapsBetweenBoxes(IEnumerable<TextRect> textBoxes, double charWidth)
    {
        var gaps = new List<int>();
        if (textBoxes == null || textBoxes.Count() == 0)
        {
            return gaps;
        }

        int step = 1;
        var minGapW = charWidth * 2;

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
