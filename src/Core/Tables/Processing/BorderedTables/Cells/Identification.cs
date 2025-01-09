using Img2table.Sharp.Core.Tables.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Img2table.Sharp.Core.Tables.Objects.Objects;

namespace Img2table.Sharp.Core.Tables.Processing.BorderedTables.Cells
{
    public class Identification
    {
        public static List<Cell> GetCellsDataframe(List<Line> horizontalLines, List<Line> verticalLines)
        {
            if (horizontalLines.Count == 0 || verticalLines.Count == 0)
            {
                return new List<Cell>();
            }

            var hLinesArray = horizontalLines.Select(line => new[] { line.X1, line.Y1, line.X2, line.Y2 }).ToArray();
            var vLinesArray = verticalLines.Select(line => new[] { line.X1, line.Y1, line.X2, line.Y2 }).ToArray();

            var cellsArray = IdentifyCells(hLinesArray, vLinesArray);

            return cellsArray.Select(c => new Cell(c[0], c[1], c[2], c[3])).ToList();
        }

        private static List<int[]> IdentifyCells(int[][] hLinesArray, int[][] vLinesArray)
        {
            List<int[]> potentialCells = new List<int[]>();

            for (int i = 0; i < hLinesArray.Length; i++)
            {
                int x1i = hLinesArray[i][0];
                int y1i = hLinesArray[i][1];
                int x2i = hLinesArray[i][2];
                int y2i = hLinesArray[i][3];

                for (int j = 0; j < hLinesArray.Length; j++)
                {
                    int x1j = hLinesArray[j][0];
                    int y1j = hLinesArray[j][1];
                    int x2j = hLinesArray[j][2];
                    int y2j = hLinesArray[j][3];

                    if (y1i >= y1j)
                    {
                        continue;
                    }

                    bool lCorresponds = -0.02 <= (x1i - x1j) / ((x2i - x1i) == 0 ? 1 : (x2i - x1i)) && (x1i - x1j) / ((x2i - x1i) == 0 ? 1 : (x2i - x1i)) <= 0.02;
                    bool rCorresponds = -0.02 <= (x2i - x2j) / ((x2i - x1i) == 0 ? 1 : (x2i - x1i)) && (x2i - x2j) / ((x2i - x1i) == 0 ? 1 : (x2i - x1i)) <= 0.02;
                    bool lContained = (x1i <= x1j && x1j <= x2i) || (x1j <= x1i && x1i <= x2j);
                    bool rContained = (x1i <= x2j && x2j <= x2i) || (x1j <= x2i && x2i <= x2j);

                    if ((lCorresponds || lContained) && (rCorresponds || rContained))
                    {
                        potentialCells.Add(new int[] { Math.Max(x1i, x1j), Math.Min(x2i, x2j), y1i, y2j });
                    }
                }
            }

            if (potentialCells.Count == 0)
            {
                return new List<int[]>();
            }

            potentialCells = potentialCells.OrderBy(cell => cell[0]).ThenBy(cell => cell[1]).ThenBy(cell => cell[2]).ThenBy(cell => cell[3]).ToList();
            List<int[]> dedupUpper = new List<int[]>();
            int prevX1 = 0, prevX2 = 0, prevY1 = 0;
            foreach (var cell in potentialCells)
            {
                int x1 = cell[0];
                int x2 = cell[1];
                int y1 = cell[2];
                int y2 = cell[3];

                if (!(x1 == prevX1 && x2 == prevX2 && y1 == prevY1))
                {
                    dedupUpper.Add(new int[] { x1, x2, y2, -y1 });
                }
                prevX1 = x1;
                prevX2 = x2;
                prevY1 = y1;
            }

            dedupUpper = dedupUpper.OrderBy(cell => cell[0]).ThenBy(cell => cell[1]).ThenBy(cell => cell[2]).ThenBy(cell => cell[3]).ToList();
            List<int[]> dedupLower = new List<int[]>();
            int prevX1Lower = 0, prevX2Lower = 0, prevY2 = 0;
            foreach (var cell in dedupUpper)
            {
                int x1 = cell[0];
                int x2 = cell[1];
                int y2 = cell[2];
                int y1 = -cell[3];

                if (!(x1 == prevX1Lower && x2 == prevX2Lower && y2 == prevY2))
                {
                    dedupLower.Add(new int[] { x1, x2, y1, y2 });
                }
                prevX1Lower = x1;
                prevX2Lower = x2;
                prevY2 = y2;
            }

            List<int[]> cells = new List<int[]>();
            foreach (var cell in dedupLower)
            {
                int x1 = cell[0];
                int x2 = cell[1];
                int y1 = cell[2];
                int y2 = cell[3];

                double margin = Math.Max(5d, (x2 - x1) * 0.025);

                List<int> delimiters = new List<int>();
                foreach (var vLine in vLinesArray)
                {
                    int x1v = vLine[0];
                    int y1v = vLine[1];
                    int x2v = vLine[2];
                    int y2v = vLine[3];

                    if (x1 - margin <= x1v && x1v <= x2 + margin)
                    {
                        double overlap = Math.Min(y2, y2v) - Math.Max(y1, y1v);
                        double tolerance = Math.Max(5d, Math.Min(10d, 0.1 * (y2 - y1)));
                        if (y2 - y1 - overlap <= tolerance)
                        {
                            delimiters.Add(x1v);
                        }
                    }
                }

                if (delimiters.Count >= 2)
                {
                    delimiters.Sort();
                    for (int j = 0; j < delimiters.Count - 1; j++)
                    {
                        cells.Add(new int[] { delimiters[j], y1, delimiters[j + 1], y2 });
                    }
                }
            }

            return cells;
        }
    }
}
