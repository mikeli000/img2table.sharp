using OpenCvSharp;
using Sdcb.PaddleOCR.Models.Local;
using Sdcb.PaddleOCR;
using Img2table.Sharp.Tabular.TableImage.TableElement;

public class PostionedTableCellDetector
{
    public static void DetectTableCells(string imagePath, Rect tableBbox)
    {
        using Mat img = Cv2.ImRead(imagePath);
        DetectLines(img, tableBbox);
    }

    public static (List<Line>, List<Line>) DetectLines(Mat srcImage, Rect tableBbox)
    {
        Cv2.Rectangle(srcImage, tableBbox, Scalar.Black, 1);

        using Mat gray = srcImage.Channels() == 3 
            ? new Mat() 
            : srcImage.Clone();
        
        if (srcImage.Channels() == 3)
        {
            Cv2.CvtColor(srcImage, gray, ColorConversionCodes.BGR2GRAY);
        }

        using Mat edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);
        LineSegmentPoint[] lines = Cv2.HoughLinesP(
            edges,
            rho: 1,              
            theta: Math.PI / 180,
            threshold: 80,       
            minLineLength: tableBbox.Width / 8,
            maxLineGap: 10
        );

        var ocrBoxes = MaskTexts(srcImage);
        lines = RemoveNearbyLines(lines, tableBbox, distanceThreshold: 15.0, angleThreshold: 10.0);
        lines = lines.OrderBy(line => line.P1.Y).ToArray();
        var hLines = lines.Where(line => IsHorizontalLine(line, angleThreshold: 10.0)).ToArray();
        var topLine = hLines[0];
        LineSegmentPoint? secLine = hLines.Length > 1 ? hLines[1] : null;

        using Mat linesMat = Mat.Zeros(gray.Size(), MatType.CV_8UC1);
        foreach (var line in lines)
        {
            Cv2.Line(linesMat, line.P1, line.P2, Scalar.White, 1);
        }
        DrawOCRBoxes(ocrBoxes, linesMat);

        var columnPositions = DetectVerLines(ocrBoxes, tableBbox);
        List<LineSegmentPoint> columns = columnPositions
            .Select(x => new LineSegmentPoint(new Point(x, tableBbox.Top), new Point(x, tableBbox.Bottom)))
            .ToList();

        if (secLine != null)
        {
            var tbodyBbox = new Rect();
            tbodyBbox.Left = tableBbox.Left;
            tbodyBbox.Top = secLine.Value.P1.Y;
            tbodyBbox.Width = tableBbox.Width;
            tbodyBbox.Height = tableBbox.Bottom - secLine.Value.P1.Y;

            var secPositions = DetectVerLines(ocrBoxes, tbodyBbox);
            if (secPositions.Count > columnPositions.Count)
            {
                foreach (var p in secPositions)
                {
                    if (columnPositions.Contains(p))
                    {
                        Cv2.Line(srcImage, new Point(p, tableBbox.Top), new Point(p, tableBbox.Bottom), Scalar.Blue, 1);
                        continue;
                    }

                    columns.Add(new LineSegmentPoint(new Point(p, tbodyBbox.Top), new Point(p, tbodyBbox.Bottom)));

                    Cv2.Line(srcImage, new Point(p, tbodyBbox.Top), new Point(p, tbodyBbox.Bottom), Scalar.Red, 1);
                }
            }
        }

        Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\cols.png", srcImage);


        lines = lines.Concat(columns).ToArray();
        var horLines = new List<Line>();
        var verLines = new List<Line>();
        foreach (var line in lines)
        {
            if (Math.Abs(line.P1.Y - line.P2.Y) < Math.Abs(line.P1.X - line.P2.X))
            {
                horLines.Add(new Line(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y));
            }
            else
            {
                verLines.Add(new Line(line.P1.X, line.P1.Y, line.P2.X, line.P2.Y));

                Cv2.Line(linesMat, line.P1, line.P2, Scalar.White, 1);
            }
        }

        Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\lines_only.png", linesMat);
        Console.WriteLine($"detect {lines.Length} X {columnPositions.Count} lines");

        return (horLines, verLines);
    }

    private static List<Rect> MaskTexts(Mat srcImage)
    {
        var ocrBoxes = new List<Rect>();
        Task<PaddleOcrResult> ocrResultTask = Task.Run(() =>
        {
            using PaddleOcrAll all = new(LocalFullModels.ChineseV3);
            all.Detector.UnclipRatio = 1.2f;
            return all.Run(srcImage);
        });

        PaddleOcrResult ocrResult = ocrResultTask.Result;
        for (int i = 0; i < ocrResult.Regions.Length; ++i)
        {
            PaddleOcrResultRegion region = ocrResult.Regions[i];
            Rect ocrBox = Extend(region.Rect.BoundingRect(), -1);
            ocrBoxes.Add(ocrBox);
        }
        
        return ocrBoxes;
    }

    static void DrawOCRBoxes(List<Rect> ocrBoxes, Mat dstImage)
    {
        for (int i = 0; i < ocrBoxes.Count; ++i)
        {
            Rect ocrBox = Extend(ocrBoxes[i], -1);
            Cv2.Rectangle(dstImage, ocrBox, Scalar.White, -1);
        }
    }

    public static Rect Extend(in Rect rect, int extendLength)
    {
        return Rect.FromLTRB(rect.Left - extendLength, rect.Top - extendLength, rect.Right + extendLength, rect.Bottom + extendLength);
    }

    private static List<int> DetectVerLines(List<Rect> ocrBoxes, Rect tableRect)
    {
        var filteredBoxes = ocrBoxes
            .Where(box => IsContains(box, tableRect))
            .ToList();
        var columnPositions = ScanForGapsBetweenBoxes(filteredBoxes, tableRect);
        columnPositions.Insert(0, tableRect.Left);
        columnPositions.Insert(columnPositions.Count, tableRect.Right);

        return columnPositions;
    }

    private static bool IsContains(Rect box, Rect tableRect)
    {
        return box.X >= tableRect.X &&
               box.Y >= tableRect.Y &&
               box.X + box.Width <= tableRect.X + tableRect.Width &&
               box.Y + box.Height <= tableRect.Y + tableRect.Height;
    }

    private static List<int> ScanForGapsBetweenBoxes(List<Rect> ocrBoxes, Rect tableRect)
    {
        var gaps = new List<int>();

        int step = 1;
        int minGapW = 5;

        var ocrBoxesCopy = new List<Rect>(ocrBoxes);
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

    private static bool TryGetIntersectingBox(int x, List<Rect> ocrBoxes, out Rect intersectBox)
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

    private static LineSegmentPoint[] RemoveNearbyLines(LineSegmentPoint[] lines, Rect tableBbox, double distanceThreshold = 10.0, double angleThreshold = 5.0)
    {
        if (lines.Length <= 1) 
        { 
            return lines; 
        }
        
        var filteredLines = new List<LineSegmentPoint>();
        var processed = new bool[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            if (processed[i])
            {
                continue;
            }

            var currentLine = lines[i];
            var similarLines = new List<LineSegmentPoint> { currentLine };
            processed[i] = true;

            for (int j = i + 1; j < lines.Length; j++)
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

        return filteredLines.ToArray();
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
