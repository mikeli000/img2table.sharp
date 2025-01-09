using img2table.sharp.img2table.tables.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.processing.bordered_tables.cells
{
    public class Deduplication
    {
        public static List<Cell> deduplicate_cells(List<Cell> cells)
        {
            // 创建单元格覆盖数组
            int xMax = cells.Count > 0 ? cells.Max(c => c.X2) : 0;
            int yMax = cells.Count > 0 ? cells.Max(c => c.Y2) : 0;
            byte[,] coverageArray = new byte[yMax, xMax];
            // 初始化覆盖数组为1
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

                // 如果单元格至少有25%的区域未被覆盖，则添加它
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
