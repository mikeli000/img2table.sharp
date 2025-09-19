using OpenCvSharp;
using OpenCvSharp.XImgProc;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage.TableElement;
using System.Collections.Generic;

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
        public static string _debug_temp_folder = Path.Combine(Path.GetTempPath(), "img2table");
        private bool _debug_draw_lines = true;
        private bool _debug_draw_kv_table = true;

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

        public List<Table> ExtractTables(bool implicitRows, bool implicitColumns, bool borderlessTables, Rect? tableBbox = null, IEnumerable<TextRect> textBoxes = null, bool isImage = false)
        {
            ExtractBorderedTables(implicitRows, implicitColumns, tableBbox, textBoxes, isImage);
            if (_tables != null && _tables.Count > 0)
            {
                if (_tables.Count == 1 && _tables[0] is KeyValueTable)
                {
                    return _tables;
                }

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
                    if (reCompsiteTable)
                    {
                        ExtractBorderedTables(implicitRows, implicitColumns, null, null, isImage);
                    }
                }
                if (_tables.Count <= 0)
                {
                    return temp;
                }

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

        private void ExtractBorderedTables(bool implicitRows = false, bool implicitColumns = false, Rect? tableBbox = null, IEnumerable<TextRect> textBoxes = null, bool isImage = false)
        {
            int minLineLength = _medianLineSep.HasValue ? (int)Math.Max(1.5 * _medianLineSep.Value, 4 * _charLength) : 20;
            var (hLines, vLines) = LineDetector.DetectLines(_img, _contours, _charLength, minLineLength);
            if (isImage)
            {
                if (_debug_draw_lines)
                {
                    DebugDrawLines(_img, hLines, vLines, textBoxes);
                }

                CompsiteTable(hLines, vLines, implicitRows, implicitColumns);
                return;
            }
            

            var originalHLines = hLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();
            var originalVLines = vLines.Select(l => new Line(l.X1, l.Y1, l.X2, l.Y2)).ToList();

            RemoveNoiseLines(hLines, vLines, textBoxes, minLineLength);

            if (tableBbox != null)
            {
                var (h, v) = SolidLineNormalizer.Normalize(hLines, vLines, textBoxes, tableBbox.Value);
                hLines = h;
                vLines = v;

                if (PostionedTableCellDetector.TryDetectKVTable(hLines, vLines, tableBbox.Value, textBoxes, _charLength, out var kvTable))
                {
                    if (kvTable != null)
                    {
                        _tables = _tables?? new List<Table>();
                        _tables.Add(kvTable);

                        if (_debug_draw_kv_table)
                        {
                            DebugDrawKVTables(_img, _tables);
                        }
                        
                        return;
                    }
                }

                if (PostionedTableCellDetector.TryDetectLines(hLines, vLines, tableBbox.Value, textBoxes, _charLength, out var detectHLines, out var detectVLines))
                {
                    hLines = detectHLines;
                    vLines = detectVLines;
                }

                vLines = vLines.OrderBy(vl => vl.X1).ToList();
                AlignTableBorder(hLines, vLines, tableBbox.Value, textBoxes);

                if (_debug_draw_lines)
                {
                    DebugDrawLines(_img, hLines, vLines, textBoxes);
                }

                CompsiteTable(hLines, vLines, implicitRows, implicitColumns, textBoxes);
            }
            else
            {
                _shouldOCR = implicitRows || implicitColumns;
                CompsiteTable(hLines, vLines, implicitRows, implicitColumns, null);
            }
        }
            
        private void RemoveEmptyRowAndCol(Table table, IEnumerable<TextRect> textBoxes)
        {
            var copy = new List<TextRect>(textBoxes);
            List<Row> emptyRows = new List<Row>();

            int rows = table.NbRows;
            int cols = table.NbColumns;
            int[][] flags = new int[rows][];
            for (int r = 0; r < rows; r++)
            {
                var row = table.Rows.ElementAt(r);
                var colFlags = new int[cols];
                for (int c = 0; c < cols; c++)
                {
                    colFlags[c] = 1;
                    var cell = row.Cells.ElementAt(c);
                    if (!LineUtils.ContainsTextBox(cell.Rect(), copy, 0)) // strict mode
                    {
                        colFlags[c] = 0;
                    }
                }
                flags[r] = colFlags;
            }

            for (int r = 0; r < flags.Count(); r++)
            {
                var rowFlags = flags[r];
                if (rowFlags.All(f => f == 0))
                {
                    emptyRows.Add(table.Rows.ElementAt(r));
                }
            }

            List<int> emptyCols = new List<int>();
            for (int c = 0; c < cols; c++)
            {
                bool emptyCol = true;
                for (int r = 0; r < rows; r++)
                {
                    if (flags[r][c] == 1)
                    {
                        emptyCol = false;
                        break;
                    }
                }

                if (emptyCol)
                {
                    emptyCols.Add(c);
                }
            }

            foreach (var row in emptyRows)
            {
                table.Items.Remove(row);
            }

            if (emptyCols.Count > 0)
            {
                foreach (var row in table.Rows)
                {
                    for (int c = emptyCols.Count - 1; c >= 0; c--)
                    {
                        row.Cells.RemoveAt(emptyCols[c]);
                    }
                }
            }
        }

        private void RemoveNoiseLines(List<Line> hLines, List<Line> vLines, IEnumerable<TextRect> textBoxes, int minLineLength)
        {
            if (textBoxes == null || textBoxes.Count() <= 0)
            {
                return;
            }

            int minHLineLength = minLineLength * 2;
            hLines.RemoveAll(l => l.Length < minHLineLength);
            int minVLineLength = minLineLength * 2;
            vLines.RemoveAll(l => l.Length < minVLineLength && !LineUtils.IntersectAnyLine(l, hLines));

            if (hLines != null && hLines.Count() > 0)
            {
                hLines.RemoveAll(l => l.Y1 != l.Y2);
                var intersectHLines = hLines.Where(l => LineUtils.IntersectTextBoxes(l, textBoxes, delta: 4)); // Tolerance Mode
                if (intersectHLines?.Count() > 0)
                {
                    hLines.RemoveAll(l => intersectHLines.Contains(l));
                }

                int topmost = textBoxes.Min(b => b.Top) - 1;
                var topEdgeLines = hLines.Where(l => l.Y1 < topmost).ToList();
                if (topEdgeLines.Count() > 1)
                {
                    var y1 = topEdgeLines[0].Y1;
                    if (!topEdgeLines.All(l => l.Y1 == y1))
                    {
                        var longest = topEdgeLines.OrderByDescending(l => l.Length).First();
                        hLines.RemoveAll(l => topEdgeLines.Contains(l) && l != longest);
                    }
                }

                int bottommost = textBoxes.Max(b => b.Bottom) + 1;
                var bottomEdgeLines = hLines.Where(l => l.Y1 > bottommost).ToList();
                if (bottomEdgeLines.Count() > 1)
                {
                    var y1 = bottomEdgeLines[0].Y1;
                    if (!bottomEdgeLines.All(l => l.Y1 == y1))
                    {
                        var longest = bottomEdgeLines.OrderByDescending(l => l.Length).First();
                        hLines.RemoveAll(l => bottomEdgeLines.Contains(l) && l != longest);
                    }
                }
            }

            if (vLines != null && vLines.Count() > 0)
            {
                vLines.RemoveAll(l => l.X1 != l.X2);
                var intersectVLines = vLines.Where(l => LineUtils.IntersectTextBoxes(l, textBoxes, delta: 4)); // Tolerance Mode
                if (intersectVLines?.Count() > 0)
                {
                    vLines.RemoveAll(l => intersectVLines.Contains(l));
                }

                int leftmost = textBoxes.Min(b => b.Left) - 1;
                var leftEdgeLines = vLines.Where(l => l.X1 < leftmost).ToList();
                if (leftEdgeLines.Count() > 1)
                {
                    var x1 = leftEdgeLines[0].X1;
                    if (!leftEdgeLines.All(l => l.X1 == x1))
                    {
                        var longest = leftEdgeLines.OrderByDescending(l => l.Length).First();
                        vLines.RemoveAll(l => leftEdgeLines.Contains(l) && l != longest);
                    }
                }

                int rightmost = textBoxes.Max(b => b.Right) + 1;
                var rightEdgeLines = vLines.Where(l => l.X1 > rightmost).ToList();
                if (rightEdgeLines.Count() > 1)
                {
                    var x1 = rightEdgeLines[0].X1;
                    if (!rightEdgeLines.All(l => l.X1 == x1))
                    {
                        var longest = rightEdgeLines.OrderByDescending(l => l.Length).First();
                        vLines.RemoveAll(l => rightEdgeLines.Contains(l) && l != longest);
                    }
                }
            }
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

        private void CompsiteTable(List<Line> hLines, List<Line> vLines, bool implicitRows = false, bool implicitColumns = false, IEnumerable<TextRect> textBoxes = null)
        {
            _lines = new List<Line>();
            _lines.AddRange(hLines);
            _lines.AddRange(vLines);
            var cells = GetCells(hLines, vLines);
            _tables = TableDetector.DetectTables(cells, _contours, _lines, _charLength);
            _tables = _tables.Select(table => Implicit.ImplicitContent(table, _contours, _charLength, implicitRows, implicitColumns)).ToList();
            _tables = Consecutive.MergeConsecutiveTables(_tables, _contours);
            _tables = _tables.Where(tb => Math.Min(tb.NbRows, tb.NbColumns) >= 1).ToList();

            if (textBoxes != null)
            {
                foreach (var table in _tables)
                {
                    RemoveEmptyRowAndCol(table, textBoxes);
                }
            }
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

        private static void DebugDrawKVTables(Mat src, List<Table> kvTables)
        {
            var debugImage = src.Clone();

            var table = kvTables.FirstOrDefault();
            foreach (var row in table.Rows)
            {
                foreach (var cell in row.Cells)
                {
                    Cv2.Rectangle(debugImage, new Rect(cell.X1, cell.Y1, cell.Width, cell.Height), Scalar.Yellow, 1);
                }
            }

            if (!Directory.Exists(_debug_temp_folder))
            {
                Directory.CreateDirectory(_debug_temp_folder);
            }

            var file = Path.Combine(_debug_temp_folder, $@"{Guid.NewGuid().ToString()}.png");
            Cv2.ImWrite(file, debugImage);
        }

        private static void DebugDrawLines(Mat src, IEnumerable<Line> hLines, IEnumerable<Line> vLines, IEnumerable<TextRect> textBoxes)
        {
            var debugImage = src.Clone();
            foreach (var line in hLines)
            {
                Cv2.Line(debugImage, line.X1, line.Y1, line.X2, line.Y2, Scalar.Blue, 2);
            }
            foreach (var line in vLines)
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

            if (!Directory.Exists(_debug_temp_folder))
            {
                Directory.CreateDirectory(_debug_temp_folder);
            }

            var file = Path.Combine(_debug_temp_folder, $@"{Guid.NewGuid().ToString()}.png");
            Cv2.ImWrite(file, debugImage);
        }
    }
}
