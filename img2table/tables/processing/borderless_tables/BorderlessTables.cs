using img2table.sharp.img2table.tables.objects;
using img2table.sharp.img2table.tables.processing.borderless_tables.layout;
using img2table.sharp.img2table.tables.processing.borderless_tables.table;
using OpenCvSharp;
using System.Data;
using static img2table.sharp.img2table.tables.objects.Objects;

namespace img2table.sharp.img2table.tables.processing.borderless_tables
{
    public class BorderlessTables
    {
        public static List<Table> identify_borderless_tables(Mat thresh, List<Line> lines, double char_length, 
            double median_line_sep, List<Cell> contours, List<Table> existing_tables)
        {
            // Segment image and identify parts that can correspond to tables
            var table_segments = Layout.segment_image(thresh, lines, char_length, median_line_sep, existing_tables);

            var tables = new List<Table>();
            foreach (var table_segment in table_segments)
            {
                // Identify column groups in segment
                var column_group = Columns.identify_columns(tableSegment: table_segment,
                                                charLength: char_length,
                                                medianLineSep: median_line_sep);    

                if (column_group != null)
                {
                    // Identify potential table rows
                    var row_delimiters = Rows.identify_delimiter_group_rows(column_group, contours);

                    if (row_delimiters != null && row_delimiters.Count() > 0)
                    {
                        // Create table from column group and rows
                        var borderless_table = TableIdentifier.identify_table(column_group, row_delimiters, contours, median_line_sep, char_length);

                        if (borderless_table != null)
                        {
                            var corrected_table = coherent_table(borderless_table, table_segment.Elements);
                            if (corrected_table != null)
                            {
                                tables.Add(corrected_table);
                            }
                        }
                    }
                }
            }

            return deduplicate_tables(tables, existing_tables);
        }

        public static Table coherent_table(Table tb, List<Cell> elements)
        {
            DataTable dfRows = CreateDataFrame(tb);
            
            // Dataframe of elements
            DataTable dfElements = CreateElementsDataFrame(elements);

            // Get elements in each cells and identify coherent rows
            var uniqueRows = dfRows.AsEnumerable()
                .GroupBy(row => new
                {
                    row_id = row.Field<int>("row_id"),
                    x1 = row.Field<int>("x1"),
                    y1 = row.Field<int>("y1"),
                    x2 = row.Field<int>("x2"),
                    y2 = row.Field<int>("y2")
                })
                .Select(g => g.First()).CopyToDataTable();

            // Calculate nb_cells for each row_id
            var rowsWithNbCells = uniqueRows.AsEnumerable()
                .GroupBy(row => row.Field<int>("row_id"))
                .Select(g => new
                {
                    row_id = g.Key,
                    nb_cells = g.Count(),
                    cells = g.AsEnumerable()
                })
                .Where(r => r.nb_cells >= 3)
                .ToList();

            // Cross join with elements
            var crossJoin = from row in rowsWithNbCells
                            from element in dfElements.AsEnumerable()
                            from cell in row.cells
                            select new
                            {
                                row.row_id,
                                x1 = cell.Field<int>("x1"),
                                y1 = cell.Field<int>("y1"),
                                x2 = cell.Field<int>("x2"),
                                y2 = cell.Field<int>("y2"),
                                x1_right = element.Field<int>("x1"),
                                y1_right = element.Field<int>("y1"),
                                x2_right = element.Field<int>("x2"),
                                y2_right = element.Field<int>("y2")
                            };

            var overlaps = crossJoin.Select(joined => new
                {
                    joined.row_id,
                    x_overlap = Math.Min(joined.x2, joined.x2_right) - Math.Max(joined.x1, joined.x1_right),
                    y_overlap = Math.Min(joined.y2, joined.y2_right) - Math.Max(joined.y1, joined.y1_right),
                    area = (joined.x2_right - joined.x1_right) * (joined.y2_right - joined.y1_right)
                }).Where(joined => joined.x_overlap > 0 && joined.y_overlap > 0)
                .Select(joined => new
                {
                    joined.row_id,
                    joined.x_overlap,
                    joined.y_overlap,
                    area_overlap = joined.x_overlap * joined.y_overlap,
                    joined.area
                });

            var row_id_list = overlaps.Where(joined => (double)joined.area_overlap / joined.area >= 0.5)
                .GroupBy(joined => joined.row_id)
                .Select(g => new
                {
                    row_id = g.Key,
                    col = g.Count()
                })
                .Where(g => g.col > 1);
            var row_range = new Dictionary<string, int>
            {
                { "min_row", row_id_list.Min(x => x.row_id) },
                { "max_row", row_id_list.Max(x => x.row_id) }
            };

            if (row_range.Count > 0)
            {
                // Get new rows
                int offset = row_range["min_row"];
                int count = row_range["max_row"] - offset + 1;
                var newRows = tb.Items.GetRange(offset, count);
                if (newRows.Count >= 2)
                {
                    return new Table(newRows, true);
                }
            }

            return null;
        }

        private static DataTable CreateDataFrame(Table tb)
        {
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("row_id", typeof(int));
            dataTable.Columns.Add("x1", typeof(int));
            dataTable.Columns.Add("y1", typeof(int));
            dataTable.Columns.Add("x2", typeof(int));
            dataTable.Columns.Add("y2", typeof(int));

            for (int rowId = 0; rowId < tb.Items.Count; rowId++)
            {
                Row row = tb.Items[rowId];
                foreach (Cell c in row.Items)
                {
                    DataRow dataRow = dataTable.NewRow();
                    dataRow["row_id"] = rowId;
                    dataRow["x1"] = c.X1;
                    dataRow["y1"] = c.Y1;
                    dataRow["x2"] = c.X2;
                    dataRow["y2"] = c.Y2;
                    dataTable.Rows.Add(dataRow);
                }
            }

            return dataTable;
        }

        private static DataTable CreateElementsDataFrame(List<Cell> elements)
        {
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("x1", typeof(int));
            dataTable.Columns.Add("y1", typeof(int));
            dataTable.Columns.Add("x2", typeof(int));
            dataTable.Columns.Add("y2", typeof(int));

            foreach (Cell c in elements)
            {
                DataRow dataRow = dataTable.NewRow();
                dataRow["x1"] = c.X1;
                dataRow["y1"] = c.Y1;
                dataRow["x2"] = c.X2;
                dataRow["y2"] = c.Y2;
                dataTable.Rows.Add(dataRow);
            }

            return dataTable;
        }

        private static List<Table> deduplicate_tables(List<Table> identifiedTables, List<Table> existingTables)
        {
            // Sort tables by area
            identifiedTables = identifiedTables.OrderByDescending(tb => tb.Area).ToList();

            // For each table check if it does not overlap with an existing table
            List<Table> finalTables = new List<Table>();
            foreach (var table in identifiedTables)
            {
                if (!existingTables.Concat(finalTables).Any(tb =>
                            (Common.is_contained_cell(table.Cell, tb.Cell, 0.1)
                             || Common.is_contained_cell(tb.Cell, table.Cell, 0.1))))
                {
                    finalTables.Add(table);
                }
            }

            return finalTables;
        }
    }
}
