using img2table.sharp.img2table.tables.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.processing.borderless_tables.Model;

namespace img2table.sharp.img2table.tables.processing.borderless_tables
{
    public class Whitespaces
    {
        public static List<Whitespace> get_whitespaces(ColumnGroup column_group, bool vertical = true, double min_width = 0,
            double min_height = 1, double pct = 0.25f, bool continuous = true)
        {
            // 翻转对象坐标（水平情况）
            ImageSegment segment;
            if (!vertical)
            {
                List<Cell> flippedElements = column_group.Elements.Select(el => new Cell(el.Y1, el.X1, el.Y2, el.X2)).ToList();
                segment = new ImageSegment(column_group.Y1, column_group.X1, column_group.Y2, column_group.X2, flippedElements);
            }
            else
            {
                segment = new ImageSegment(column_group.X1, column_group.Y1, column_group.X2, column_group.Y2, column_group.Elements);
            }


            // 获取段中元素的最小/最大高度
            int yMin = segment.Elements.Min(el => el.Y1);
            int yMax = segment.Elements.Max(el => el.Y2);

            // 创建包含元素的数组
            List<int[]> baseElementsArray = segment.Elements.Select(el => new int[] { el.X1, el.Y1, el.X2, el.Y2 })
                .Concat(new List<int[]> { new int[] { segment.X1, yMin, segment.X2, yMin }, new int[] { segment.X1, yMax, segment.X2, yMax } })
                .ToList();

            // 添加新列
            double[,] elementsArray = ConvertTo2DArray(baseElementsArray);
            elementsArray = AddNewColumn(elementsArray);

            // 根据新列排序
            //elementsArray = SortByNewColumn(elementsArray);
            elementsArray = SortArray(elementsArray, new int[] { 5, 4, 3, 2, 1 });

            // 计算空白区域组
            var wsGroups = compute_whitespaces(elementsArray, min_width, min_height, pct * (yMax - yMin), continuous);

            // 映射到空白区域
            List<Whitespace> whitespaces = wsGroups.Select(wsGp => new Whitespace(wsGp.Select(c => new Cell(c[0], c[1], c[2], c[3])).ToList())).ToList();

            // 翻转对象坐标（水平情况）
            if (!vertical)
            {
                whitespaces = whitespaces.Select(ws => ws.Flipped()).ToList();
            }

            return whitespaces;
        }

        public static List<Whitespace> get_whitespaces(ImageSegment segment, bool vertical = true, double min_width = 0,
            double min_height = 1, double pct = 0.25f, bool continuous = true)
        {
            // 翻转对象坐标（水平情况）
            if (!vertical)
            {
                List<Cell> flippedElements = segment.Elements.Select(el => new Cell(el.Y1, el.X1, el.Y2, el.X2)).ToList();
                segment = new ImageSegment(segment.Y1, segment.X1, segment.Y2, segment.X2, flippedElements);
            }

            // 获取段中元素的最小/最大高度
            int yMin = segment.Elements.Min(el => el.Y1);
            int yMax = segment.Elements.Max(el => el.Y2);

            // 创建包含元素的数组
            List<int[]> baseElementsArray = segment.Elements.Select(el => new int[] { el.X1, el.Y1, el.X2, el.Y2 })
                .Concat(new List<int[]> { new int[] { segment.X1, yMin, segment.X2, yMin }, new int[] { segment.X1, yMax, segment.X2, yMax } })
                .ToList();

            // 添加新列
            double[,] elementsArray = ConvertTo2DArray(baseElementsArray);
            elementsArray = AddNewColumn(elementsArray);

            // 根据新列排序
            //elementsArray = SortByNewColumn(elementsArray);
            elementsArray = SortArray(elementsArray, new int[] { 5, 4, 3, 2, 1 });

            // 计算空白区域组
            var wsGroups = compute_whitespaces(elementsArray, min_width, min_height, pct * (yMax - yMin), continuous);

            // 映射到空白区域
            List<Whitespace> whitespaces = wsGroups.Select(wsGp => new Whitespace(wsGp.Select(c => new Cell(c[0], c[1], c[2], c[3])).ToList())).ToList();

            // 翻转对象坐标（水平情况）
            if (!vertical)
            {
                whitespaces = whitespaces.Select(ws => ws.Flipped()).ToList();
            }

            return whitespaces;
        }

        private static double[,] ConvertTo2DArray(List<int[]> baseElementsArray)
        {
            int rows = baseElementsArray.Count;
            int cols = baseElementsArray[0].Length;

            double[,] elementsArray = new double[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    elementsArray[i, j] = baseElementsArray[i][j];
                }
            }

            return elementsArray;
        }

        private static double[,] AddNewColumn(double[,] elementsArray)
        {
            int rows = elementsArray.GetLength(0);
            int cols = elementsArray.GetLength(1);

            double[,] newArray = new double[rows, cols + 1];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    newArray[i, j] = elementsArray[i, j];
                }
                newArray[i, cols] = (elementsArray[i, 1] + elementsArray[i, 3]) / 2;
            }

            return newArray;
        }

        public static double[,] SortArray(double[,] array, int[] sortOrder)
        {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);

            // 将二维数组转换为一维数组
            double[][] jaggedArray = new double[rows][];
            for (int i = 0; i < rows; i++)
            {
                jaggedArray[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                {
                    jaggedArray[i][j] = array[i, j];
                }
            }

            // 排序一维数组
            Array.Sort(jaggedArray, (a, b) =>
            {
                foreach (int col in sortOrder)
                {
                    int colIndex = col - 1; // 列号从1开始，数组索引从0开始
                    int comparison = a[colIndex].CompareTo(b[colIndex]);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }
                return 0;
            });

            // 将一维数组转换回二维数组
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    array[i, j] = jaggedArray[i][j];
                }
            }

            return array;
        }

        private static double[,] SortByNewColumn(double[,] elementsArray)
        {
            int rows = elementsArray.GetLength(0);
            int cols = elementsArray.GetLength(1);

            var sortedArray = new double[rows, cols];
            var list = new List<double[]>();

            for (int i = 0; i < rows; i++)
            {
                var row = new double[cols];
                for (int j = 0; j < cols; j++)
                {
                    row[j] = elementsArray[i, j];
                }
                list.Add(row);
            }

            list = list.OrderBy(row => row[cols - 1]).ToList();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    sortedArray[i, j] = list[i][j];
                }
            }

            return sortedArray;

        }
        static List<List<List<int>>> compute_whitespaces(double[,] elementsArray, double minWidth, double minHeight, double totalHeight, bool continuous = true)
        {
            // 获取元素中的x值
            HashSet<double> xVals = new HashSet<double>();
            for (int i = 0; i < elementsArray.GetLength(0); i++)
            {
                xVals.Add(elementsArray[i, 0]);
                xVals.Add(elementsArray[i, 2]);
            }

            // 创建x值数组
            double[] xArray = xVals.OrderBy(x => x).ToArray();

            // 检查范围
            List<List<List<int>>> finalWhitespaces = new List<List<List<int>>>();
            for (int i = 0; i < xArray.Length - 1; i++)
            {
                double xMin = xArray[i];
                double xMax = xArray[i + 1];

                // 检查数组元素
                if (xMax - xMin < minWidth)
                {
                    continue;
                }

                // 识别空白区域位置
                List<int[]> listWs = new List<int[]>();
                double prevY = 1e6;
                for (int j = 0; j < elementsArray.GetLength(0); j++)
                {
                    double x1 = elementsArray[j, 0];
                    double y1 = elementsArray[j, 1];
                    double x2 = elementsArray[j, 2];
                    double y2 = elementsArray[j, 3];

                    // 检查是否重叠段
                    double overlap = Math.Min(xMax, x2) - Math.Max(xMin, x1);
                    if (overlap > 0)
                    {
                        // 如果空白区域足够高，则添加它
                        if (y1 - prevY >= minHeight)
                        {
                            listWs.Add(new int[] { (int)xMin, (int)prevY, (int)xMax, (int)y1 });
                        }
                        prevY = y2;
                    }
                }

                // 创建空白区域
                if (continuous)
                {
                    double yMin = -1000, yMax = -1000;
                    for (int k = 0; k < listWs.Count; k++)
                    {
                        int x1Ws = listWs[k][0];
                        int y1Ws = listWs[k][1];
                        int x2Ws = listWs[k][2];
                        int y2Ws = listWs[k][3];

                        // 检查与之前的空白区域
                        if (y1Ws == yMax)
                        {
                            yMin = Math.Min(y1Ws, yMin);
                            yMax = Math.Max(y2Ws, yMax);
                        }
                        else
                        {
                            // 检查当前空白区域
                            if (yMax - yMin >= totalHeight)
                            {
                                finalWhitespaces.Add(new List<List<int>> { new List<int> { (int)xMin, (int)yMin, (int)xMax, (int)yMax } });
                            }
                            yMin = y1Ws;
                            yMax = y2Ws;
                        }
                    }

                    // 检查最后一个空白区域
                    if (yMax - yMin >= totalHeight)
                    {
                        finalWhitespaces.Add(new List<List<int>> { new List<int> { (int)xMin, (int)yMin, (int)xMax, (int)yMax } });
                    }
                }
                else
                {
                    int nbWs = 0;
                    double totHeightWs = 0;
                    double minHeightWs = 1e6;
                    double maxHeightWs = 0;
                    List<List<int>> wsGroup = new List<List<int>>();
                    for (int k = 0; k < listWs.Count; k++)
                    {
                        int x1Ws = listWs[k][0];
                        int y1Ws = listWs[k][1];
                        int x2Ws = listWs[k][2];
                        int y2Ws = listWs[k][3];

                        // 更新指标
                        nbWs++;
                        totHeightWs += y2Ws - y1Ws;
                        minHeightWs = Math.Min(y1Ws, minHeightWs);
                        maxHeightWs = Math.Max(y2Ws, maxHeightWs);
                        wsGroup.Add(new List<int> { (int)xMin, y1Ws, (int)xMax, y2Ws });
                    }

                    // 检查组的相关性
                    if (totHeightWs >= totalHeight && totHeightWs >= 0.8 * (maxHeightWs - minHeightWs) && (nbWs == 1 || xMax - xMin >= 2 * minWidth))
                    {
                        finalWhitespaces.Add(wsGroup);
                    }
                }
            }

            // 在连续的情况下去重空白区域
            if (continuous)
            {
                List<List<List<int>>> dedupWhitespaces = new List<List<List<int>>>();

                int x1Prev = 0, y1Prev = 0, x2Prev = 0, y2Prev = 0;
                for (int i = 0; i < finalWhitespaces.Count; i++)
                {
                    int x1 = finalWhitespaces[i][0][0];
                    int y1 = finalWhitespaces[i][0][1];
                    int x2 = finalWhitespaces[i][0][2];
                    int y2 = finalWhitespaces[i][0][3];

                    if (x1 == x2Prev && y1 == y1Prev && y2 == y2Prev)
                    {
                        // 与之前的空白区域合并
                        x2Prev = x2;
                    }
                    else
                    {
                        // 添加空白区域
                        if (x2Prev - x1Prev >= minWidth && i > 0)
                        {
                            dedupWhitespaces.Add(new List<List<int>> { new List<int> { x1Prev, y1Prev, x2Prev, y2Prev } });
                        }
                        // 重置指标
                        x1Prev = x1;
                        y1Prev = y1;
                        x2Prev = x2;
                        y2Prev = y2;
                    }
                }
                // 添加最后一个空白区域
                if (x2Prev - x1Prev >= minWidth)
                {
                    dedupWhitespaces.Add(new List<List<int>> { new List<int> { x1Prev, y1Prev, x2Prev, y2Prev } });
                }

                return dedupWhitespaces;
            }

            return finalWhitespaces;
        }

        public static List<Whitespace> get_relevant_vertical_whitespaces(ImageSegment segment, double charLength, double medianLineSep, double pct = 0.25)
        {
            // 识别垂直空白区域
            List<Whitespace> vWhitespaces = get_whitespaces(segment, true, min_width: charLength, 
                min_height: Math.Min(medianLineSep, segment.ElementHeight), pct: pct, continuous: true);

            // 识别可以作为列分隔符的相关垂直空白区域
            List<Whitespace> verticalDelims = identify_coherent_v_whitespaces(vWhitespaces);

            // 去重空白区域
            return deduplicate_whitespaces(verticalDelims, segment.Elements);
        }

        static bool adjacent_whitespaces(Whitespace w1, Whitespace w2)
        {
            bool xCoherent = new HashSet<int> { w1.X1, w1.X2 }.Intersect(new HashSet<int> { w2.X1, w2.X2 }).Any();
            bool yCoherent = Math.Min(w1.Y2, w2.Y2) - Math.Max(w1.Y1, w2.Y1) > 0;

            return xCoherent && yCoherent;
        }

        static List<Whitespace> identify_coherent_v_whitespaces(List<Whitespace> vWhitespaces)
        {
            List<int> deletedIdx = new List<int>();
            for (int i = 0; i < vWhitespaces.Count; i++)
            {
                for (int j = i + 1; j < vWhitespaces.Count; j++)
                {
                    // 检查两个空白区域是否相邻
                    bool adjacent = adjacent_whitespaces(vWhitespaces[i], vWhitespaces[j]);

                    if (adjacent)
                    {
                        if (vWhitespaces[i].Height > vWhitespaces[j].Height)
                        {
                            deletedIdx.Add(j);
                        }
                        else if (vWhitespaces[i].Height < vWhitespaces[j].Height)
                        {
                            deletedIdx.Add(i);
                        }
                    }
                }
            }

            return vWhitespaces.Where((ws, idx) => !deletedIdx.Contains(idx)).ToList();
        }

        static List<Whitespace> deduplicate_whitespaces(List<Whitespace> ws, List<Cell> elements)
        {
            if (ws.Count <= 1)
            {
                return ws;
            }

            List<int> deletedIdx = new List<int>();
            List<Whitespace> mergedWs = new List<Whitespace>();

            for (int i = 0; i < ws.Count; i++)
            {
                for (int j = i + 1; j < ws.Count; j++)
                {
                    List<Cell> matchingElements = new List<Cell>();
                    foreach (var ws1 in ws[i].Cells)
                    {
                        foreach (var ws2 in ws[j].Cells)
                        {
                            if (Math.Min(ws1.Y2, ws2.Y2) - Math.Max(ws1.Y1, ws2.Y1) <= 0)
                            {
                                continue;
                            }

                            // 获取公共区域
                            var commonArea = new Cell(
                                Math.Min(ws1.X2, ws2.X2),
                                Math.Max(ws1.Y1, ws2.Y1),
                                Math.Max(ws1.X1, ws2.X1),
                                Math.Min(ws1.Y2, ws2.Y2)
                            );

                            // 识别匹配的元素
                            matchingElements.AddRange(elements.Where(el =>
                                Math.Min(el.X2, commonArea.X2) - Math.Max(el.X1, commonArea.X1) > 0 &&
                                Math.Min(el.Y2, commonArea.Y2) - Math.Max(el.Y1, commonArea.Y1) > 0));
                        }
                    }

                    if (matchingElements.Count == 0)
                    {
                        // 将较小的元素添加到删除的空白区域
                        if (ws[i].Height > ws[j].Height)
                        {
                            deletedIdx.Add(j);
                        }
                        else if (ws[i].Height < ws[j].Height)
                        {
                            deletedIdx.Add(i);
                        }
                        else
                        {
                            // 创建合并的空白区域
                            var newCells = ws[i].Cells.Concat(ws[j].Cells)
                                .Select(c => new Cell(
                                    Math.Min(ws[i].X1, ws[j].X1),
                                    c.Y1,
                                    Math.Max(ws[i].X2, ws[j].X2),
                                    c.Y2
                                )).Distinct().ToList();
                            mergedWs.Add(new Whitespace(newCells));
                            deletedIdx.AddRange(new[] { i, j });
                        }
                    }
                }
            }

            var filteredWs = ws.Where((w, idx) => !deletedIdx.Contains(idx)).ToList();

            // 删除与过滤后的空白区域不一致的合并空白区域
            mergedWs = mergedWs.Where(mWs => !filteredWs.Any(w => Math.Min(w.X2, mWs.X2) - Math.Max(w.X1, mWs.X1) > 0)).ToList();

            if (mergedWs.Count > 1)
            {
                // 去重重叠的合并空白区域
                var seq = mergedWs.OrderByDescending(w => w.Area).GetEnumerator();
                var filteredMergedWs = new List<Whitespace> { seq.Current };
                while (seq.MoveNext())
                {
                    if (!filteredWs.Any(fWs => fWs.Equals(seq.Current)))
                    {
                        filteredMergedWs.Add(seq.Current);
                    }
                }
                mergedWs = filteredMergedWs;
            }

            return filteredWs.Concat(mergedWs).ToList();
        }

    }
}
