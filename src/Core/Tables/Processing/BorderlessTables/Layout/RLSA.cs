using Img2table.Sharp.Core.Tables.Objects;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Img2table.Sharp.Core.Tables.Objects.Objects;
    
namespace Img2table.Sharp.Core.Tables.Processing.BorderlessTables.layout
{
    public class RLSA
    {
        public static Mat IdentifyTextMask(Mat thresh, List<Line> lines, double charLength, List<Objects.Table> existing_tables = null)
        {
            foreach (var line in lines)
            {
                if (line.Horizontal && line.Length >= 3 * charLength)
                {
                    Cv2.Rectangle(thresh, new Point(line.X1, line.Y1 - line.Thickness.Value / 2 - 1), new Point(line.X2, line.Y2 + line.Thickness.Value / 2 + 1), Scalar.Black, -1);
                }
                else if (line.Vertical && line.Length >= 2 * charLength)
                {
                    Cv2.Rectangle(thresh, new Point(line.X1 - line.Thickness.Value / 2 - 1, line.Y1), new Point(line.X2 + line.Thickness.Value / 2 + 1, line.Y2), Scalar.Black, -1);
                }
            }

            Cv2.Dilate(thresh, thresh, Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 1)), iterations: 1);

            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            int nLabels = Cv2.ConnectedComponentsWithStats(thresh, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            if (nLabels <= 1)
            {
                return thresh;
            }

            double average_height = stats.RowRange(1, nLabels).Col((int)ConnectedComponentsTypes.Height).Mean().Val0;
            double median_width = Utils.CalculateMedian(stats.RowRange(1, nLabels).Col((int)ConnectedComponentsTypes.Width));
            
            Mat cc_denoised = RemoveNoise(labels, stats, average_height, median_width);

            Mat rlsa_small = AdaptiveRLSA(cc_denoised, stats, 1, 3.5, 0.4);
            Cv2.Erode(255 * (rlsa_small.GreaterThan(0)).ToMat(), rlsa_small, Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 2)));

            var max = new Mat();
            Cv2.Max(rlsa_small, thresh, max);
            Mat mask_obstacles = FindObstacles(max, charLength);
            Mat cc_obstacles = cc_denoised.Clone();
            cc_obstacles.SetTo(-1, mask_obstacles);

            Mat rlsa_image = AdaptiveRLSA(cc_obstacles, stats, 5, 3.5, 0.4);

            Mat cc_stats_rlsa = new Mat();
            Cv2.ConnectedComponentsWithStats(Utils.CreateBinaryImage(rlsa_image, 255), new Mat(), cc_stats_rlsa, new Mat(), PixelConnectivity.Connectivity8, MatType.CV_32S);
            
            Mat text_mask = GetTextMask(thresh, cc_stats_rlsa, charLength, median_width);

            Mat cc_final = cc_obstacles.Clone();
            cc_final = cc_final.SetTo(-1, ~text_mask);
            Mat rlsa_final = AdaptiveRLSA(cc_final, stats, 1.25, 3.5, 0.4);
                        
            foreach (var tb in existing_tables ?? new List<Objects.Table>())
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

        private static Mat FindObstacles(Mat img, double min_width)
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

                        prev_cc_position = row;
                    }
                }

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

        private static Mat RemoveNoise(Mat cc, Mat ccStats, double averageHeight, double medianWidth)
        {
            Mat result = cc.Clone();

            for (int idx = 1; idx < ccStats.Rows; idx++)
            {
                int x = ccStats.At<int>(idx, (int)ConnectedComponentsTypes.Left);
                int y = ccStats.At<int>(idx, (int)ConnectedComponentsTypes.Top);
                int w = ccStats.At<int>(idx, (int)ConnectedComponentsTypes.Width);
                int h = ccStats.At<int>(idx, (int)ConnectedComponentsTypes.Height);
                int area = ccStats.At<int>(idx, (int)ConnectedComponentsTypes.Area);

                bool is_dash = (w / (double)h >= 2) && (0.5 * medianWidth <= w && w <= 1.5 * medianWidth);

                if (is_dash)
                {
                    continue;
                }

                bool cond_height = h < averageHeight / 3;
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

        private static Mat AdaptiveRLSA(Mat cc, Mat ccStats, double a, double th, double c)
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

                    if (label == 0)
                    {
                        continue;
                    }
                    else if (prev_cc_label == -1 || label == -1)
                    {
                        prev_cc_position = col;
                        prev_cc_label = label;
                        continue;
                    }
                    else if (label == prev_cc_label)
                    {
                        for (int i = prev_cc_position; i < col; i++)
                        {
                            rsla_img.Set<byte>(row, i, 1);
                        }
                    }
                    else
                    {
                        int x1_cc = ccStats.At<int>(label, (int)ConnectedComponentsTypes.Left);
                        int y1_cc = ccStats.At<int>(label, (int)ConnectedComponentsTypes.Top);
                        int width_cc = ccStats.At<int>(label, (int)ConnectedComponentsTypes.Width);
                        int height_cc = ccStats.At<int>(label, (int)ConnectedComponentsTypes.Height);

                        int x1_prev = ccStats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Left);
                        int y1_prev = ccStats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Top);
                        int width_prev = ccStats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Width);
                        int height_prev = ccStats.At<int>(prev_cc_label, (int)ConnectedComponentsTypes.Height);

                        int length = col - prev_cc_position - 1;
                        double height_ratio = Math.Max(height_cc, height_prev) / (double)Math.Max(Math.Min(height_cc, height_prev), 1);
                        int h_overlap = Math.Min(y1_cc + height_cc, y1_prev + height_prev) - Math.Max(y1_cc, y1_prev);

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

                    prev_cc_position = col;
                    prev_cc_label = label;
                }
            }

            return rsla_img;
        }

        private static Mat GetTextMask(Mat thresh, Mat ccStatsRLSA, double charLength, double medianWidth)
        {
            Mat textMask = new Mat(thresh.Size(), MatType.CV_8UC1, Scalar.All(0));

            double num = 0, denum = 0;
            for (int i = 1; i < ccStatsRLSA.Rows; i++)
            {
                int height = ccStatsRLSA.At<int>(i, (int)ConnectedComponentsTypes.Height);
                int area = ccStatsRLSA.At<int>(i, (int)ConnectedComponentsTypes.Area);
                num += height * area;
                denum += area;
            }
            double Hm = num / Math.Max(denum, 1);

            for (int cc_idx = 0; cc_idx < ccStatsRLSA.Rows; cc_idx++)
            {
                int x = ccStatsRLSA.At<int>(cc_idx, (int)ConnectedComponentsTypes.Left);
                int y = ccStatsRLSA.At<int>(cc_idx, (int)ConnectedComponentsTypes.Top);
                int w = ccStatsRLSA.At<int>(cc_idx, (int)ConnectedComponentsTypes.Width);
                int h = ccStatsRLSA.At<int>(cc_idx, (int)ConnectedComponentsTypes.Height);
                int area = ccStatsRLSA.At<int>(cc_idx, (int)ConnectedComponentsTypes.Area);

                if ((w / (double)h >= 2) && (0.5 * medianWidth <= w && w <= 1.5 * medianWidth))
                {
                    for (int row = y; row < y + h; row++)
                    {
                        for (int col = x; col < x + w; col++)
                        {
                            textMask.Set<byte>(row, col, 255);
                        }
                    }
                    continue;
                }

                if (cc_idx == 0 || Math.Min(w, h) <= 2 * charLength / 3)
                {
                    continue;
                }

                int h_tc = 0;
                for (int row = y; row < y + h; row++)
                {
                    byte prevValue = 0;
                    for (int col = x; col < x + w; col++)
                    {
                        byte value = thresh.At<byte>(row, col);

                        if (value == 255)
                        {
                            if (prevValue == 0)
                            {
                                h_tc += 1;
                            }
                        }
                        prevValue = value;
                    }
                }

                int v_tc = 0, nb_cols = 0;
                for (int col = x; col < x + w; col++)
                {
                    int hasPixel = 0;
                    byte prevValue = 0;
                    for (int row = y; row < y + h; row++)
                    {
                        byte value = thresh.At<byte>(row, col);

                        if (value == 255)
                        {
                            hasPixel = 1;
                            if (prevValue == 0)
                            {
                                v_tc += 1;
                            }
                        }
                        prevValue = value;
                    }

                    nb_cols += hasPixel;
                }

                double H = h;
                double R = (double)w / Math.Max(h, 1);
                double THx = (double)h_tc / Math.Max(nb_cols, 1);
                double TVx = (double)v_tc / Math.Max(nb_cols, 1);
                double THy = (double)h_tc / Math.Max(h, 1);

                bool isText = false;
                if (0.8 * Hm <= H && H <= 1.2 * Hm)
                {
                    isText = true;
                }
                else if (H < 0.8 * Hm && 1.2 < THx && THx < 3.5)
                {
                    isText = true;
                }
                else if (THx < 0.2 && R > 5 && 0.95 < TVx && TVx < 1.05)
                {
                    isText = false;
                }
                else if (THx > 5 && R < 0.2 && 0.95 < THy && THy < 1.05)
                {
                    isText = false;
                }
                else if (H > 1.2 * Hm && 1.2 < THx && THx < 3.5 && 1.2 < TVx && TVx < 3.5)
                {
                    isText = true;
                }

                if (isText)
                {
                    for (int row = y; row < y + h; row++)
                    {
                        for (int col = x; col < x + w; col++)
                        {
                            textMask.Set<byte>(row, col, 255);
                        }
                    }
                }
            }

            return textMask;
        }
    }
}
