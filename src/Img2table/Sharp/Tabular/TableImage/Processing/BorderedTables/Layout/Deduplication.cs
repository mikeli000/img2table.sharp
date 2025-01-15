using Img2table.Sharp.Tabular.TableImage.TableElement;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout
{
    public class Deduplication
    {
        public static List<Cell> DeduplicateCells(List<Cell> cells)
        {
            int xMax = cells.Count > 0 ? cells.Max(c => c.X2) : 0;
            int yMax = cells.Count > 0 ? cells.Max(c => c.Y2) : 0;
            byte[,] coverageArray = new byte[yMax, xMax];

            for (int y = 0; y < yMax; y++)
            {
                for (int x = 0; x < xMax; x++)
                {
                    coverageArray[y, x] = 1;
                }
            }


            List<Cell> dedupCells = new List<Cell>();
            foreach (var cell in cells.OrderBy(c => c.Area))
            {
                bool shouldAdd = false;
                for (int y = cell.Y1; y < cell.Y2; y++)
                {
                    for (int x = cell.X1; x < cell.X2; x++)
                    {
                        if (coverageArray[y, x] == 1)
                        {
                            shouldAdd = true;
                            break;
                        }
                    }
                    if (shouldAdd)
                    {
                        break;
                    }
                }

                if (shouldAdd)
                {
                    dedupCells.Add(cell);
                    for (int y = cell.Y1; y < cell.Y2; y++)
                    {
                        for (int x = cell.X1; x < cell.X2; x++)
                        {
                            coverageArray[y, x] = 0;
                        }
                    }
                }
            }

            return dedupCells;
        }
    }
}
