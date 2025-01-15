using Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using static Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.TableImageStructure;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.Layout
{
    public class TableIdentifier
    {
        public static Table IdentifyTable(ColumnGroup columns, List<Cell> rowDelimiters, List<Cell> contours, double medianLineSep, double charLength)
        {
            Table table = GetTable(columns, rowDelimiters, contours);

            if (table != null)
            {
                if (Coherency.check_table_coherency(table, medianLineSep, charLength))
                {
                    return table;
                }
            }

            return null;
        }

        private static Table GetTable(ColumnGroup columns, List<Cell> rowDelimiters, List<Cell> contours)
        {
            List<Line> vLines = new List<Line>();
            foreach (var col in columns.Columns)
            {
                var seq = col.Whitespaces.SelectMany(v_ws => v_ws.Ws.Cells).OrderBy(c => c.Y1 + c.Y2).ToList();
                var lineGroups = new List<List<Cell>> { new List<Cell> { seq.First() } };
                foreach (var c in seq.Skip(1))
                {
                    if (c.Y1 > lineGroups.Last().Last().Y2)
                    {
                        lineGroups.Add(new List<Cell>());
                    }
                    lineGroups.Last().Add(c);
                }

                vLines.AddRange(lineGroups.Select(gp => new Line(
                    (gp.First().X1 + gp.First().X2) / 2,
                    gp.First().Y1,
                    (gp.First().X1 + gp.First().X2) / 2,
                    gp.Last().Y2
                )));
            }

            List<Line> hLines = rowDelimiters.Select(d => new Line(d.X1, d.Y1, d.X2, d.Y2)).ToList();
            List<Cell> cells = CellDetector.DetectCells(hLines, vLines);

            Table table = TableCreation.ClusterToTable(cells, contours, true);
            return table != null && table.NbColumns >= 3 && table.NbRows >= 2 ? table : null;
        }
    }
}
