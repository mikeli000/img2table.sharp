using OpenCvSharp;
using OpenCvSharp.XImgProc;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout;

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

        public TableImage(Mat img)
        {
            _img = img;

            _thresh = ThresholdDarkAreas(_img, DefaultCharLength);
            var t = Metrics.ComputeImgMetrics(_thresh.Clone());
            _charLength = t.Item1.Value;
            _medianLineSep = t.Item2;
            _contours = t.Item3;
        }

        public List<Table> ExtractTables(bool implicitRows, bool implicitColumns, bool borderlessTables)
        {
            ExtractBorderedTables(implicitRows, implicitColumns);

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

        private void ExtractBorderedTables(bool implicitRows = false, bool implicitColumns = false)
        {
            int minLineLength = _medianLineSep.HasValue ? (int)Math.Min(1.5 * _medianLineSep.Value, 4 * _charLength) : 20;

            var (hLines, vLines) = LineDetector.DetectLines(_img, _contours, _charLength, minLineLength);
            _lines = new List<Line>();
            _lines.AddRange(hLines);
            _lines.AddRange(vLines);

            var cells = GetCells(hLines, vLines);

            _tables = TableDetector.DetectTables(cells, _contours, _lines, _charLength);

            _tables = _tables.Select(table => Implicit.ImplicitContent(table, _contours, _charLength, implicitRows, implicitColumns)).ToList();

            _tables = Consecutive.MergeConsecutiveTables(_tables, _contours);

            _tables = _tables.Where(tb => Math.Min(tb.NbRows, tb.NbColumns) >= 2).ToList();
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
