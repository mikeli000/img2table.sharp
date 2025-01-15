using Img2table.Sharp.Tabular.TableImage.TableElement;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout
{
    public class TableCreation
    {
        public static List<Cell> NormalizeTableCells(List<Cell> clusterCells)
        {
            int width = clusterCells.Max(c => c.X2) - clusterCells.Min(c => c.X1);
            int height = clusterCells.Max(c => c.Y2) - clusterCells.Min(c => c.Y1);

            List<int> hValues = clusterCells.SelectMany(cell => new[] { cell.X1, cell.X2 }).Distinct().OrderBy(x => x).ToList();
            List<int> hDelims = GroupCloseValues(hValues, Math.Min(width * 0.02, 10));

            List<int> vValues = clusterCells.SelectMany(cell => new[] { cell.Y1, cell.Y2 }).Distinct().OrderBy(y => y).ToList();
            List<int> vDelims = GroupCloseValues(vValues, Math.Min(height * 0.02, 10));

            List<Cell> normalizedCells = new List<Cell>();
            foreach (var cell in clusterCells)
            {
                int x1 = hDelims.OrderBy(d => Math.Abs(d - cell.X1)).First();
                int x2 = hDelims.OrderBy(d => Math.Abs(d - cell.X2)).First();
                int y1 = vDelims.OrderBy(d => Math.Abs(d - cell.Y1)).First();
                int y2 = vDelims.OrderBy(d => Math.Abs(d - cell.Y2)).First();

                Cell normalizedCell = new Cell(x1, y1, x2, y2);
                if (normalizedCell.Area > 0)
                {
                    normalizedCells.Add(normalizedCell);
                }
            }

            return normalizedCells;
        }

        private static List<int> GroupCloseValues(List<int> values, double threshold)
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

        private static Table RemoveUnwantedElements(Table table, List<Cell> elements)
        {
            if (elements.Count == 0 || table.NbRows * table.NbColumns == 0)
            {
                return new Table(new List<Row>());
            }

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

            var dfCellsElements = from cell in dfCells.AsEnumerable()
                                  from element in dfElements.AsEnumerable()
                                  let x_overlap = Math.Max(0, Math.Min(cell.Field<int>("x2"), element.Field<int>("x2_el")) - Math.Max(cell.Field<int>("x1"), element.Field<int>("x1_el")))
                                  let y_overlap = Math.Max(0, Math.Min(cell.Field<int>("y2"), element.Field<int>("y2_el")) - Math.Max(cell.Field<int>("y1"), element.Field<int>("y1_el")))
                                  let contains = x_overlap * y_overlap / element.Field<double>("area_el") >= 0.6
                                  select new { id_row = cell.Field<int>("id_row"), id_col = cell.Field<int>("id_col"), contains };

            var emptyRows = dfCellsElements.GroupBy(c => c.id_row)
                                           .Where(g => !g.Any(c => c.contains))
                                           .Select(g => g.Key)
                                           .ToList();

            var emptyCols = dfCellsElements.GroupBy(c => c.id_col)
                                           .Where(g => !g.Any(c => c.contains))
                                           .Select(g => g.Key)
                                           .ToList();

            table.RemoveRows(emptyRows);
            table.RemoveColumns(emptyCols);

            return table;
        }

        public static Table ClusterToTable(List<Cell> clusterCells, List<Cell> elements, bool borderless = false)
        {
            List<int> vDelims = clusterCells.SelectMany(cell => new[] { cell.Y1, cell.Y2 }).Distinct().OrderBy(y => y).ToList();

            List<int> hDelims = clusterCells.SelectMany(cell => new[] { cell.X1, cell.X2 }).Distinct().OrderBy(x => x).ToList();

            List<Row> listRows = new List<Row>();
            for (int i = 0; i < vDelims.Count - 1; i++)
            {
                int yTop = vDelims[i];
                int yBottom = vDelims[i + 1];

                List<Cell> matchingCells = clusterCells.Where(c => Math.Min(c.Y2, yBottom) - Math.Max(c.Y1, yTop) >= 0.9 * (yBottom - yTop)).ToList();
                List<Cell> listCells = new List<Cell>();

                for (int j = 0; j < hDelims.Count - 1; j++)
                {
                    int xLeft = hDelims[j];
                    int xRight = hDelims[j + 1];

                    Cell defaultCell = new Cell(xLeft, yTop, xRight, yBottom);
                    List<Cell> containingCells = matchingCells.Where(c => IsContainedCell(defaultCell, c, 0.9)).OrderBy(c => c.Area).ToList();

                    if (containingCells.Count > 0)
                    {
                        listCells.Add(containingCells.First());
                    }
                    else
                    {
                        if (matchingCells.Count > 0)
                        {
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

            Table table = new Table(listRows, borderless);

            Table processedTable = RemoveUnwantedElements(table, elements);

            return processedTable;
        }

        private static bool IsContainedCell(Cell innerCell, Cell outerCell, double percentage)
        {
            int innerArea = innerCell.Area;
            int overlapX = Math.Max(0, Math.Min(innerCell.X2, outerCell.X2) - Math.Max(innerCell.X1, outerCell.X1));
            int overlapY = Math.Max(0, Math.Min(innerCell.Y2, outerCell.Y2) - Math.Max(innerCell.Y1, outerCell.Y1));
            int overlapArea = overlapX * overlapY;

            return overlapArea >= percentage * innerArea;
        }
    }
}
