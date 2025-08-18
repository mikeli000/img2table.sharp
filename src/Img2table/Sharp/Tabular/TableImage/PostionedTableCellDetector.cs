using OpenCvSharp;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using PDFDict.SDK.Sharp.Core.OCR;
using System.Text;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;

public class PostionedTableCellDetector
{
    public static void DetectLines(List<Line> originalHLines, List<Line> originalVLines, Rect tableBbox, IEnumerable<Rect> textBoxes, int charWidth)
    {
        var hLines = originalHLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();
        var vLines = originalVLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();

        if (textBoxes == null || textBoxes.Count() <= 0)
        {
            return;
        }
    }

    public static List<Line> DetectVerLines(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> textBoxes, int charWidth)
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

    private static bool LineCrossTextBox(Line line, List<Line> vLines, IEnumerable<Rect> textBoxes)
    {
        return false;
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

    private static List<int> DetectBodyVerLines(IEnumerable<TextRect> ocrBoxes, Rect bodyRect, List<int> topColumnPositions, int charWidth)
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

    private static List<int> DetectColumnPositions(IEnumerable<TextRect> ocrBoxes, Rect tableRect, int charWidth)
    {
        var columnPositions = ScanForGapsBetweenBoxes(ocrBoxes, tableRect, charWidth);
        columnPositions.Insert(0, tableRect.Left);
        columnPositions.Insert(columnPositions.Count, tableRect.Right);

        return columnPositions;
    }

    private static bool IsContains(Rect box, Rect tableRect)
    {
        return box.X >= tableRect.X 
            && box.Y >= tableRect.Y 
            && box.X + box.Width <= tableRect.X + tableRect.Width 
            && box.Y + box.Height <= tableRect.Y + tableRect.Height;
    }

    private static List<int> ScanForGapsBetweenBoxes(IEnumerable<TextRect> ocrBoxes, Rect tableRect, int charWidth)
    {
        var gaps = new List<int>();
        if (ocrBoxes == null || ocrBoxes.Count() == 0)
        {
            return gaps;
        }

        int step = 1;
        int minGapW = charWidth * 2;

        var ocrBoxesCopy = new List<TextRect>(ocrBoxes);
        int minX = ocrBoxesCopy.Min(r => r.Left);
        int maxX = ocrBoxesCopy.Max(r => r.Right);

        int currentX = minX + 1;
        while (currentX < maxX)
        {
            if (TryGetIntersectingBox(currentX, ocrBoxesCopy, out var intersectingBox))
            {
                currentX = intersectingBox.Right + 1;
                ocrBoxesCopy.RemoveAll(box => box.Right <= currentX);
            }
            else
            {
                int gapStart = currentX;
                
                while (currentX < maxX)
                {
                    if (TryGetIntersectingBox(currentX, ocrBoxesCopy, out intersectingBox))
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
                ocrBoxesCopy.RemoveAll(box => box.Right <= currentX);
            }
        }
        
        return gaps;
    }

    private static bool TryGetIntersectingBox(int x, List<TextRect> ocrBoxes, out Rect intersectBox)
    {
        intersectBox = default;
        foreach (var box in ocrBoxes)
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
