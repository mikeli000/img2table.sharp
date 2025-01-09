using Img2table.Sharp.Img2table.Tables.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Img2table.Sharp.Img2table.Tables.Processing.BorderlessTables.Model;

namespace Img2table.Sharp.Img2table.Tables.Processing.BorderlessTables
{
    public class Whitespaces
    {
        public static List<Whitespace> GetWhitespaces(ColumnGroup columnGroup, bool vertical = true, double minWidth = 0,
            double minHeight = 1, double pct = 0.25f, bool continuous = true)
        {
            ImageSegment segment;
            if (!vertical)
            {
                List<Cell> flippedElements = columnGroup.Elements.Select(el => new Cell(el.Y1, el.X1, el.Y2, el.X2)).ToList();
                segment = new ImageSegment(columnGroup.Y1, columnGroup.X1, columnGroup.Y2, columnGroup.X2, flippedElements);
            }
            else
            {
                segment = new ImageSegment(columnGroup.X1, columnGroup.Y1, columnGroup.X2, columnGroup.Y2, columnGroup.Elements);
            }

            int yMin = segment.Elements.Min(el => el.Y1);
            int yMax = segment.Elements.Max(el => el.Y2);

            List<int[]> baseElementsArray = segment.Elements.Select(el => new int[] { el.X1, el.Y1, el.X2, el.Y2 })
                .Concat(new List<int[]> { new int[] { segment.X1, yMin, segment.X2, yMin }, new int[] { segment.X1, yMax, segment.X2, yMax } })
                .ToList();

            double[,] elementsArray = ConvertTo2DArray(baseElementsArray);
            elementsArray = AddNewColumn(elementsArray);

            elementsArray = SortArray(elementsArray, new int[] { 5, 4, 3, 2, 1 });

            // 计算空白区域组
            var wsGroups = ComputeWhitespaces(elementsArray, minWidth, minHeight, pct * (yMax - yMin), continuous);

            // 映射到空白区域
            List<Whitespace> whitespaces = wsGroups.Select(wsGp => new Whitespace(wsGp.Select(c => new Cell(c[0], c[1], c[2], c[3])).ToList())).ToList();

            // 翻转对象坐标（水平情况）
            if (!vertical)
            {
                whitespaces = whitespaces.Select(ws => ws.Flipped()).ToList();
            }

            return whitespaces;
        }

        public static List<Whitespace> GetWhitespaces(ImageSegment segment, bool vertical = true, double min_width = 0,
            double min_height = 1, double pct = 0.25f, bool continuous = true)
        {
            if (!vertical)
            {
                List<Cell> flippedElements = segment.Elements.Select(el => new Cell(el.Y1, el.X1, el.Y2, el.X2)).ToList();
                segment = new ImageSegment(segment.Y1, segment.X1, segment.Y2, segment.X2, flippedElements);
            }

            int yMin = segment.Elements.Min(el => el.Y1);
            int yMax = segment.Elements.Max(el => el.Y2);

            List<int[]> baseElementsArray = segment.Elements.Select(el => new int[] { el.X1, el.Y1, el.X2, el.Y2 })
                .Concat(new List<int[]> { new int[] { segment.X1, yMin, segment.X2, yMin }, new int[] { segment.X1, yMax, segment.X2, yMax } })
                .ToList();

            double[,] elementsArray = ConvertTo2DArray(baseElementsArray);
            elementsArray = AddNewColumn(elementsArray);
            elementsArray = SortArray(elementsArray, new int[] { 5, 4, 3, 2, 1 });

            var wsGroups = ComputeWhitespaces(elementsArray, min_width, min_height, pct * (yMax - yMin), continuous);

            List<Whitespace> whitespaces = wsGroups.Select(wsGp => new Whitespace(wsGp.Select(c => new Cell(c[0], c[1], c[2], c[3])).ToList())).ToList();

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

        private static double[,] SortArray(double[,] array, int[] sortOrder)
        {
            int rows = array.GetLength(0);
            int cols = array.GetLength(1);

            double[][] jaggedArray = new double[rows][];
            for (int i = 0; i < rows; i++)
            {
                jaggedArray[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                {
                    jaggedArray[i][j] = array[i, j];
                }
            }

            Array.Sort(jaggedArray, (a, b) =>
            {
                foreach (int col in sortOrder)
                {
                    int colIndex = col - 1;
                    int comparison = a[colIndex].CompareTo(b[colIndex]);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }
                return 0;
            });

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    array[i, j] = jaggedArray[i][j];
                }
            }

            return array;
        }

        private static List<List<List<int>>> ComputeWhitespaces(double[,] elementsArray, double minWidth, double minHeight, double totalHeight, bool continuous = true)
        {
            HashSet<double> xVals = new HashSet<double>();
            for (int i = 0; i < elementsArray.GetLength(0); i++)
            {
                xVals.Add(elementsArray[i, 0]);
                xVals.Add(elementsArray[i, 2]);
            }

            double[] xArray = xVals.OrderBy(x => x).ToArray();

            List<List<List<int>>> finalWhitespaces = new List<List<List<int>>>();
            for (int i = 0; i < xArray.Length - 1; i++)
            {
                double xMin = xArray[i];
                double xMax = xArray[i + 1];

                if (xMax - xMin < minWidth)
                {
                    continue;
                }

                List<int[]> listWs = new List<int[]>();
                double prevY = 1e6;
                for (int j = 0; j < elementsArray.GetLength(0); j++)
                {
                    double x1 = elementsArray[j, 0];
                    double y1 = elementsArray[j, 1];
                    double x2 = elementsArray[j, 2];
                    double y2 = elementsArray[j, 3];

                    double overlap = Math.Min(xMax, x2) - Math.Max(xMin, x1);
                    if (overlap > 0)
                    {
                        if (y1 - prevY >= minHeight)
                        {
                            listWs.Add(new int[] { (int)xMin, (int)prevY, (int)xMax, (int)y1 });
                        }
                        prevY = y2;
                    }
                }

                if (continuous)
                {
                    double yMin = -1000, yMax = -1000;
                    for (int k = 0; k < listWs.Count; k++)
                    {
                        int x1Ws = listWs[k][0];
                        int y1Ws = listWs[k][1];
                        int x2Ws = listWs[k][2];
                        int y2Ws = listWs[k][3];

                        if (y1Ws == yMax)
                        {
                            yMin = Math.Min(y1Ws, yMin);
                            yMax = Math.Max(y2Ws, yMax);
                        }
                        else
                        {
                            if (yMax - yMin >= totalHeight)
                            {
                                finalWhitespaces.Add(new List<List<int>> { new List<int> { (int)xMin, (int)yMin, (int)xMax, (int)yMax } });
                            }
                            yMin = y1Ws;
                            yMax = y2Ws;
                        }
                    }

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

                        nbWs++;
                        totHeightWs += y2Ws - y1Ws;
                        minHeightWs = Math.Min(y1Ws, minHeightWs);
                        maxHeightWs = Math.Max(y2Ws, maxHeightWs);
                        wsGroup.Add(new List<int> { (int)xMin, y1Ws, (int)xMax, y2Ws });
                    }

                    if (totHeightWs >= totalHeight && totHeightWs >= 0.8 * (maxHeightWs - minHeightWs) && (nbWs == 1 || xMax - xMin >= 2 * minWidth))
                    {
                        finalWhitespaces.Add(wsGroup);
                    }
                }
            }

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
                        x2Prev = x2;
                    }
                    else
                    {
                        if (x2Prev - x1Prev >= minWidth && i > 0)
                        {
                            dedupWhitespaces.Add(new List<List<int>> { new List<int> { x1Prev, y1Prev, x2Prev, y2Prev } });
                        }
                        x1Prev = x1;
                        y1Prev = y1;
                        x2Prev = x2;
                        y2Prev = y2;
                    }
                }

                if (x2Prev - x1Prev >= minWidth)
                {
                    dedupWhitespaces.Add(new List<List<int>> { new List<int> { x1Prev, y1Prev, x2Prev, y2Prev } });
                }

                return dedupWhitespaces;
            }

            return finalWhitespaces;
        }

        public static List<Whitespace> GetRelevantVerticalWhitespaces(ImageSegment segment, double charLength, double medianLineSep, double pct = 0.25)
        {
            List<Whitespace> vWhitespaces = GetWhitespaces(segment, true, min_width: charLength, 
                min_height: Math.Min(medianLineSep, segment.ElementHeight), pct: pct, continuous: true);

            List<Whitespace> verticalDelims = IdentifyCoherentVWhitespaces(vWhitespaces);

            return DeduplicateWhitespaces(verticalDelims, segment.Elements);
        }

        private static bool AdjacentWhitespaces(Whitespace w1, Whitespace w2)
        {
            bool xCoherent = new HashSet<int> { w1.X1, w1.X2 }.Intersect(new HashSet<int> { w2.X1, w2.X2 }).Any();
            bool yCoherent = Math.Min(w1.Y2, w2.Y2) - Math.Max(w1.Y1, w2.Y1) > 0;

            return xCoherent && yCoherent;
        }

        private static List<Whitespace> IdentifyCoherentVWhitespaces(List<Whitespace> vWhitespaces)
        {
            List<int> deletedIdx = new List<int>();
            for (int i = 0; i < vWhitespaces.Count; i++)
            {
                for (int j = i + 1; j < vWhitespaces.Count; j++)
                {
                    bool adjacent = AdjacentWhitespaces(vWhitespaces[i], vWhitespaces[j]);

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

        private static List<Whitespace> DeduplicateWhitespaces(List<Whitespace> ws, List<Cell> elements)
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

                            var commonArea = new Cell(
                                Math.Min(ws1.X2, ws2.X2),
                                Math.Max(ws1.Y1, ws2.Y1),
                                Math.Max(ws1.X1, ws2.X1),
                                Math.Min(ws1.Y2, ws2.Y2)
                            );

                            matchingElements.AddRange(elements.Where(el =>
                                Math.Min(el.X2, commonArea.X2) - Math.Max(el.X1, commonArea.X1) > 0 &&
                                Math.Min(el.Y2, commonArea.Y2) - Math.Max(el.Y1, commonArea.Y1) > 0));
                        }
                    }

                    if (matchingElements.Count == 0)
                    {
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

            mergedWs = mergedWs.Where(mWs => !filteredWs.Any(w => Math.Min(w.X2, mWs.X2) - Math.Max(w.X1, mWs.X1) > 0)).ToList();

            if (mergedWs.Count > 1)
            {
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
