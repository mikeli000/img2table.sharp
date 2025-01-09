using img2table.sharp.img2table.tables.objects;
using img2table.sharp.img2table.tables.processing.bordered_tables.cells;
using img2table.sharp.img2table.tables.processing.borderless_tables;
using static img2table.sharp.img2table.tables.objects.Objects;
using static img2table.sharp.img2table.tables.processing.borderless_tables.Model;

namespace img2table.sharp.img2table.tables.processing.bordered_tables.tables
{
    public class Implicit
    {
        public static Table implicit_content(Table table, List<Cell> contours, double charLength, bool implicitRows = false, bool implicitColumns = false)
        {
            if (!implicitRows && !implicitColumns)
            {
                return table;
            }

            // 获取表格轮廓并创建相应的段
            List<Cell> tbContours = contours.Where(c => c.X1 >= table.X1 && c.X2 <= table.X2 && c.Y1 >= table.Y1 && c.Y2 <= table.Y2).ToList();
            ImageSegment segment = new ImageSegment(table.X1, table.Y1, table.X2, table.Y2, tbContours);

            // 创建新线条
            List<Line> lines = table.Lines;
            if (implicitRows)
            {
                lines.AddRange(ImplicitRowsLines(table, segment));
            }
            if (implicitColumns)
            {
                lines.AddRange(ImplicitColumnsLines(table, segment, charLength));
            }

            // 创建单元格
            List<Cell> cells = Cells.get_cells(lines.Where(line => line.Horizontal).ToList(), lines.Where(line => line.Vertical).ToList());

            return TableCreation.cluster_to_table(cells, tbContours, false);
        }

        static List<Line> ImplicitRowsLines(Table table, ImageSegment segment)
        {
            // 水平空白区域
            List<Whitespace> hWs = Whitespaces.get_whitespaces(segment, vertical: false, pct: 1);

            // 如果缺少顶部或底部的空白区域，则创建它们
            if (hWs[0].Y1 > segment.Y1)
            {
                Whitespace upWs = new Whitespace(new List<Cell>
                {
                    new Cell(hWs.Min(ws => ws.X1), segment.Y1, hWs.Max(ws => ws.X2), segment.Elements.Min(el => el.Y1))
                });
                hWs.Insert(0, upWs);
            }

            if (hWs[^1].Y2 < segment.Y2)
            {
                Whitespace downWs = new Whitespace(new List<Cell>
                {
                    new Cell(hWs.Min(ws => ws.X1), segment.Elements.Max(el => el.Y2), hWs.Max(ws => ws.X2), segment.Y2)
                });
                hWs.Add(downWs);
            }

            // 识别相关的空白区域高度
            if (hWs.Count > 2)
            {
                List<int> fullWsH = hWs.Skip(1).Take(hWs.Count - 2).Where(ws => ws.Width == hWs.Max(w => w.Width)).Select(ws => ws.Height).OrderBy(h => h).ToList();
                int minHeight = fullWsH.Count >= 3 ? (int)(0.5 * fullWsH[(fullWsH.Count / 2) + (fullWsH.Count % 2) - 1]) : 1;
                hWs = new List<Whitespace> { hWs[0] }.Concat(hWs.Skip(1).Take(hWs.Count - 2).Where(ws => ws.Height >= minHeight)).Concat(new List<Whitespace> { hWs[^1] }).ToList();
            }

            // 识别创建的线条
            List<Line> createdLines = new List<Line>();
            foreach (var ws in hWs)
            {
                if (!table.Lines.Any(line => ws.Y1 <= line.Y1 && line.Y1 <= ws.Y2 && line.Horizontal))
                {
                    createdLines.Add(new Line(table.X1, (ws.Y1 + ws.Y2) / 2, table.X2, (ws.Y1 + ws.Y2) / 2));
                }
            }

            return createdLines;
        }

        static List<Line> ImplicitColumnsLines(Table table, ImageSegment segment, double charLength)
        {
            // 垂直空白区域
            List<Whitespace> vWs = Whitespaces.get_whitespaces(segment, vertical: true, min_width: charLength, pct: 1);

            // 识别创建的线条
            List<Line> createdLines = new List<Line>();
            foreach (var ws in vWs)
            {
                if (!table.Lines.Any(line => ws.X1 <= line.X1 && line.X1 <= ws.X2 && line.Vertical))
                {
                    createdLines.Add(new Line((ws.X1 + ws.X2) / 2, table.Y1, (ws.X1 + ws.X2) / 2, table.Y2));
                }
            }

            return createdLines;
        }
    }
}
