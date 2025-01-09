using Img2table.Sharp.Img2table.Tables.Objects;
using static Img2table.Sharp.Img2table.Tables.Objects.Objects;

namespace Img2table.Sharp.Img2table.Tables.Processing.BorderedTables.Tables
{
    public class SemiBordered
    {
        public static List<Cell> AddSemiBorderedCells(List<Cell> cluster, List<Line> lines, double charLength)
        {
            if (cluster.Count == 0)
            {
                return cluster;
            }

            var (hLinesCl, vLinesCl) = GetLinesInCluster(cluster, lines);
            var (leftVal, rightVal, topVal, bottomVal) = IdentifyTableDimensions(cluster, hLinesCl, vLinesCl, charLength);
            List<Cell> newCells = IdentifyPotentialNewCells(cluster, hLinesCl, vLinesCl, leftVal, rightVal, topVal, bottomVal);
            List<Cell> updatedCluster = UpdateClusterCells(cluster, newCells);

            return updatedCluster;
        }

        private static List<Cell> UpdateClusterCells(List<Cell> cluster, List<Cell> newCells)
        {
            if (newCells.Count == 0)
            {
                return cluster;
            }

            var dfCluster = cluster.Select(c => new { c.X1, c.Y1, c.X2, c.Y2, Area = c.Area }).ToList();
            var dfCells = newCells.Select((c, idx) => new { idx, c.X1, c.Y1, c.X2, c.Y2, Area = c.Area }).ToList();

            var dfCellsIndep = dfCells
                .SelectMany(cell => dfCluster, (cell, clusterCell) => new
                {
                    cell.idx,
                    cell.X1,
                    cell.Y1,
                    cell.X2,
                    cell.Y2,
                    cell.Area,
                    XOverlap = Math.Max(0, Math.Min(cell.X2, clusterCell.X2) - Math.Max(cell.X1, clusterCell.X1)),
                    YOverlap = Math.Max(0, Math.Min(cell.Y2, clusterCell.Y2) - Math.Max(cell.Y1, clusterCell.Y1)),
                    ClusterArea = clusterCell.Area
                })
                .Select(x => new
                {
                    x.idx,
                    x.X1,
                    x.Y1,
                    x.X2,
                    x.Y2,
                    x.Area,
                    AreaOverlap = x.XOverlap * x.YOverlap,
                    PctOverlap = (x.XOverlap * x.YOverlap) / Math.Min(x.Area, x.ClusterArea)
                })
                .GroupBy(x => x.idx)
                .Select(g => new
                {
                    g.First().idx,
                    g.First().X1,
                    g.First().Y1,
                    g.First().X2,
                    g.First().Y2,
                    g.First().Area,
                    MaxOverlap = g.Max(x => x.PctOverlap)
                })
                .Where(x => x.MaxOverlap < 0.5)
                .ToList();

            if (dfCellsIndep.Count == 0)
            {
                return cluster;
            }

            var dfDups = dfCellsIndep
                .SelectMany(cell => dfCellsIndep, (cell, otherCell) => new
                {
                    cell.idx,
                    cell.X1,
                    cell.Y1,
                    cell.X2,
                    cell.Y2,
                    cell.Area,
                    OtherIdx = otherCell.idx,
                    OtherX1 = otherCell.X1,
                    OtherY1 = otherCell.Y1,
                    OtherX2 = otherCell.X2,
                    OtherY2 = otherCell.Y2,
                    OtherArea = otherCell.Area
                })
                .Where(x => x.Area <= x.OtherArea && x.idx != x.OtherIdx)
                .Select(x => new
                {
                    x.idx,
                    XOverlap = Math.Max(0, Math.Min(x.X2, x.OtherX2) - Math.Max(x.X1, x.OtherX1)),
                    YOverlap = Math.Max(0, Math.Min(x.Y2, x.OtherY2) - Math.Max(x.Y1, x.OtherY1)),
                    x.Area,
                    OtherArea = x.OtherArea
                })
                .Select(x => new
                {
                    x.idx,
                    AreaOverlap = x.XOverlap * x.YOverlap,
                    PctOverlap = (x.XOverlap * x.YOverlap) / Math.Min(x.Area, x.OtherArea)
                })
                .GroupBy(x => x.idx)
                .Select(g => new
                {
                    g.First().idx,
                    MaxOverlap = g.Max(x => x.PctOverlap)
                })
                .Where(x => x.MaxOverlap >= 0.5)
                .Select(x => x.idx)
                .ToList();

            var dfFinalCells = dfCellsIndep
                .Where(cell => !dfDups.Contains(cell.idx))
                .Select(cell => new Cell(cell.X1, cell.Y1, cell.X2, cell.Y2))
                .ToList();

            if (dfFinalCells.Count > 0)
            {
                return TableCreation.NormalizeTableCells(cluster.Concat(dfFinalCells).ToList());
            }
            else
            {
                return cluster;
            }
        }

        private static List<Cell> IdentifyPotentialNewCells(List<Cell> cluster, List<Line> hLinesCl, List<Line> vLinesCl, int leftVal, int rightVal, int topVal, int bottomVal)
        {
            List<int> xCluster = new HashSet<int>(cluster.SelectMany(c => new[] { c.X1, c.X2 }).Union(new[] { leftVal, rightVal })).OrderBy(x => x).ToList();
            List<int> yCluster = new HashSet<int>(cluster.SelectMany(c => new[] { c.Y1, c.Y2 }).Union(new[] { topVal, bottomVal })).OrderBy(y => y).ToList();

            List<Cell> newCells = new List<Cell>();

            int x1 = xCluster[0], x2 = xCluster[1];
            int y1, y2;
            List<int> yVals = new HashSet<int>(new[] { topVal, bottomVal }.Union(hLinesCl.Where(l => Math.Min(l.X2, x2) - Math.Max(l.X1, x1) >= 0.9 * (x2 - x1)).Select(l => l.Y1))).OrderBy(y => y).ToList();
            for (int i = 0; i < yVals.Count - 1; i++)
            {
                y1 = yVals[i];
                y2 = yVals[i + 1];
                Cell newCell = new Cell(x1, y1, x2, y2);
                if (newCell.Area > 0)
                {
                    newCells.Add(newCell);
                }
            }

            x1 = xCluster[xCluster.Count - 2];
            x2 = xCluster[xCluster.Count - 1];
            yVals = new HashSet<int>(new[] { topVal, bottomVal }.Union(hLinesCl.Where(l => Math.Min(l.X2, x2) - Math.Max(l.X1, x1) >= 0.9 * (x2 - x1)).Select(l => l.Y1))).OrderBy(y => y).ToList();
            for (int i = 0; i < yVals.Count - 1; i++)
            {
                y1 = yVals[i];
                y2 = yVals[i + 1];
                Cell newCell = new Cell(x1, y1, x2, y2);
                if (newCell.Area > 0)
                {
                    newCells.Add(newCell);
                }
            }

            y1 = yCluster[0];
            y2 = yCluster[1];
            List<int> xVals = new HashSet<int>(new[] { leftVal, rightVal }.Union(vLinesCl.Where(l => Math.Min(l.Y2, y2) - Math.Max(l.Y1, y1) >= 0.9 * (y2 - y1)).Select(l => l.X1))).OrderBy(x => x).ToList();
            for (int i = 0; i < xVals.Count - 1; i++)
            {
                x1 = xVals[i];
                x2 = xVals[i + 1];
                Cell newCell = new Cell(x1, y1, x2, y2);
                if (newCell.Area > 0)
                {
                    newCells.Add(newCell);
                }
            }

            y1 = yCluster[yCluster.Count - 2];
            y2 = yCluster[yCluster.Count - 1];
            xVals = new HashSet<int>(new[] { leftVal, rightVal }.Union(vLinesCl.Where(l => Math.Min(l.Y2, y2) - Math.Max(l.Y1, y1) >= 0.9 * (y2 - y1)).Select(l => l.X1))).OrderBy(x => x).ToList();
            for (int i = 0; i < xVals.Count - 1; i++)
            {
                x1 = xVals[i];
                x2 = xVals[i + 1];
                Cell newCell = new Cell(x1, y1, x2, y2);
                if (newCell.Area > 0)
                {
                    newCells.Add(newCell);
                }
            }

            return newCells.Distinct().ToList();
        }

        private static (List<Line> hLinesCl, List<Line> vLinesCl) GetLinesInCluster(List<Cell> cluster, List<Line> lines)
        {
            int xMin = cluster.Min(c => c.X1);
            int xMax = cluster.Max(c => c.X2);
            int yMin = cluster.Min(c => c.Y1);
            int yMax = cluster.Max(c => c.Y2);

            var yValuesCl = new HashSet<int>(cluster.SelectMany(c => new[] { c.Y1, c.Y2 }));
            List<Line> hLinesCl = lines.Where(line => line.Horizontal
                && yValuesCl.Any(y => Math.Abs(line.Y1 - y) <= 0.05 * (yMax - yMin))).ToList();

            var xValuesCl = new HashSet<int>(cluster.SelectMany(c => new[] { c.X1, c.X2 }));
            List<Line> vLinesCl = lines.Where(line => line.Vertical
                && xValuesCl.Any(x => Math.Abs(line.X1 - x) <= 0.05 * (xMax - xMin))).ToList();

            return (hLinesCl, vLinesCl);
        }

        private static (int leftVal, int rightVal, int topVal, int bottomVal) IdentifyTableDimensions(List<Cell> cluster, List<Line> hLinesCl, List<Line> vLinesCl, double charLength)
        {
            int leftVal, rightVal, topVal, bottomVal;

            if (hLinesCl.Count > 0)
            {
                int left = hLinesCl.Min(line => line.X1);
                int right = hLinesCl.Max(line => line.X2);

                var leftEndLines = hLinesCl.Where(line => line.X1 - left <= 0.05 * (right - left)).ToList();
                if (new HashSet<Line> { hLinesCl[0], hLinesCl[^1] }.Except(leftEndLines).Count() == 0)
                {
                    leftVal = cluster.Min(c => c.X1) - left <= 2 * charLength ? cluster.Min(c => c.X1) : left;
                }
                else
                {
                    leftVal = cluster.Min(c => c.X1);
                }

                var rightEndLines = hLinesCl.Where(line => right - line.X2 <= 0.05 * (right - left)).ToList();
                if (new HashSet<Line> { hLinesCl[0], hLinesCl[^1] }.Except(rightEndLines).Count() == 0)
                {
                    rightVal = right - cluster.Max(c => c.X2) <= 2 * charLength ? cluster.Max(c => c.X2) : right;
                }
                else
                {
                    rightVal = cluster.Max(c => c.X2);
                }
            }
            else
            {
                leftVal = cluster.Min(c => c.X1);
                rightVal = cluster.Max(c => c.X2);
            }

            if (vLinesCl.Count > 0)
            {
                int top = vLinesCl.Min(line => line.Y1);
                int bottom = vLinesCl.Max(line => line.Y2);

                var topEndLines = vLinesCl.Where(line => line.Y1 - top <= 0.05 * (bottom - top)).ToList();
                if (new HashSet<Line> { vLinesCl[0], vLinesCl[^1] }.Except(topEndLines).Count() == 0)
                {
                    topVal = cluster.Min(c => c.Y1) - top <= 2 * charLength ? cluster.Min(c => c.Y1) : top;
                }
                else
                {
                    topVal = cluster.Min(c => c.Y1);
                }

                var bottomEndLines = vLinesCl.Where(line => bottom - line.Y2 <= 0.05 * (bottom - top)).ToList();
                if (new HashSet<Line> { vLinesCl[0], vLinesCl[^1] }.Except(bottomEndLines).Count() == 0)
                {
                    bottomVal = bottom - cluster.Max(c => c.Y2) <= 2 * charLength ? cluster.Max(c => c.Y2) : bottom;
                }
                else
                {
                    bottomVal = cluster.Max(c => c.Y2);
                }
            }
            else
            {
                topVal = cluster.Min(c => c.Y1);
                bottomVal = cluster.Max(c => c.Y2);
            }

            return (leftVal, rightVal, topVal, bottomVal);
        }
    }
}
