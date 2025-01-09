using Img2table.Sharp.Img2table.Tables.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Img2table.Sharp.Img2table.Tables.Processing.BorderlessTables.Model;

namespace Img2table.Sharp.Img2table.Tables.Processing.BorderlessTables
{
    public class Columns
    {
        public static ColumnGroup IdentifyColumns(TableSegment tableSegment, double charLength, double medianLineSep)
        {
            var columns = GetColumnsDelimiters(tableSegment, charLength);

            if (columns.Count > 0)
            {
                int x1Del = columns.Min(d => d.X1);
                int x2Del = columns.Max(d => d.X2);
                int y1Del = columns.Min(d => d.Y1);
                int y2Del = columns.Max(d => d.Y2);
                List<Cell> elements = tableSegment.Elements
                    .Where(el => el.X1 >= x1Del && el.X2 <= x2Del && el.Y1 >= y1Del && el.Y2 <= y2Del)
                    .ToList();
                ColumnGroup columnGroup = new ColumnGroup(columns, charLength, elements);

                return columnGroup.Columns.Count >= 4 && columnGroup.Elements.Count > 0 ? columnGroup : null;
            }

            return null;
        }

        private static List<Column> GetColumnsDelimiters(TableSegment tableSegment, double charLength)
        {
            var tableAreas = tableSegment.TableAreas.OrderBy(x => x.Position).ToList();

            List<Column> columns = new List<Column>();
            for (int idArea = 0; idArea < tableAreas.Count; idArea++)
            {
                var tbArea = tableAreas[idArea];
                List<Column> newColumns = new List<Column>();
                var whitespaces = tbArea.Whitespaces.Select(ws => new VerticalWS(ws, position: idArea, top: ws.Y1 == tbArea.Y1, bottom: ws.Y2 == tbArea.Y2)).ToList();

                foreach (var col in columns)
                {
                    var matchingWs = whitespaces.Where(v_ws => col.Corresponds(v_ws, charLength)).ToList();

                    if (matchingWs.Any())
                    {
                        foreach (var v_ws in matchingWs)
                        {
                            v_ws.Used = true;

                            var new_whitespaces = new List<VerticalWS>();
                            new_whitespaces.AddRange(col.Whitespaces);
                            var newCol = new Column(new_whitespaces, col.Top, col.Bottom, col.TopPosition, col.BottomPosition);
                            newCol.Add(v_ws);
                            newColumns.Add(newCol);
                        }
                    }
                    else
                    {
                        newColumns.Add(col);
                    }
                }

                newColumns.AddRange(whitespaces.Where(v_ws => !v_ws.Used).Select(v_ws => Column.FromWs(v_ws)));

                columns = newColumns;
            }

            var dictBounds = tableAreas.Select((area, index) => new { area, index })
                         .ToDictionary(
                             x => x.index,
                             x => new Dictionary<string, int>
                             {
                                 { "y_min", x.area.Y1 },
                                 { "y_max", x.area.Y2 }
                             });
            List<Column> reshapedColumns = new List<Column>();
            foreach (var col in columns)
            {
                List<VerticalWS> reshapedWhitespaces = new List<VerticalWS>();

                foreach (var v_ws in col.Whitespaces)
                {
                    int y_min = v_ws.Top ? dictBounds.GetValueOrDefault(v_ws.Position - 1, new Dictionary<string, int>()).GetValueOrDefault("y_max", v_ws.Y1) : v_ws.Y1;
                    int y_max = v_ws.Bottom ? dictBounds.GetValueOrDefault(v_ws.Position + 1, new Dictionary<string, int>()).GetValueOrDefault("y_min", v_ws.Y2) : v_ws.Y2;

                    var reshaped_v_ws = new VerticalWS(
                        new Whitespace(v_ws.Ws.Cells.Select(c => new Cell(
                            col.X1, 
                            c.Y1 == v_ws.Y1 ? y_min : c.Y1, 
                            col.X2, 
                            c.Y2 == v_ws.Y2 ? y_max : c.Y2)).ToList()),
                        v_ws.Position, 
                        v_ws.Top, 
                        v_ws.Bottom, 
                        v_ws.Used
                    );

                    reshapedWhitespaces.Add(reshaped_v_ws);
                }

                Column reshapedCol = new Column(reshapedWhitespaces);
                reshapedColumns.Add(reshapedCol);
            }

            int maxHeight = reshapedColumns.Max(col => col.Height);
            reshapedColumns = reshapedColumns.Where(col => col.Height >= 0.66 * maxHeight).ToList();

            return reshapedColumns;
        }
    }
}
