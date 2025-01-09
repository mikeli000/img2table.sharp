using img2table.sharp.img2table.tables.objects;
using OpenCvSharp;

namespace img2table.sharp.img2table.tables
{
    public class Metrics
    {
        public static Tuple<double?, double?, List<Cell>> compute_img_metrics(Mat thresh)
        {
            // Compute average character length based on connected components analysis
            var t = compute_char_length(thresh);
            var char_length = t.Item1;
            var thresh_chars = t.Item2;
            var chars_array = t.Item3;

            if (char_length == null) 
            {
                return new (null, null, null);
            }
            
            return compute_median_line_sep(char_length.Value, thresh_chars, chars_array);
        }

        private static Tuple<double?, double?, List<Cell>> compute_median_line_sep(double char_length, Mat thresh_chars, Mat chars_array)
        {
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size((int)(char_length / 2 + 1), (int)(char_length / 3 + 1)));
            Cv2.MorphologyEx(thresh_chars, thresh_chars, MorphTypes.Close, kernel);

            Mat labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            Cv2.ConnectedComponentsWithStats(thresh_chars, labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);

            Mat stats_contours = recompute_contours(stats, chars_array);

            var row_separations = get_row_separations(stats_contours);

            double? median_line_sep = GetMedianLineSeparation(row_separations);
            if (median_line_sep == null)
            {
                return new (null, null, null);
            }

            List<Cell> contours = new List<Cell>();
            for (int idx = 1; idx < stats_contours.Rows; idx++) // 从1开始，跳过第一个元素
            {
                int x = stats_contours.At<int>(idx, 0);
                int y = stats_contours.At<int>(idx, 1);
                int w = stats_contours.At<int>(idx, 2);
                int h = stats_contours.At<int>(idx, 3);

                contours.Add(new Cell(x, y, x + w, y + h));
            }

            return new (char_length, median_line_sep, contours);
        }

        static List<double> get_row_separations(Mat stats)
        {
            List<double> rowSeparations = new List<double>();

            for (int i = 1; i < stats.Rows; i++)
            {
                // 获取统计信息
                int xi = stats.At<int>(i, 0);
                int yi = stats.At<int>(i, 1);
                //int wi = stats.At<int>(i, 2);
                int hi = stats.At<int>(i, 3);
                double rowSeparation = 1e6;

                for (int j = 1; j < stats.Rows; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    // 获取统计信息
                    int xj = stats.At<int>(j, 0);
                    int yj = stats.At<int>(j, 1);
                    //int wj = stats.At<int>(j, 2);
                    int hj = stats.At<int>(j, 3);

                    // 计算水平重叠和垂直位置
                    int hOverlap = Math.Min(xi + hi, xj + hj) - Math.Max(xi, xj);
                    double vPosI = (2 * yi + hi) / 2.0;
                    double vPosJ = (2 * yj + hj) / 2.0;
                    if (hOverlap <= 0 || vPosJ <= vPosI)
                    {
                        continue;
                    }

                    if (vPosJ - vPosI <= rowSeparation)
                    {
                        rowSeparation = vPosJ - vPosI;
                    }
                }

                if (rowSeparation < 1e6)
                {
                    rowSeparations.Add(rowSeparation);
                }
            }

            return rowSeparations;
        }

        static double? GetMedianLineSeparation(List<double> rowSeparations)
        {
            if (rowSeparations.Count == 0)
            {
                return null;
            }

            // 将行分隔值转换为整数并进行处理
            var processedSeparations = rowSeparations
                .Select(sep => 2 * Math.Floor(sep / 2) + 1)
                .GroupBy(sep => sep)
                .Select(group => new { Sep = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ToList();

            // 获取出现次数最多的行分隔值
            double medianLineSep = processedSeparations.First().Sep;

            return medianLineSep;
        }

        private static Mat recompute_contours(Mat stats, Mat charsArray)
        {
            List<List<int>> listContours = new List<List<int>>();
            for (int idx = 1; idx < stats.Rows; idx++) // 从1开始，跳过第一个元素
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                // 初始化轮廓坐标
                int x1 = 1000000, y1 = 1000000, x2 = 0, y2 = 0;
                int nbChars = 0;

                for (int idC = 0; idC < charsArray.Rows; idC++)
                {
                    int xc = charsArray.At<int>(idC, 0);
                    int yc = charsArray.At<int>(idC, 1);
                    int wc = charsArray.At<int>(idC, 2);
                    int hc = charsArray.At<int>(idC, 3);
                    int areaC = charsArray.At<int>(idC, 4);

                    // 计算重叠区域
                    int xOverlap = Math.Max(0, Math.Min(x + w, xc + wc) - Math.Max(x, xc));
                    int yOverlap = Math.Max(0, Math.Min(y + h, yc + hc) - Math.Max(y, yc));

                    if (xOverlap * yOverlap >= 0.5 * hc * wc)
                    {
                        // 更新统计信息
                        x1 = Math.Min(x1, xc);
                        y1 = Math.Min(y1, yc);
                        x2 = Math.Max(x2, xc + wc);
                        y2 = Math.Max(y2, yc + hc);
                        nbChars++;
                    }
                }

                if (nbChars > 0)
                {
                    listContours.Add(new List<int> { x1, y1, x2 - x1, y2 - y1 });
                }
            }

            Mat contoursArray = listContours.Count > 0 ? Utils.ListToMat(listContours) : new Mat(0, 4, MatType.CV_32S);
            return contoursArray;
        }

        private static Tuple<double?, Mat?, Mat?> compute_char_length(Mat thresh)
        {
            // Connected components
            Mat cc_labels = new Mat();
            Mat stats = new Mat();
            Mat centroids = new Mat();
            Cv2.ConnectedComponentsWithStats(thresh, cc_labels, stats, centroids, PixelConnectivity.Connectivity8, MatType.CV_32S);
            // Remove dots
            stats = remove_dots(cc_labels, stats);

            // Remove connected components with less than 10 pixels
            // 创建布尔掩码，过滤面积大于10的连通组件
            int numComponents = stats.Rows;
            bool[] maskPixels = new bool[numComponents];
            int validCount = 0;
            for (int i = 0; i < numComponents; i++)
            {
                int area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                if (area > 10)
                {
                    maskPixels[i] = true;
                    validCount++;
                }
                else
                {
                    maskPixels[i] = false;
                }
            }

            // 创建一个新的 Mat 对象来存储过滤后的结果
            Mat filteredStats = new Mat(validCount, stats.Cols, stats.Type());

            // 复制符合条件的行到新的 Mat 对象中
            int rowIndex = 0;
            for (int i = 0; i < numComponents; i++)
            {
                if (maskPixels[i])
                {
                    for (int col = 0; col < stats.Cols; col++)
                    {
                        filteredStats.Set(rowIndex, col, stats.At<int>(i, col));
                    }
                    rowIndex++;
                }
            }

            // 计算新的列并将其添加到 filteredStats 中
            Mat completeStats = new Mat(filteredStats.Rows, filteredStats.Cols + 2, MatType.CV_32S);
            for (int i = 0; i < filteredStats.Rows; i++)
            {
                for (int j = 0; j < filteredStats.Cols; j++)
                {
                    completeStats.Set(i, j, filteredStats.At<int>(i, j));
                }
                int newCol1 = (2 * filteredStats.At<int>(i, (int)ConnectedComponentsTypes.Left) + filteredStats.At<int>(i, (int)ConnectedComponentsTypes.Width)) / 2;
                int newCol2 = (2 * filteredStats.At<int>(i, (int)ConnectedComponentsTypes.Top) + filteredStats.At<int>(i, (int)ConnectedComponentsTypes.Height)) / 2;
                completeStats.Set(i, filteredStats.Cols, newCol1);
                completeStats.Set(i, filteredStats.Cols + 1, newCol2);
            }

            stats = remove_dotted_lines(completeStats);

            if (stats.Rows == 0)
            {
                return new (null, null, null);
            }

            // Filter relevant connected components
            var t = filter_cc(stats);
            Mat relevant_stats = t.Item1;
            Mat discarded_stats = t.Item2;

            if (relevant_stats.Rows > 0)
            {
                // 提取宽度列
                int[] widths = new int[relevant_stats.Rows];
                for (int i = 0; i < relevant_stats.Rows; i++)
                {
                    widths[i] = relevant_stats.At<int>(i, (int)ConnectedComponentsTypes.Width);
                }

                // 计算宽度的众数
                int argmaxCharLength = Utils.ArgMax(Utils.BinCount(widths));
                double meanCharLength = widths.Average();

                // 计算字符长度
                double charLength = meanCharLength;
                if (1.5 * argmaxCharLength <= meanCharLength)
                {
                    charLength = meanCharLength;
                }
                else
                {
                    charLength = argmaxCharLength;
                }

                // Create thresholded image with characters
                t = create_character_thresh(thresh, relevant_stats, discarded_stats, charLength);
                var characters_thresh = t.Item1;
                var chars_array = t.Item2;

                return new(charLength, characters_thresh, chars_array);
            }

            return new (null, null, null);
        }

        static Tuple<Mat, Mat> create_character_thresh(Mat thresh, Mat stats, Mat discardedStats, double charLength)
        {
            // 创建空白字符阈值图像
            Mat characterThresh = new Mat(thresh.Size(), MatType.CV_8U, new Scalar(0));

            // 识别丢弃的连通组件中可能是字符的部分
            List<List<int>> listRelevantChars = new List<List<int>>();
            for (int idx = 0; idx < stats.Rows; idx++)
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                // 将字符添加到阈值图像中
                listRelevantChars.Add(new List<int> { x, y, w, h, area });
                characterThresh[new Rect(x, y, w, h)] = thresh[new Rect(x, y, w, h)];

                for (int idxDiscarded = 1; idxDiscarded < discardedStats.Rows; idxDiscarded++)
                {
                    int ccX = discardedStats.At<int>(idxDiscarded, 0);
                    int ccY = discardedStats.At<int>(idxDiscarded, 1);
                    int ccW = discardedStats.At<int>(idxDiscarded, 2);
                    int ccH = discardedStats.At<int>(idxDiscarded, 3);
                    int ccArea = discardedStats.At<int>(idxDiscarded, 4);

                    // 计算 y 重叠
                    int yOverlap = Math.Min(ccY + ccH, y + h) - Math.Max(ccY, y);

                    if (yOverlap < 0.5 * Math.Min(ccH, h))
                    {
                        continue;
                    }
                    else if (Math.Max(ccH, ccW) > 3 * Math.Max(h, w))
                    {
                        continue;
                    }

                    // 计算水平距离
                    int distance = Math.Min(
                        Math.Min(Math.Abs(ccX - x), Math.Abs(ccX - x - w)),
                        Math.Min(Math.Abs(ccX + ccW - x), Math.Abs(ccX + ccW - x - w)));

                    if (yOverlap > 0 && distance <= charLength)
                    {
                        // 将新字符添加到阈值图像中
                        listRelevantChars.Add(new List<int> { ccX, ccY, ccW, ccH, ccArea });
                        characterThresh[new Rect(ccX, ccY, ccW, ccH)] = thresh[new Rect(ccX, ccY, ccW, ccH)];
                    }
                }
            }

            Mat relevantCharsArray = listRelevantChars.Count > 0 ? Utils.ListToMat(listRelevantChars) : new Mat(0, 5, MatType.CV_32S);
            return Tuple.Create(characterThresh, relevantCharsArray);
        }

        static Tuple<Mat, Mat> filter_cc(Mat stats)
        {
            List<List<int>> keptCC = new List<List<int>>();
            List<List<int>> discardedCC = new List<List<int>>();

            for (int idx = 0; idx < stats.Rows; idx++)
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                // Compute aspect ratio and fill ratio
                float ar = (float)Math.Max(w, h) / Math.Min(w, h);
                float fill = (float)area / (w * h);

                if (ar <= 5 && fill > 0.08)
                {
                    keptCC.Add(new List<int> { x, y, w, h, area });
                }
                else
                {
                    discardedCC.Add(new List<int> { x, y, w, h, area });
                }
            }

            if (keptCC.Count == 0)
            {
                // Map to arrays
                Mat keptArray = keptCC.Count > 0 ? Utils.ListToMat(keptCC) : new Mat(0, 5, MatType.CV_32S);
                Mat discardedArray = discardedCC.Count > 0 ? Utils.ListToMat(discardedCC) : new Mat(0, 5, MatType.CV_32S);
                return Tuple.Create(keptArray, discardedArray);
            }

            // Map keptCC to array and compute metrics
            Mat keptStats = Utils.ListToMat(keptCC);
            double[] widths = new double[keptStats.Rows];
            double[] heights = new double[keptStats.Rows];
            for (int i = 0; i < keptStats.Rows; i++)
            {
                widths[i] = keptStats.At<int>(i, 2);
                heights[i] = keptStats.At<int>(i, 3);
            }
            double medianWidth = Utils.Median(widths);
            double medianHeight = Utils.Median(heights);

            // Compute bbox area bounds
            double upperBound = 5 * medianWidth * medianHeight;
            double lowerBound = 0.2 * medianWidth * medianHeight;

            keptCC.Clear();
            for (int idx = 0; idx < keptStats.Rows; idx++)
            {
                int x = keptStats.At<int>(idx, 0);
                int y = keptStats.At<int>(idx, 1);
                int w = keptStats.At<int>(idx, 2);
                int h = keptStats.At<int>(idx, 3);
                int area = keptStats.At<int>(idx, 4);

                // Check area
                bool boundedArea = lowerBound <= w * h && w * h <= upperBound;
                // Check dashes
                bool isDash = (float)w / h >= 2 && 0.5 * medianWidth <= w && w <= 1.5 * medianWidth;

                if (boundedArea || isDash)
                {
                    keptCC.Add(new List<int> { x, y, w, h, area });
                }
                else
                {
                    discardedCC.Add(new List<int> { x, y, w, h, area });
                }
            }

            // Map to arrays
            Mat keptArrayFinal = keptCC.Count > 0 ? Utils.ListToMat(keptCC) : new Mat(0, 5, MatType.CV_32S);
            Mat discardedArrayFinal = discardedCC.Count > 0 ? Utils.ListToMat(discardedCC) : new Mat(0, 5, MatType.CV_32S);
            return Tuple.Create(keptArrayFinal, discardedArrayFinal);
        }

        private static Mat remove_dotted_lines(Mat completeStats)
        {
            // Identify vertical lines
            int[] columnToSort = new int[completeStats.Rows];
            for (int i = 0; i < completeStats.Rows; i++)
            {
                columnToSort[i] = completeStats.At<int>(i, 6);
            }

            // 获取排序索引
            int[] sortedIndices = columnToSort
                .Select((value, index) => new { Value = value, Index = index })
                .OrderBy(x => x.Value)
                .Select(x => x.Index)
                .ToArray();

            // 根据排序索引重新排列矩阵
            Mat sortedCompleteStats = new Mat(completeStats.Rows, completeStats.Cols, completeStats.Type());
            for (int i = 0; i < sortedIndices.Length; i++)
            {
                int sortedIndex = sortedIndices[i];
                for (int j = 0; j < completeStats.Cols; j++)
                {
                    sortedCompleteStats.Set(i, j, completeStats.At<int>(sortedIndex, j));
                }
            }
            completeStats = sortedCompleteStats;

            List<List<int>> lineAreas = new List<List<int>>();
            int x1Area = 0, y1Area = 0, x2Area = 0, y2Area = 0, widthArea = 0, prevYMiddle = -10, areaCount = 0;
            for (int idx = 0; idx < completeStats.Rows; idx++)
            {
                int x = completeStats.At<int>(idx, 0);
                int y = completeStats.At<int>(idx, 1);
                int w = completeStats.At<int>(idx, 2);
                int h = completeStats.At<int>(idx, 3);
                int xMiddle = completeStats.At<int>(idx, 5);
                int yMiddle = completeStats.At<int>(idx, 6);

                if ((float)w / h < 2)
                {
                    continue;
                }

                if (yMiddle - prevYMiddle <= 2)
                {
                    // Add to previous area
                    x1Area = Math.Min(x, x1Area);
                    y1Area = Math.Min(y, y1Area);
                    x2Area = Math.Max(x + w, x2Area);
                    y2Area = Math.Max(y + h, y2Area);
                    widthArea += w;
                    areaCount += 1;
                    prevYMiddle = yMiddle;
                }
                else
                {
                    // Check if previously defined area is relevant
                    if (areaCount >= 5 && (float)widthArea / ((x2Area - x1Area) == 0 ? 1 : (x2Area - x1Area)) >= 0.66)
                    {
                        lineAreas.Add(new List<int> { x1Area, y1Area, x2Area, y2Area });
                    }
                    // Create new area
                    x1Area = x;
                    y1Area = y;
                    x2Area = x + w;
                    y2Area = y + h;
                    widthArea = w;
                    prevYMiddle = yMiddle;
                    areaCount = 1;
                }
            }

            // Check last area
            if (areaCount >= 5 && (float)widthArea / ((x2Area - x1Area) == 0 ? 1 : (x2Area - x1Area)) >= 0.66)
            {
                lineAreas.Add(new List<int> { x1Area, y1Area, x2Area, y2Area });
            }

            // Identify vertical lines
            // 提取第5列数据
            columnToSort = new int[completeStats.Rows];
            for (int i = 0; i < completeStats.Rows; i++)
            {
                columnToSort[i] = completeStats.At<int>(i, 5);
            }

            // 获取排序索引
            sortedIndices = columnToSort
                .Select((value, index) => new { Value = value, Index = index })
                .OrderBy(x => x.Value)
                .Select(x => x.Index)
                .ToArray();

            // 根据排序索引重新排列矩阵
            sortedCompleteStats = new Mat(completeStats.Rows, completeStats.Cols, completeStats.Type());
            for (int i = 0; i < sortedIndices.Length; i++)
            {
                int sortedIndex = sortedIndices[i];
                for (int j = 0; j < completeStats.Cols; j++)
                {
                    sortedCompleteStats.Set(i, j, completeStats.At<int>(sortedIndex, j));
                }
            }
            completeStats = sortedCompleteStats;

            x1Area = 0;
            y1Area = 0; 
            x2Area = 0;
            y2Area = 0;
            int heightArea = 0;
            int prevXMiddle = -10;
            areaCount = 0;
            for (int idx = 0; idx < completeStats.Rows; idx++)
            {
                int x = completeStats.At<int>(idx, 0);
                int y = completeStats.At<int>(idx, 1);
                int w = completeStats.At<int>(idx, 2);
                int h = completeStats.At<int>(idx, 3);
                int xMiddle = completeStats.At<int>(idx, 5);
                int yMiddle = completeStats.At<int>(idx, 6);

                if ((float)h / w < 2)
                {
                    continue;
                }

                if (xMiddle - prevXMiddle <= 2)
                {
                    // Add to previous area
                    x1Area = Math.Min(x, x1Area);
                    y1Area = Math.Min(y, y1Area);
                    x2Area = Math.Max(x + w, x2Area);
                    y2Area = Math.Max(y + h, y2Area);
                    heightArea += h;
                    areaCount += 1;
                    prevXMiddle = xMiddle;
                }
                else
                {
                    // Check if previously defined area is relevant
                    if (areaCount >= 5 && (float)heightArea / ((y2Area - y1Area) == 0 ? 1 : (y2Area - y1Area)) >= 0.66)
                    {
                        lineAreas.Add(new List<int> { x1Area, y1Area, x2Area, y2Area });
                    }
                    // Create new area
                    x1Area = x;
                    y1Area = y;
                    x2Area = x + w;
                    y2Area = y + h;
                    heightArea = h;
                    prevXMiddle = xMiddle;
                    areaCount = 1;
                }
            }

            // 检查最后一个区域
            if (areaCount >= 5 && (float)heightArea / ((y2Area - y1Area) == 0 ? 1 : (y2Area - y1Area)) >= 0.66)
            {
                lineAreas.Add(new List<int> { x1Area, y1Area, x2Area, y2Area });
            }

            if (lineAreas.Count == 0)
            {
                Mat result = new Mat(completeStats.Rows, 5, MatType.CV_32S);
                for (int i = 0; i < completeStats.Rows; i++)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        result.Set(i, j, completeStats.At<int>(i, j));
                    }
                }
                return result;
            }

            // 创建 lineAreas 数组
            int[,] areasArray = new int[lineAreas.Count, 4];
            for (int i = 0; i < lineAreas.Count; i++)
            {
                areasArray[i, 0] = lineAreas[i][0];
                areasArray[i, 1] = lineAreas[i][1];
                areasArray[i, 2] = lineAreas[i][2];
                areasArray[i, 3] = lineAreas[i][3];
            }

            // 检查连通组件是否位于区域内
            List<int[]> keptCC = new List<int[]>();
            for (int idx = 0; idx < completeStats.Rows; idx++)
            {
                int x = completeStats.At<int>(idx, 0);
                int y = completeStats.At<int>(idx, 1);
                int w = completeStats.At<int>(idx, 2);
                int h = completeStats.At<int>(idx, 3);
                int area = completeStats.At<int>(idx, 4);
                int xMiddle = completeStats.At<int>(idx, 5);
                int yMiddle = completeStats.At<int>(idx, 6);

                float intersectionArea = 0;
                for (int j = 0; j < areasArray.GetLength(0); j++)
                {
                    x1Area = areasArray[j, 0];
                    y1Area = areasArray[j, 1];
                    x2Area = areasArray[j, 2];
                    y2Area = areasArray[j, 3];

                    // 计算重叠区域
                    float xOverlap = Math.Max(0, Math.Min(x2Area, x + w) - Math.Max(x1Area, x));
                    float yOverlap = Math.Max(0, Math.Min(y2Area, y + h) - Math.Max(y1Area, y));
                    intersectionArea += xOverlap * yOverlap;
                }

                if (intersectionArea / (w * h) < 0.25)
                {
                    keptCC.Add(new int[] { x, y, w, h, area });
                }
            }
            // PrintMat(completeStats);

            // 返回 keptCC 或空数组
            if (keptCC.Count > 0)
            {
                Mat result = new Mat(keptCC.Count, 5, MatType.CV_32S);
                for (int i = 0; i < keptCC.Count; i++)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        result.Set(i, j, keptCC[i][j]);
                    }
                }
                return result;
            }
            else
            {
                return new Mat(0, 5, MatType.CV_32S);
            }
        }

        private static Mat remove_dots(Mat cc_labels, Mat stats)
        {
            List<int[]> cc_to_keep = new List<int[]>();

            for (int idx = 1; idx < stats.Rows; idx++)
            {
                int x = stats.At<int>(idx, 0);
                int y = stats.At<int>(idx, 1);
                int w = stats.At<int>(idx, 2);
                int h = stats.At<int>(idx, 3);
                int area = stats.At<int>(idx, 4);

                // Check number of inner pixels
                int innerPixels = 0;
                for (int row = y; row < y + h; row++)
                {
                    int prevPosition = -1;
                    for (int col = x; col < x + w; col++)
                    {
                        int value = cc_labels.At<int>(row, col);
                        if (value == idx)
                        {
                            if (prevPosition >= 0)
                            {
                                innerPixels += col - prevPosition - 1;
                            }
                            prevPosition = col;
                        }
                    }
                }

                for (int col = x; col < x + w; col++)
                {
                    int prevPosition = -1;
                    for (int row = y; row < y + h; row++)
                    {
                        int value = cc_labels.At<int>(row, col);
                        if (value == idx)
                        {
                            if (prevPosition >= 0)
                            {
                                innerPixels += row - prevPosition - 1;
                            }
                            prevPosition = row;
                        }
                    }
                }

                // Compute roundness
                double roundness = 4 * area / (Math.PI * Math.Max(h, w) * Math.Max(h, w));

                if (!(innerPixels / (2.0 * area) <= 0.1 && roundness >= 0.7))
                {
                    cc_to_keep.Add(new int[] { x, y, w, h, area });
                }
            }

            if (cc_to_keep.Count > 0)
            {
                Mat result = new Mat(cc_to_keep.Count, 5, MatType.CV_32S);
                for (int i = 0; i < cc_to_keep.Count; i++)
                {
                    result.Set(i, 0, cc_to_keep[i][0]);
                    result.Set(i, 1, cc_to_keep[i][1]);
                    result.Set(i, 2, cc_to_keep[i][2]);
                    result.Set(i, 3, cc_to_keep[i][3]);
                    result.Set(i, 4, cc_to_keep[i][4]);
                }
                return result;
            }
            else
            {
                return new Mat(0, 5, MatType.CV_32S);
            }
        }

    }
}
