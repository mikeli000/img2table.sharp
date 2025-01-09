using img2table.sharp.img2table.tables.objects;
using img2table.sharp.img2table.tables.processing.bordered_tables;
using img2table.sharp.img2table.tables.processing.bordered_tables.cells;
using img2table.sharp.img2table.tables.processing.bordered_tables.tables;
using img2table.sharp.img2table.tables.processing.borderless_tables;
using OpenCvSharp;
using OpenCvSharp.XImgProc;
using System.Threading;
using static img2table.sharp.img2table.tables.objects.Objects;

namespace img2table.sharp.img2table.tables
{
    public class TableImage
    {
        private Mat img;
        private Mat thresh;
        private double char_length;
        private static double default_char_length = 11;
        private double? median_line_sep;
        private List<Cell> contours;
        private List<Line> lines;
        private List<Table> tables;

        public TableImage(Mat img)
        {
            this.img = img;

            this.thresh = threshold_dark_areas(this.img, default_char_length);
            // Compute image metrics
            var t = Metrics.compute_img_metrics(this.thresh.Clone());
            this.char_length = t.Item1.Value;
            this.median_line_sep = t.Item2;
            this.contours = t.Item3;
        }

        public List<Table> extract_tables(bool implicit_rows, bool implicit_columns, bool borderless_tables)
        {
            // Extract bordered tables
            extract_bordered_tables(implicit_rows, implicit_columns);

            if (borderless_tables)
            {
                // Extract borderless tables
                extract_borderless_tables();
            }

            return tables;
        }


        public void extract_borderless_tables()
        {
            if (median_line_sep != null)
            {
                thresh = threshold_dark_areas(img, char_length);
                // 提取无边框的表格
                List<Table> borderlessTables = BorderlessTables.identify_borderless_tables(thresh, lines, char_length, median_line_sep.Value, contours, tables);

                // 添加到表格列表中
                tables.AddRange(borderlessTables.Where(tb => tb.NbRows >= 2 && tb.NbColumns >= 3));
            }
        }

        public void extract_bordered_tables(bool implicit_rows = false, bool implicit_columns = false)
        {
            // 计算线检测的参数
            int min_line_length = median_line_sep.HasValue ? (int)Math.Min(1.5 * median_line_sep.Value, 4 * char_length) : 20;

            // Detect rows in image
            var (h_lines, v_lines) = Lines.detect_lines(img, contours, char_length, min_line_length);
            // 合并水平线和垂直线
            this.lines = new List<Line>();
            lines.AddRange(h_lines);
            lines.AddRange(v_lines);

            // Create cells from rows
            var cells = get_cells(h_lines, v_lines);

            // Create tables from rows
            tables = Tables.get_tables(cells, contours, lines, char_length);

            // If necessary, detect implicit rows
            tables = tables.Select(table => Implicit.implicit_content(table, contours, char_length, implicit_rows, implicit_rows)).ToList();

            // Merge consecutive tables
            tables = Consecutive.merge_consecutive_tables(tables, contours);

            // Post filter bordered tables
            tables = tables.Where(tb => Math.Min(tb.NbRows, tb.NbColumns) >= 2).ToList();
        }

        private static List<Cell> get_cells(List<Line> h_lines, List<Line> v_lines)
        {
            // Create dataframe with cells from horizontal and vertical rows
            var cells = Identification.get_cells_dataframe(h_lines, v_lines);

            // Deduplicate cells
            var dedup_cells = Deduplication.deduplicate_cells(cells);
            return dedup_cells;
        }

        private static Mat threshold_dark_areas(Mat img, double char_length)
        {
            // Convert to grayscale
            using var gray = new Mat();
            Cv2.CvtColor(img, gray, ColorConversionCodes.RGB2GRAY);

            // If image is mainly black, revert the image
            if (Cv2.Mean(gray).Val0 <= 127)
            {
                Cv2.BitwiseNot(gray, gray);
            }

            int thresh_kernel = (int)(char_length) / 2 * 2 + 1;

            // Threshold original image
            using Mat t_sauvola = new Mat();
            CvXImgProc.NiblackThreshold(gray, t_sauvola, 255, ThresholdTypes.BinaryInv, thresh_kernel, 0.2, LocalBinarizationMethods.Sauvola);
            // Create a binary image where pixels are set to 255 if the corresponding pixel in gray is less than or equal to t_sauvola
            Mat thresh = new Mat();
            Cv2.Compare(gray, t_sauvola, thresh, CmpType.LE);
            thresh = thresh * 255;
            thresh.ConvertTo(thresh, MatType.CV_8U);

            // Mask on areas with dark background
            int blur_size = Math.Min(255, (int)(2 * char_length) / 2 * 2 + 1);
            using Mat blur = new Mat();
            Cv2.GaussianBlur(gray, blur, new Size(blur_size, blur_size), 0);
            using Mat mask = new Mat();
            Cv2.InRange(blur, 0, 100, mask);

            // Identify dark areas
            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            Cv2.ConnectedComponentsWithStats(mask, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            // For each dark area, use binary threshold instead of regular threshold
            Mat binary_thresh = null;
            Mat d_gray = new Mat();
            Cv2.BitwiseNot(gray, d_gray);
            for (int idx = 1; idx < stats.Rows; idx++)
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                if (area / (double)(w * h) >= 0.5 && Math.Min(w, h) >= char_length && Math.Max(w, h) >= 5 * char_length)
                {
                    if (binary_thresh == null)
                    {
                        // Threshold binary image

                        Mat bin_t_sauvola = new Mat();
                        CvXImgProc.NiblackThreshold(d_gray, bin_t_sauvola, 255, ThresholdTypes.BinaryInv, thresh_kernel, 0.2, LocalBinarizationMethods.Sauvola);

                        binary_thresh = new Mat();
                        //Cv2.BitwiseNot(gray, gray);
                        Cv2.Compare(d_gray, bin_t_sauvola, binary_thresh, CmpType.LE);
                        binary_thresh = binary_thresh * 255;
                        binary_thresh.ConvertTo(binary_thresh, MatType.CV_8U);
                    }
                    thresh[new Rect(x, y, w, h)] = binary_thresh[new Rect(x, y, w, h)];
                }
            }

            return thresh;
        }
    }
}
