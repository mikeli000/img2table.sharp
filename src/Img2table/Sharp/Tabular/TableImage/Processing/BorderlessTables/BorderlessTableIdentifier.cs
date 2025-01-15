
using OpenCvSharp;
using System.Data;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.Layout;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables
{
    public class BorderlessTableIdentifier
    {
        public static List<Table> IdentifyBorderlessTables(Mat thresh, List<Line> lines, double char_length,
            double medianLineSep, List<Cell> contours, List<Table> existingTables)
        {
            var tableSegments = ImageLayout.SegmentImage(thresh, lines, char_length, medianLineSep, existingTables);

            var tables = new List<Table>();
            foreach (var tableSegment in tableSegments)
            {
                var columnGroup = TableColumnIdentifier.IdentifyColumns(tableSegment: tableSegment,
                                                charLength: char_length,
                                                medianLineSep: medianLineSep);

                if (columnGroup != null)
                {
                    var rowDelimiters = TableRowIdentifier.IdentifyDelimiterGroupRows(columnGroup, contours);

                    if (rowDelimiters != null && rowDelimiters.Count() > 0)
                    {
                        var borderlessTable = TableIdentifier.IdentifyTable(columnGroup, rowDelimiters, contours, medianLineSep, char_length);

                        if (borderlessTable != null)
                        {
                            var corrected_table = CoherentTable(borderlessTable, tableSegment.Elements);
                            if (corrected_table != null)
                            {
                                tables.Add(corrected_table);
                            }
                        }
                    }
                }
            }

            return DeduplicateTables(tables, existingTables);
        }

        private static Table CoherentTable(Table tb, List<Cell> elements)
        {
            DataTable dfRows = CreateDataFrame(tb);
            DataTable dfElements = CreateElementsDataFrame(elements);

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

        private static List<Table> DeduplicateTables(List<Table> identifiedTables, List<Table> existingTables)
        {
            identifiedTables = identifiedTables.OrderByDescending(tb => tb.Area).ToList();

            List<Table> finalTables = new List<Table>();
            foreach (var table in identifiedTables)
            {
                if (!existingTables.Concat(finalTables).Any(tb =>
                            Common.IsContainedCell(table.Cell, tb.Cell, 0.1)
                             || Common.IsContainedCell(tb.Cell, table.Cell, 0.1)))
                {
                    finalTables.Add(table);
                }
            }

            return finalTables;
        }
    }
}
