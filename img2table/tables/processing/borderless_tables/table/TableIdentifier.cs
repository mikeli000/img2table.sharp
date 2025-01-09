using img2table.sharp.img2table.tables.objects;
using img2table.sharp.img2table.tables.processing.bordered_tables.cells;
using img2table.sharp.img2table.tables.processing.bordered_tables.tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.objects.Objects;
using static img2table.sharp.img2table.tables.processing.borderless_tables.Model;

namespace img2table.sharp.img2table.tables.processing.borderless_tables.table
{
    public class TableIdentifier
    {
        public static Table identify_table(ColumnGroup columns, List<Cell> row_delimiters, List<Cell> contours, double median_line_sep, double char_length)
        {
            // Create table from rows and columns delimiters
            Table table = get_table(columns, row_delimiters, contours);

            if (table != null)
            {
                if (Coherency.check_table_coherency(table, median_line_sep, char_length))
                {
                    return table;
                }
            }

            return null;
        }

        static Table get_table(ColumnGroup columns, List<Cell> rowDelimiters, List<Cell> contours)
        {
            // Convert delimiters to lines
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

            // Identify cells
            List<Cell> cells = Cells.get_cells(hLines, vLines);


            // Create table object
            Table table = TableCreation.cluster_to_table(cells, contours, true);

            return table != null && table.NbColumns >= 3 && table.NbRows >= 2 ? table : null;
        }
    }
}
