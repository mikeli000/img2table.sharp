using Img2table.Sharp.Tabular.TableElement;
using static Img2table.Sharp.Tabular.Processing.BorderlessTables.TableImageStructure;

namespace Img2table.Sharp.Tabular.Processing.BorderlessTables
{
    public class TableRowIdentifier
    {
        private static List<Cell> IdentifyRowDelimiters(ColumnGroup columnGroup)
        {
            List<Whitespace> h_ws = WhitespaceIdentifier.GetWhitespaces(columnGroup, false, 0.66);

            if (h_ws.First().Y1 > columnGroup.Y1)
            {
                var up_ws = new Whitespace(new List<Cell>
                {
                    new Cell(h_ws.Min(ws => ws.X1), columnGroup.Y1, h_ws.Max(ws => ws.X2), columnGroup.Elements.Min(el => el.Y1))
                });
                h_ws.Insert(0, up_ws);
            }

            if (h_ws.Last().Y2 < columnGroup.Y2)
            {
                var down_ws = new Whitespace(new List<Cell>
                {
                    new Cell(h_ws.Min(ws => ws.X1), columnGroup.Y2, h_ws.Max(ws => ws.X2), columnGroup.Elements.Max(el => el.Y2))
                });
                h_ws.Add(down_ws);
            }

            if (h_ws.Count > 2)
            {
                var full_ws_h = h_ws.Skip(1).Take(h_ws.Count - 2).Where(ws => ws.Width == h_ws.Max(w => w.Width)).Select(ws => ws.Height).OrderBy(h => h).ToList();
                double min_height = full_ws_h.Count >= 3 ? 0.5 * full_ws_h[(full_ws_h.Count + 1) / 2 - 1] : 1;
                h_ws = new List<Whitespace> { h_ws.First() }.Concat(h_ws.Skip(1).Take(h_ws.Count - 2).Where(ws => ws.Height >= min_height)).Concat(new List<Whitespace> { h_ws.Last() }).ToList();
            }

            List<int> deleted_idx = new List<int>();
            for (int i = 0; i < h_ws.Count; i++)
            {
                for (int j = i + 1; j < h_ws.Count; j++)
                {
                    bool adjacent = new HashSet<int> { h_ws[i].Y1, h_ws[i].Y2 }.Intersect(new HashSet<int> { h_ws[j].Y1, h_ws[j].Y2 }).Any();

                    if (adjacent)
                    {
                        if (h_ws[i].Width > h_ws[j].Width)
                        {
                            deleted_idx.Add(j);
                        }
                        else if (h_ws[i].Width < h_ws[j].Width)
                        {
                            deleted_idx.Add(i);
                        }
                    }
                }
            }

            h_ws = h_ws.Where((ws, idx) => !deleted_idx.Contains(idx)).ToList();

            List<Cell> final_delims = new List<Cell>();
            foreach (var ws in h_ws)
            {
                if (ws.Y1 == columnGroup.Y1 || ws.Y2 == columnGroup.Y2)
                {
                    continue;
                }

                final_delims.Add(new Cell(ws.X1, (ws.Y1 + ws.Y2) / 2, ws.X2, (ws.Y1 + ws.Y2) / 2));
            }

            int x1_els = columnGroup.Elements.Min(el => el.X1);
            int x2_els = columnGroup.Elements.Max(el => el.X2);
            int y1_els = columnGroup.Elements.Min(el => el.Y1);
            int y2_els = columnGroup.Elements.Max(el => el.Y2);
            final_delims.Add(new Cell(x1_els, y1_els, x2_els, y1_els));
            final_delims.Add(new Cell(x1_els, y2_els, x2_els, y2_els));

            return final_delims.OrderBy(d => d.Y1).ToList();
        }

        private static List<Cell> FilterCoherentRowDelimiters(List<Cell> rowDelimiters, ColumnGroup columnGroup)
        {
            int max_width = rowDelimiters.Max(d => d.Width);

            List<int> delimiters_to_delete = new List<int>();
            for (int idx = 0; idx < rowDelimiters.Count; idx++)
            {
                var delim = rowDelimiters[idx];
                if (delim.Width >= 0.95 * max_width)
                {
                    continue;
                }

                var upper_delim = rowDelimiters[idx - 1];
                var upper_area = new Cell(Math.Max(delim.X1, upper_delim.X1), upper_delim.Y2, Math.Min(delim.X2, upper_delim.X2), delim.Y1);
                var upper_columns = columnGroup.Columns
                    .Where(col => Math.Min(upper_area.Y2, col.Y2) - Math.Max(upper_area.Y1, col.Y1) >= 0.8 * upper_area.Height && upper_area.X1 <= col.X1 && col.X1 <= upper_area.X2)
                    .OrderBy(c => c.X1)
                    .ToList();

                var upper_contained_elements = upper_columns.Any()
                    ? columnGroup.Elements.Where(el => el.Y1 >= upper_area.Y1 && el.Y2 <= upper_area.Y2 && el.X1 >= upper_columns.First().X2 && el.X2 <= upper_columns.Last().X1).ToList()
                    : new List<Cell>();

                var bottom_delim = rowDelimiters[idx + 1];
                var bottom_area = new Cell(Math.Max(delim.X1, bottom_delim.X1), delim.Y2, Math.Min(delim.X2, bottom_delim.X2), bottom_delim.Y1);
                var bottom_columns = columnGroup.Columns
                    .Where(col => Math.Min(bottom_area.Y2, col.Y2) - Math.Max(bottom_area.Y1, col.Y1) >= 0.8 * bottom_area.Height && bottom_area.X1 <= col.X1 && col.X1 <= bottom_area.X2)
                    .OrderBy(c => c.X1)
                    .ToList();

                var bottom_contained_elements = bottom_columns.Any()
                    ? columnGroup.Elements.Where(el => el.Y1 >= bottom_area.Y1 && el.Y2 <= bottom_area.Y2 && el.X1 >= bottom_columns.First().X2 && el.X2 <= bottom_columns.Last().X1).ToList()
                    : new List<Cell>();

                if (!upper_contained_elements.Any() || !bottom_contained_elements.Any())
                {
                    delimiters_to_delete.Add(idx);
                }
            }

            return rowDelimiters.Where((d, idx) => !delimiters_to_delete.Contains(idx)).ToList();
        }

        private static List<Cell> CorrectDelimiterWidth(List<Cell> rowDelimiters, List<Cell> contours)
        {
            int x_min = rowDelimiters.Min(d => d.X1);
            int x_max = rowDelimiters.Max(d => d.X2);

            for (int idx = 0; idx < rowDelimiters.Count; idx++)
            {
                var delim = rowDelimiters[idx];
                if (delim.Width == x_max - x_min)
                {
                    continue;
                }

                var left_contours = contours.Where(c => c.Y1 + c.Height / 6 < delim.Y1 && delim.Y1 < c.Y2 - c.Height / 6 && Math.Min(c.X2, delim.X1) - Math.Max(c.X1, x_min) > 0).ToList();
                int delim_x_min = Math.Max(left_contours.Any() ? left_contours.Max(c => c.X2) : x_min, x_min);

                var right_contours = contours.Where(c => c.Y1 + c.Height / 6 < delim.Y1 && delim.Y1 < c.Y2 - c.Height / 6 && Math.Min(c.X2, x_max) - Math.Max(c.X1, delim.X2) > 0).ToList();
                int delim_x_max = Math.Min(right_contours.Any() ? right_contours.Min(c => c.X1) : x_max, x_max);

                delim.X1 = delim_x_min;
                delim.X2 = delim_x_max;
            }

            return rowDelimiters;
        }

        public static List<Cell> IdentifyDelimiterGroupRows(ColumnGroup columnGroup, List<Cell> contours)
        {
            List<Cell> row_delimiters = IdentifyRowDelimiters(columnGroup);

            if (row_delimiters.Any())
            {
                List<Cell> coherent_delimiters = FilterCoherentRowDelimiters(row_delimiters, columnGroup);
                List<Cell> corrected_delimiters = CorrectDelimiterWidth(coherent_delimiters, contours);

                return corrected_delimiters.Count >= 3 ? corrected_delimiters : new List<Cell>();
            }
            return new List<Cell>();
        }
    }
}
