using img2table.sharp.img2table.tables.objects;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.objects.Objects;
    
namespace img2table.sharp.img2table.tables.processing.borderless_tables.layout
{
    public class RLSA
    {
        public static Mat identify_text_mask(Mat thresh, List<Line> lines, double char_length, double median_line_sep, List<Table> existing_tables = null)
        {
            // Mask rows in image
            foreach (var line in lines)
            {
                if (line.Horizontal && line.Length >= 3 * char_length)
                {
                    Cv2.Rectangle(thresh, new Point(line.X1, line.Y1 - line.Thickness.Value / 2 - 1), new Point(line.X2, line.Y2 + line.Thickness.Value / 2 + 1), Scalar.Black, -1);
                }
                else if (line.Vertical && line.Length >= 2 * char_length)
                {
                    Cv2.Rectangle(thresh, new Point(line.X1 - line.Thickness.Value / 2 - 1, line.Y1), new Point(line.X2 + line.Thickness.Value / 2 + 1, line.Y2), Scalar.Black, -1);
                }
            }

            // Apply dilation
            Cv2.Dilate(thresh, thresh, Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 1)), iterations: 1);

            // Connected components
            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            int nLabels = Cv2.ConnectedComponentsWithStats(thresh, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            if (nLabels <= 1)
            {
                return thresh;
            }

            // Remove noise
            double average_height = stats.RowRange(1, nLabels).Col((int)ConnectedComponentsTypes.Height).Mean().Val0;
            double median_width = Utils.CalculateMedian(stats.RowRange(1, nLabels).Col((int)ConnectedComponentsTypes.Width));
            
            Mat cc_denoised = remove_noise(labels, stats, average_height, median_width);

            // Apply small RLSA
            Mat rlsa_small = adaptive_rlsa(cc_denoised, stats, 1, 3.5, 0.4);
            Cv2.Erode(255 * (rlsa_small.GreaterThan(0)).ToMat(), rlsa_small, Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 2)));

            // Identify obstacles and remove them from denoised cc array
            var max = new Mat();
            Cv2.Max(rlsa_small, thresh, max);
            Mat mask_obstacles = find_obstacles(max, char_length);
            Mat cc_obstacles = cc_denoised.Clone();
            cc_obstacles.SetTo(-1, mask_obstacles);

            // RLSA image
            Mat rlsa_image = adaptive_rlsa(cc_obstacles, stats, 5, 3.5, 0.4);

            // Connected components of the rlsa image
            Mat cc_stats_rlsa = new Mat();
            Cv2.ConnectedComponentsWithStats(Utils.CreateBinaryImage(rlsa_image, 255), new Mat(), cc_stats_rlsa, new Mat(), PixelConnectivity.Connectivity8, MatType.CV_32S);
            
            // Get text mask
            Mat text_mask = get_text_mask(thresh, cc_stats_rlsa, char_length, median_width);

            // Compute final image
            Mat cc_final = cc_obstacles.Clone();
            cc_final = cc_final.SetTo(-1, ~text_mask);
            Mat rlsa_final = adaptive_rlsa(cc_final, stats, 1.25, 3.5, 0.4);
                        
            // Remove all elements from existing tables
            foreach (var tb in existing_tables ?? new List<Table>())
            {
                for (int row = tb.Y1; row < tb.Y2; row++)
                {
                    for (int col = tb.X1; col < tb.X2; col++)
                    {
                        rlsa_final.Set<byte>(row, col, 0);
                    }
                }
            }
            
            Cv2.Erode(255 * rlsa_final, rlsa_final, Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 2)));
            return rlsa_final;
        }

        static Mat find_obstacles(Mat img, double min_width)
        {
            int minWidth = (int)Math.Ceiling(min_width);
            int h = img.Rows;
            int w = img.Cols;
            Mat mask_obstacles = new Mat(h, w, MatType.CV_8UC1, Scalar.All(0));

            for (int col = 0; col < w - minWidth; col++)
            {
                int prev_cc_position = -1;
                int row;
                for (row = 0; row < h; row++)
                {
                    int max_value = 0;
                    for (int idx = 0; idx < minWidth; idx++)
                    {
                        max_value = Math.Max(max_value, img.At<byte>(row, col + idx));
                    }

                    // Not a CC
                    if (max_value == 0)
                    {
                        continue;
                    }
                    else
                    {
                        int length = row - prev_cc_position - 1;
                        if (length > h / 5)
                        {
                            for (int id_row = prev_cc_position + 1; id_row < row; id_row++)
                            {
                                for (int idx = 0; idx < minWidth; idx++)
                                {
                                    mask_obstacles.Set<byte>(id_row, col + idx, 1);
                                }
                            }
                        }

                        // Update counters
                        prev_cc_position = row;
                    }
                }

                // Check ending
                int end_length = h - prev_cc_position - 1;
                if (end_length > h / 5)
                {
                    for (int id_row = prev_cc_position + 1; id_row < h; id_row++)
                    {
                        for (int idx = 0; idx < minWidth; idx++)
                        {
                            mask_obstacles.Set<byte>(id_row, col + idx, 1);
                        }
                    }
                }
            }

            return mask_obstacles;
        }

        static Mat remove_noise(Mat cc, Mat cc_stats, double average_height, double median_width)
        {
            Mat result = cc.Clone();

            for (int idx = 1; idx < cc_stats.Rows; idx++)
            {
                // Get stats
                int x = cc_stats.At<int>(idx, (int)ConnectedComponentsTypes.Left);
                int y = cc_stats.At<int>(idx, (int)ConnectedComponentsTypes.Top);
                int w = cc_stats.At<int>(idx, (int)ConnectedComponentsTypes.Width);
                int h = cc_stats.At<int>(idx, (int)ConnectedComponentsTypes.Height);
                int area = cc_stats.At<int>(idx, (int)ConnectedComponentsTypes.Area);

                // Check dashes
                bool is_dash = (w / (double)h >= 2) && (0.5 * median_width <= w && w <= 1.5 * median_width);

                if (is_dash)
                {
                    continue;
                }

                // Check removal conditions
                bool cond_height = h < average_height / 3;
                bool cond_elongation = Math.Max(h, w) / (double)Math.Max(Math.Min(h, w), 1) < 0.33;
                bool cond_low_density = area / (double)(Math.Max(w, 1) * Math.Max(h, 1)) < 0.08;

                if (cond_height || cond_elongation || cond_low_density)
                {
                    for (int row = y; row < y + h; row++)
                    {
                        for (int col = x; col < x + w; col++)
                        {
                            if (cc.At<int>(row, col) == idx)
                            {
                                result.Set<int>(row, col, 0);
                            }
                        }
                    }
                }
            }

            return result;
        }

        static Mat adaptive_rlsa(Mat cc, Mat cc_stats, double a, double th, double c)
        {
            Mat rsla_img = Utils.CreateBinaryImage(cc);

            int h = cc.Rows;
            int w = cc.Cols;

            for (int row = 0; row < h; row++)
            {
                int prev_cc_position = -1;
                int prev_cc_label = -1;

                for (int col = 0; col < w; col++)
                {
                    int label = cc.At<int>(row, col);

                    // Not a CC
                    if (label == 0)
                    {
                        continue;
                    }
                    // First encountered CC
                    else if (prev_cc_label == -1 || label == -1)
                    {
                        prev_cc_position = col;
                        prev_cc_label = label;
                        continue;
                    }
                    else if (label == prev_cc_label)
                    {
                        // Update all pixels in range
                        for (int i = prev_cc_position; i < col; i++)
                        {
                            rsla_img.Set<byte>(row, i, 1);
                        }
                    }
                    else
                    {
                        // Get CC characteristics
                        int x1_cc = cc_stats.At<int>(label, (int)ConnectedComponentsTypes.Left);
                        int y1_cc = cc_stats.At<int>(label, (int)ConnectedComponentsTypes.Top);
                        int width_cc = cc_stats.At<int>(label, (int)ConnectedComponentsTypes.Width);
                        int height_cc = cc_stats.At<int>(label, (int)ConnectedComponentsTypes.Height);

                        // Get other CC characteristics
                        int x1_prev = cc_stats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Left);
                        int y1_prev = cc_stats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Top);
                        int width_prev = cc_stats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Width);
                        int height_prev = cc_stats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Height);

                        // Compute metrics
                        int length = col - prev_cc_position - 1;
                        double height_ratio = Math.Max(height_cc, height_prev) / (double)Math.Max(Math.Min(height_cc, height_prev), 1);
                        int h_overlap = Math.Min(y1_cc + height_cc, y1_prev + height_prev) - Math.Max(y1_cc, y1_prev);

                        // Presence of other CC
                        bool no_other_cc = true;
                        int[] list_ccs = { -1, 0, label, prev_cc_label };
                        for (int y = Math.Max(0, row - 2); y < Math.Min(row + 3, h); y++)
                        {
                            for (int x = prev_cc_position + 1; x < col; x++)
                            {
                                if (!list_ccs.Contains(cc.At<int>(y, x)))
                                {
                                    no_other_cc = false;
                                }
                            }
                        }

                        // Check conditions
                        if ((length <= a * Math.Min(height_cc, height_prev))
                            && (height_ratio <= th)
                            && (h_overlap >= c * Math.Min(height_cc, height_prev))
                            && no_other_cc)
                        {
                            for (int i = prev_cc_position; i < col; i++)
                            {
                                rsla_img.Set<byte>(row, i, 1);
                            }
                        }
                    }

                    // Update counters
                    prev_cc_position = col;
                    prev_cc_label = label;
                }
            }

            return rsla_img;
        }

        static Mat get_text_mask(Mat thresh, Mat cc_stats_rlsa, double char_length, double median_width)
        {
            Mat text_mask = new Mat(thresh.Size(), MatType.CV_8UC1, Scalar.All(0));

            // Get average height
            double num = 0, denum = 0;
            for (int i = 1; i < cc_stats_rlsa.Rows; i++)
            {
                int height = cc_stats_rlsa.At<int>(i, (int)ConnectedComponentsTypes.Height);
                int area = cc_stats_rlsa.At<int>(i, (int)ConnectedComponentsTypes.Area);
                num += height * area;
                denum += area;
            }
            double Hm = num / Math.Max(denum, 1);

            for (int cc_idx = 0; cc_idx < cc_stats_rlsa.Rows; cc_idx++)
            {
                int x = cc_stats_rlsa.At<int>(cc_idx, (int)ConnectedComponentsTypes.Left);
                int y = cc_stats_rlsa.At<int>(cc_idx, (int)ConnectedComponentsTypes.Top);
                int w = cc_stats_rlsa.At<int>(cc_idx, (int)ConnectedComponentsTypes.Width);
                int h = cc_stats_rlsa.At<int>(cc_idx, (int)ConnectedComponentsTypes.Height);
                int area = cc_stats_rlsa.At<int>(cc_idx, (int)ConnectedComponentsTypes.Area);

                // Check for dashes
                if ((w / (double)h >= 2) && (0.5 * median_width <= w && w <= 1.5 * median_width))
                {
                    for (int row = y; row < y + h; row++)
                    {
                        for (int col = x; col < x + w; col++)
                        {
                            text_mask.Set<byte>(row, col, 255);
                        }
                    }
                    continue;
                }

                if (cc_idx == 0 || Math.Min(w, h) <= 2 * char_length / 3)
                {
                    continue;
                }

                // Get horizontal white to black transitions
                int h_tc = 0;
                for (int row = y; row < y + h; row++)
                {
                    byte prev_value = 0;
                    for (int col = x; col < x + w; col++)
                    {
                        byte value = thresh.At<byte>(row, col);

                        if (value == 255)
                        {
                            if (prev_value == 0)
                            {
                                h_tc += 1;
                            }
                        }
                        prev_value = value;
                    }
                }

                // Get vertical white to black transitions
                int v_tc = 0, nb_cols = 0;
                for (int col = x; col < x + w; col++)
                {
                    int has_pixel = 0;
                    byte prev_value = 0;
                    for (int row = y; row < y + h; row++)
                    {
                        byte value = thresh.At<byte>(row, col);

                        if (value == 255)
                        {
                            has_pixel = 1;
                            if (prev_value == 0)
                            {
                                v_tc += 1;
                            }
                        }
                        prev_value = value;
                    }

                    nb_cols += has_pixel;
                }

                // Update metrics
                double H = h;
                double R = (double)w / Math.Max(h, 1);
                double THx = (double)h_tc / Math.Max(nb_cols, 1);
                double TVx = (double)v_tc / Math.Max(nb_cols, 1);
                double THy = (double)h_tc / Math.Max(h, 1);

                // Apply rules to identify text elements
                bool is_text = false;
                if (0.8 * Hm <= H && H <= 1.2 * Hm)
                {
                    is_text = true;
                }
                else if (H < 0.8 * Hm && 1.2 < THx && THx < 3.5)
                {
                    is_text = true;
                }
                else if (THx < 0.2 && R > 5 && 0.95 < TVx && TVx < 1.05)
                {
                    is_text = false;
                }
                else if (THx > 5 && R < 0.2 && 0.95 < THy && THy < 1.05)
                {
                    is_text = false;
                }
                else if (H > 1.2 * Hm && 1.2 < THx && THx < 3.5 && 1.2 < TVx && TVx < 3.5)
                {
                    is_text = true;
                }

                if (is_text)
                {
                    for (int row = y; row < y + h; row++)
                    {
                        for (int col = x; col < x + w; col++)
                        {
                            text_mask.Set<byte>(row, col, 255);
                        }
                    }
                }
            }

            return text_mask;
        }

    }
}
