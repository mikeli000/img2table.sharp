using img2table.sharp.img2table.tables.objects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.processing.bordered_tables.tables
{
    public class TableCreation
    {
        public static List<Cell> normalize_table_cells(List<Cell> clusterCells)
        {
            // 计算表格形状
            int width = clusterCells.Max(c => c.X2) - clusterCells.Min(c => c.X1);
            int height = clusterCells.Max(c => c.Y2) - clusterCells.Min(c => c.Y1);

            // 获取现有的水平值列表
            List<int> hValues = clusterCells.SelectMany(cell => new[] { cell.X1, cell.X2 }).Distinct().OrderBy(x => x).ToList();
            // 通过将接近的值分组来计算分隔符
            List<int> hDelims = GroupCloseValues(hValues, Math.Min(width * 0.02, 10));

            // 获取现有的垂直值列表
            List<int> vValues = clusterCells.SelectMany(cell => new[] { cell.Y1, cell.Y2 }).Distinct().OrderBy(y => y).ToList();
            // 通过将接近的值分组来计算分隔符
            List<int> vDelims = GroupCloseValues(vValues, Math.Min(height * 0.02, 10));

            // 规范化所有单元格
            List<Cell> normalizedCells = new List<Cell>();
            foreach (var cell in clusterCells)
            {
                int x1 = hDelims.OrderBy(d => Math.Abs(d - cell.X1)).First();
                int x2 = hDelims.OrderBy(d => Math.Abs(d - cell.X2)).First();
                int y1 = vDelims.OrderBy(d => Math.Abs(d - cell.Y1)).First();
                int y2 = vDelims.OrderBy(d => Math.Abs(d - cell.Y2)).First();

                Cell normalizedCell = new Cell(x1, y1, x2, y2);
                // 检查单元格是否为空
                if (normalizedCell.Area > 0)
                {
                    normalizedCells.Add(normalizedCell);
                }
            }

            return normalizedCells;
        }

        static List<int> GroupCloseValues(List<int> values, double threshold)
        {
            List<int> delims = new List<int>();
            List<int> group = new List<int> { values[0] };

            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] - values[i - 1] < threshold)
                {
                    group.Add(values[i]);
                }
                else
                {
                    delims.Add((int)Math.Round(group.Average()));
                    group = new List<int> { values[i] };
                }
            }

            delims.Add((int)Math.Round(group.Average()));
            return delims;
        }

        static Table remove_unwanted_elements(Table table, List<Cell> elements)
        {
            if (elements.Count == 0 || table.NbRows * table.NbColumns == 0)
            {
                return new Table(new List<Row>());
            }

            // Identify elements corresponding to each cell
            DataTable dfElements = new DataTable();
            dfElements.Columns.Add("x1_el", typeof(int));
            dfElements.Columns.Add("y1_el", typeof(int));
            dfElements.Columns.Add("x2_el", typeof(int));
            dfElements.Columns.Add("y2_el", typeof(int));
            dfElements.Columns.Add("area_el", typeof(double));

            foreach (var el in elements)
            {
                DataRow row = dfElements.NewRow();
                row["x1_el"] = el.X1;
                row["y1_el"] = el.Y1;
                row["x2_el"] = el.X2;
                row["y2_el"] = el.Y2;
                row["area_el"] = el.Area;
                dfElements.Rows.Add(row);
            }

            DataTable dfCells = new DataTable();
            dfCells.Columns.Add("id_row", typeof(int));
            dfCells.Columns.Add("id_col", typeof(int));
            dfCells.Columns.Add("x1", typeof(int));
            dfCells.Columns.Add("y1", typeof(int));
            dfCells.Columns.Add("x2", typeof(int));
            dfCells.Columns.Add("y2", typeof(int));

            for (int id_row = 0; id_row < table.Items.Count; id_row++)
            {
                for (int id_col = 0; id_col < table.Items[id_row].Items.Count; id_col++)
                {
                    var c = table.Items[id_row].Items[id_col];
                    DataRow row = dfCells.NewRow();
                    row["id_row"] = id_row;
                    row["id_col"] = id_col;
                    row["x1"] = c.X1;
                    row["y1"] = c.Y1;
                    row["x2"] = c.X2;
                    row["y2"] = c.Y2;
                    dfCells.Rows.Add(row);
                }
            }

            // Calculate overlaps and identify empty rows and columns
            var dfCellsElements = from cell in dfCells.AsEnumerable()
                                  from element in dfElements.AsEnumerable()
                                  let x_overlap = Math.Max(0, Math.Min(cell.Field<int>("x2"), element.Field<int>("x2_el")) - Math.Max(cell.Field<int>("x1"), element.Field<int>("x1_el")))
                                  let y_overlap = Math.Max(0, Math.Min(cell.Field<int>("y2"), element.Field<int>("y2_el")) - Math.Max(cell.Field<int>("y1"), element.Field<int>("y1_el")))
                                  let contains = (x_overlap * y_overlap) / element.Field<double>("area_el") >= 0.6
                                  select new { id_row = cell.Field<int>("id_row"), id_col = cell.Field<int>("id_col"), contains };

            var emptyRows = dfCellsElements.GroupBy(c => c.id_row)
                                           .Where(g => !g.Any(c => c.contains))
                                           .Select(g => g.Key)
                                           .ToList();

            var emptyCols = dfCellsElements.GroupBy(c => c.id_col)
                                           .Where(g => !g.Any(c => c.contains))
                                           .Select(g => g.Key)
                                           .ToList();

            // Remove empty rows and columns
            table.RemoveRows(emptyRows);
            table.RemoveColumns(emptyCols);

            return table;
        }

        public static Table cluster_to_table(List<Cell> clusterCells, List<Cell> elements, bool borderless = false)
        {
            // 获取垂直分隔符列表
            List<int> vDelims = clusterCells.SelectMany(cell => new[] { cell.Y1, cell.Y2 }).Distinct().OrderBy(y => y).ToList();

            // 获取水平分隔符列表
            List<int> hDelims = clusterCells.SelectMany(cell => new[] { cell.X1, cell.X2 }).Distinct().OrderBy(x => x).ToList();

            // 创建行和单元格
            List<Row> listRows = new List<Row>();
            for (int i = 0; i < vDelims.Count - 1; i++)
            {
                int yTop = vDelims[i];
                int yBottom = vDelims[i + 1];

                // 获取匹配的单元格
                List<Cell> matchingCells = clusterCells.Where(c => Math.Min(c.Y2, yBottom) - Math.Max(c.Y1, yTop) >= 0.9 * (yBottom - yTop)).ToList();
                List<Cell> listCells = new List<Cell>();

                for (int j = 0; j < hDelims.Count - 1; j++)
                {
                    int xLeft = hDelims[j];
                    int xRight = hDelims[j + 1];

                    // 创建默认单元格
                    Cell defaultCell = new Cell(xLeft, yTop, xRight, yBottom);

                    // 检查包含默认单元格的单元格
                    List<Cell> containingCells = matchingCells.Where(c => IsContainedCell(defaultCell, c, 0.9)).OrderBy(c => c.Area).ToList();

                    // 添加包含默认单元格的单元格
                    if (containingCells.Count > 0)
                    {
                        listCells.Add(containingCells.First());
                    }
                    else
                    {
                        if (matchingCells.Count > 0)
                        {
                            // 获取最接近匹配单元格的x值
                            int xValue = matchingCells.SelectMany(cell => new[] { cell.X1, cell.X2 }).OrderBy(x => Math.Min(Math.Abs(x - xLeft), Math.Abs(x - xRight))).First();
                            listCells.Add(new Cell(xValue, yTop, xValue, yBottom));
                        }
                        else
                        {
                            listCells.Add(defaultCell);
                        }
                    }
                }

                listRows.Add(new Row(listCells));
            }

            // 创建表格
            Table table = new Table(listRows, borderless);

            // 根据元素移除表格中的空行和空列
            Table processedTable = remove_unwanted_elements(table, elements);

            return processedTable;
        }

        static bool IsContainedCell(Cell innerCell, Cell outerCell, double percentage)
        {
            int innerArea = innerCell.Area;
            int overlapX = Math.Max(0, Math.Min(innerCell.X2, outerCell.X2) - Math.Max(innerCell.X1, outerCell.X1));
            int overlapY = Math.Max(0, Math.Min(innerCell.Y2, outerCell.Y2) - Math.Max(innerCell.Y1, outerCell.Y1));
            int overlapArea = overlapX * overlapY;

            return overlapArea >= percentage * innerArea;
        }
    }
}
