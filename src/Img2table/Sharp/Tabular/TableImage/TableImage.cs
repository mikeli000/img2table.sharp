using OpenCvSharp;
using OpenCvSharp.XImgProc;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout;
using PDFDict.SDK.Sharp.Core.OCR;
using System.Text;
using img2table.sharp.Img2table.Sharp.Tabular;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;

namespace Img2table.Sharp.Tabular.TableImage
{
    public class TableImage
    {
        private Mat _img;
        private Mat _thresh;
        private double _charLength;
        private static double DefaultCharLength = 11;
        private double? _medianLineSep;
        private List<Cell> _contours;
        private List<Line> _lines;
        private List<Table> _tables;
        private bool _shouldOCR = false;
        public static bool Debug = false;

        public TableImage(Mat img)
        {
            Prepare(img);
        }

        private void Prepare(Mat img)
        {
            _img = img;
            _thresh = ThresholdDarkAreas(_img, DefaultCharLength);
            var t = Metrics.ComputeImgMetrics(_thresh.Clone());
            _charLength = t.Item1 != null ? t.Item1.Value : DefaultCharLength;
            _medianLineSep = t.Item2;
            _contours = t.Item3 ?? new List<Cell>();
        }

        public bool ShouldOCR => _shouldOCR;

        public List<Table> ExtractTables(bool implicitRows, bool implicitColumns, bool borderlessTables, Rect? tableBbox = null, IEnumerable<TextRect> textBoxes = null)
        {
            ExtractBorderedTables(implicitRows, implicitColumns, tableBbox, textBoxes);
            if (_tables != null && _tables.Count > 0)
            {
                var temp = new List<Table>();
                temp.AddRange(_tables);

                if (tableBbox != null)
                {
                    bool reCompsiteTable = false;
                    if (_tables[0].NbRows <= 1)
                    {
                        implicitRows = true;
                    }
                    if (_tables[0].NbColumns <= 1)
                    {
                        implicitColumns = true;
                    }
                    reCompsiteTable = implicitRows || implicitColumns;
                    _shouldOCR = implicitRows || implicitColumns;
                    if (reCompsiteTable)
                    {
                        ExtractBorderedTables(implicitRows, implicitColumns, tableBbox, textBoxes);
                    }
                }
                if (_tables.Count <= 0)
                {
                    return temp;
                }

                _shouldOCR = true;
                return _tables;
            }

            if (borderlessTables)
            {
                ExtractBorderlessTables();
            }

            return _tables;
        }

        private void ExtractBorderlessTables()
        {
            if (_medianLineSep != null)
            {
                _thresh = ThresholdDarkAreas(_img, _charLength);
                List<Table> borderlessTables = BorderlessTableIdentifier.IdentifyBorderlessTables(_thresh, _lines, _charLength, _medianLineSep.Value, _contours, _tables);

                _tables.AddRange(borderlessTables.Where(tb => tb.NbRows >= 2 && tb.NbColumns >= 3)); //TODO : improve this condition
            }
        }

        private void ExtractBorderedTables(bool implicitRows = false, bool implicitColumns = false, Rect? tableBbox = null, IEnumerable<TextRect> textBoxes = null)
        {
            int minLineLength = _medianLineSep.HasValue ? (int)Math.Min(1.5 * _medianLineSep.Value, 4 * _charLength) : 20;
            var (hLines, vLines) = LineDetector.DetectLines(_img, _contours, _charLength, minLineLength);

            var originalHLines = hLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();
            var originalVLines = vLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();

            if (tableBbox != null)
            {
                RemoveLinesInBox(hLines, textBoxes);
                RemoveLinesInBox(vLines, textBoxes);
                ResolveTopBottomBorder(hLines, vLines, tableBbox.Value, textBoxes);
                hLines = hLines.OrderBy(hl => hl.Y1).ToList();
                vLines = PostionedTableCellDetector.DetectVerLines(hLines, vLines, tableBbox.Value, textBoxes, (int)_charLength);
                vLines = vLines.OrderBy(vl => vl.X1).ToList();
                AlignTableBorder(hLines, vLines, tableBbox.Value, textBoxes);

                CompsiteTable(hLines, vLines, implicitRows, implicitColumns);

                if (true)
                {
                    var debugImage = _img.Clone();
                    foreach (var line in originalHLines)
                    {
                        Cv2.Line(debugImage, line.X1, line.Y1, line.X2, line.Y2, Scalar.Red, 2);
                    }
                    foreach (var line in originalVLines)
                    {
                        Cv2.Line(debugImage, line.X1, line.Y1, line.X2, line.Y2, Scalar.Green, 2);
                    }

                    //Cv2.Rectangle(debugImage, tableBbox.Value, Scalar.Magenta, 1);
                    if (textBoxes != null)
                    {
                        foreach (var box in textBoxes)
                        {
                            Cv2.Rectangle(debugImage, box, Scalar.Orange, 1);
                        }
                    }

                    //var table = _tables.FirstOrDefault();
                    //foreach (var row in table.Rows)
                    //{
                    //    foreach (var cell in row.Cells)
                    //    {
                    //        Cv2.Rectangle(debugImage, new Rect(cell.X1, cell.Y1, cell.Width, cell.Height), Scalar.Yellow, 1);
                    //    }
                    //}

                    var file = $@"C:\temp\img2table\{Guid.NewGuid().ToString()}.png";
                    Cv2.ImWrite(file, debugImage);
                }
            }
            else
            {
                implicitRows = hLines.Count <= 2;
                implicitColumns = vLines.Count <= 2;
                _shouldOCR = implicitRows || implicitColumns;
                CompsiteTable(hLines, vLines, implicitRows, implicitColumns);
            }
        }

        private void RemoveLinesInBox(List<Line> hLines, IEnumerable<TextRect> textBoxes)
        {
            hLines.RemoveAll(line =>
                textBoxes.Any(box =>
                    IsPointInBox(line.X1, line.Y1, box) &&
                    IsPointInBox(line.X2, line.Y2, box)
                )
            );
        }

        private bool IsPointInBox(int x, int y, Rect box, int deltaX = 4, int deltaY = 2)
        {
            return (x >= box.Left - deltaX) && (x <= box.Right + deltaX)
                && (y >= box.Top - deltaY) && (y <= box.Bottom + deltaY);
        }

        private void AlignTableBorder(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> boxes)
        {
            AlignLeft(hLines, vLines, tableBbox, boxes);
            AlignTop(hLines, vLines, tableBbox, boxes);
            AlignBottom(hLines, vLines, tableBbox, boxes);
            AlignRight(hLines, vLines, tableBbox, boxes);
        }

        private void AlignLeft(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> boxes, int minGap = 10)
        {
            int topmost = hLines.Count > 0 ? hLines.Min(l => Math.Min(l.Y1, l.Y2)) : tableBbox.Top;
            int bottommost = hLines.Count > 0 ? hLines.Max(l => Math.Max(l.Y1, l.Y2)) : tableBbox.Bottom;
            int leftmost = vLines.Count > 0 ? vLines.Min(l => Math.Min(l.X1, l.X2)) : tableBbox.Left;
            int tableLeft = tableBbox.Left;

            foreach (var hl in hLines)
            {
                if (Math.Abs(leftmost - hl.X1) <= minGap)
                {
                    hl.X1 = leftmost;
                }
            }

            if (tableLeft < leftmost)
            {
                var boxInGap = boxes.Where(b => {
                    int centerX = b.Left + b.Right / 2;
                    return centerX > tableLeft && centerX < leftmost;
                });

                if (boxInGap.Count() > 0)
                {
                    var newVLine = new Line(tableLeft, topmost, tableLeft, bottommost);
                    vLines.Add(newVLine);

                    foreach (var hl in hLines)
                    {
                        if (Math.Abs(leftmost - hl.X1) <= 1 && IntersectBoxes(hl, boxInGap.ToList()))
                        {
                            hl.X1 = tableLeft;
                        }
                    }
                }
            }
        }

        private bool IntersectBoxes(Line line, List<TextRect> boxes, bool vertical = false)
        {
            foreach (var box in boxes)
            {
                if (vertical)
                {
                    if (line.X1 >= ((Rect)box).Left && line.X1 <= ((Rect)box).Right)
                    {
                        return true;
                    }
                }
                else
                {
                    if (line.Y1 > ((Rect)box).Top && line.Y1 < ((Rect)box).Bottom)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void ResolveTopBottomBorder(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> boxes)
        {
            int leftmost = tableBbox.Left;
            int rightmost = tableBbox.Right;

            var topmostLine = hLines.Count > 0 ? hLines.OrderBy(l => Math.Min(l.Y1, l.Y2)).First() : null;
            int topmost = topmostLine != null ? Math.Min(topmostLine.Y1, topmostLine.Y2) : tableBbox.Top;

            int tableTop = tableBbox.Top;
            if (tableTop < topmost)
            {
                var boxInGap = boxes.Where(b =>
                {
                    int centerY = (((Rect)b).Top + ((Rect)b).Bottom) / 2;
                    return centerY > tableTop && centerY < topmost;
                });
                if (boxInGap.Count() > 0)
                {
                    var newHLine = new Line(leftmost, tableTop, rightmost, tableTop);
                    hLines.Add(newHLine);
                }
                else
                {
                    if (topmostLine != null)
                    {
                        hLines.Remove(topmostLine);
                        var newHLine = new Line(leftmost, tableTop, rightmost, tableTop);
                        hLines.Add(newHLine);
                    }
                }
            }

            int bottommost = hLines.Count > 0 ? hLines.Max(l => Math.Max(l.Y1, l.Y2)) : tableBbox.Bottom;
            int tableBottom = tableBbox.Bottom;
            if (tableBottom > bottommost)
            {
                var boxInGap = boxes.Where(b => {
                    int centerY = (((Rect)b).Top + ((Rect)b).Bottom) / 2;
                    return centerY > bottommost && centerY < tableBottom;
                });

                if (boxInGap.Count() > 0)
                {
                    var newHLine = new Line(leftmost, tableBottom, rightmost, tableBottom);
                    hLines.Add(newHLine);
                }
            }

            AlignLeft(hLines, vLines, tableBbox, boxes);
            AlignRight(hLines, vLines, tableBbox, boxes);
        }

        private void AlignTop(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> boxes, int minGap = 10)
        {
            int leftmost = vLines.Count > 0 ? vLines.Min(l => Math.Min(l.X1, l.X2)) : tableBbox.Left;
            int rightmost = vLines.Count > 0 ? vLines.Max(l => Math.Max(l.X1, l.X2)) : tableBbox.Right;
            int topmost = hLines.Count > 0 ? hLines.Min(l => Math.Min(l.Y1, l.Y2)) : tableBbox.Top;
            int tableTop = tableBbox.Top;

            foreach (var vl in vLines)
            {
                if (Math.Abs(topmost - vl.Y1) <= minGap)
                {
                    vl.Y1 = topmost;
                }
            }

            if (tableTop < topmost)
            {
                var boxInGap = boxes.Where(b => {
                    int centerY = (((Rect)b).Top + ((Rect)b).Bottom) / 2;
                    return centerY > tableTop && centerY < topmost;
                });
                if (boxInGap.Count() > 0)
                {
                    var newHLine = new Line(leftmost, tableTop, rightmost, tableTop);
                    hLines.Add(newHLine);

                    foreach (var vl in vLines)
                    {
                        if (Math.Abs(vl.Y1 - topmost) <= 1 && IntersectBoxes(vl, boxInGap.ToList(), true))
                        {
                            vl.Y1 = tableTop;
                        }
                    }
                }
            }
        }
        private void AlignBottom(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> boxes, int minGap = 10)
        {
            int leftmost = vLines.Count > 0 ? vLines.Min(l => Math.Min(l.X1, l.X2)) : tableBbox.Left;
            int rightmost = vLines.Count > 0 ? vLines.Max(l => Math.Max(l.X1, l.X2)) : tableBbox.Right;
            int bottommost = hLines.Count > 0 ? hLines.Max(l => Math.Max(l.Y1, l.Y2)) : tableBbox.Bottom;
            int tableBottom = tableBbox.Bottom;

            foreach (var vl in vLines)
            {
                if (Math.Abs(bottommost - vl.Y2) <= minGap)
                {
                    vl.Y2 = bottommost;
                }
            }

            if (tableBottom > bottommost)
            {
                var boxInGap = boxes.Where(b => {
                    int centerY = (((Rect)b).Top + ((Rect)b).Bottom) / 2;
                    return centerY > bottommost && centerY < tableBottom;
                });

                if (boxInGap.Count() > 0)
                {
                    var newHLine = new Line(leftmost, tableBottom, rightmost, tableBottom);
                    hLines.Add(newHLine);

                    foreach (var vl in vLines)
                    {
                        if (Math.Abs(vl.Y2 - bottommost) <= 1 && IntersectBoxes(vl, boxInGap.ToList(), true))
                        {
                            vl.Y2 = tableBottom;
                        }
                    }
                }
            }
        }

        private void AlignRight(List<Line> hLines, List<Line> vLines, Rect tableBbox, IEnumerable<TextRect> boxes, int minGap = 10)
        {
            int topmost = hLines.Count > 0 ? hLines.Min(l => Math.Min(l.Y1, l.Y2)) : tableBbox.Top;
            int bottommost = hLines.Count > 0 ? hLines.Max(l => Math.Max(l.Y1, l.Y2)) : tableBbox.Bottom;
            int rightmost = vLines.Count > 0 ? vLines.Max(l => Math.Max(l.X1, l.X2)) : tableBbox.Right;
            int tableRight = tableBbox.Right;

            foreach (var hl in hLines)
            {
                if (Math.Abs(rightmost - hl.X2) <= minGap)
                {
                    hl.X2 = rightmost;
                }
            }

            if (tableRight > rightmost)
            {
                var boxInGap = boxes.Where(b => {
                    int centerX = (((Rect)b).Left + ((Rect)b).Right) / 2;
                    return centerX > rightmost && centerX < tableRight;
                });

                if (boxInGap.Count() > 0)
                {
                    var newVLine = new Line(tableRight, topmost, tableRight, bottommost);
                    vLines.Add(newVLine);

                    foreach (var hl in hLines)
                    {
                        if (Math.Abs(hl.X2 - rightmost) <= 1 && IntersectBoxes(hl, boxInGap.ToList()))
                        {
                            hl.X2 = tableRight;
                        }
                    }
                }
            }
        }

        private void CompsiteTable(List<Line> hLines, List<Line> vLines, bool implicitRows = false, bool implicitColumns = false)
        {
            _lines = new List<Line>();
            _lines.AddRange(hLines);
            _lines.AddRange(vLines);
            var cells = GetCells(hLines, vLines);
            _tables = TableDetector.DetectTables(cells, _contours, _lines, _charLength);
            _tables = _tables.Select(table => Implicit.ImplicitContent(table, _contours, _charLength, implicitRows, implicitColumns)).ToList();
            _tables = Consecutive.MergeConsecutiveTables(_tables, _contours);
            _tables = _tables.Where(tb => Math.Min(tb.NbRows, tb.NbColumns) >= 1).ToList();
        }

        private static List<Cell> GetCells(List<Line> hLines, List<Line> vLines)
        {
            var cells = Identification.GetCellsDataframe(hLines, vLines);

            var dedupCells = Deduplication.DeduplicateCells(cells);
            return dedupCells;
        }

        private static Mat ThresholdDarkAreas(Mat img, double charLength)
        {
            using var gray = new Mat();
            Cv2.CvtColor(img, gray, ColorConversionCodes.RGB2GRAY);

            if (Cv2.Mean(gray).Val0 <= 127)
            {
                Cv2.BitwiseNot(gray, gray);
            }

            int threshKernel = (int)charLength / 2 * 2 + 1;

            using Mat t_sauvola = new Mat();
            CvXImgProc.NiblackThreshold(gray, t_sauvola, 255, ThresholdTypes.BinaryInv, threshKernel, 0.2, LocalBinarizationMethods.Sauvola);
            Mat thresh = new Mat();
            Cv2.Compare(gray, t_sauvola, thresh, CmpType.LE);
            thresh = thresh * 255;
            thresh.ConvertTo(thresh, MatType.CV_8U);

            int blur_size = Math.Min(255, (int)(2 * charLength) / 2 * 2 + 1);
            using Mat blur = new Mat();
            Cv2.GaussianBlur(gray, blur, new Size(blur_size, blur_size), 0);
            using Mat mask = new Mat();
            Cv2.InRange(blur, 0, 100, mask);

            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            Mat binaryThresh = null;
            Mat d_gray = new Mat();
            Cv2.BitwiseNot(gray, d_gray);
            for (int idx = 1; idx < stats.Rows; idx++)
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                if (area / (double)(w * h) >= 0.5 && Math.Min(w, h) >= charLength && Math.Max(w, h) >= 5 * charLength)
                {
                    if (binaryThresh == null)
                    {
                        Mat bin_t_sauvola = new Mat();
                        CvXImgProc.NiblackThreshold(d_gray, bin_t_sauvola, 255, ThresholdTypes.BinaryInv, threshKernel, 0.2, LocalBinarizationMethods.Sauvola);

                        binaryThresh = new Mat();
                        //Cv2.BitwiseNot(gray, gray);
                        Cv2.Compare(d_gray, bin_t_sauvola, binaryThresh, CmpType.LE);
                        binaryThresh = binaryThresh * 255;
                        binaryThresh.ConvertTo(binaryThresh, MatType.CV_8U);
                    }
                    thresh[new Rect(x, y, w, h)] = binaryThresh[new Rect(x, y, w, h)];
                }
            }

            return thresh;
        }
    }
}
