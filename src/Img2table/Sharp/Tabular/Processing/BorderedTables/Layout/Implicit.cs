using static Img2table.Sharp.Tabular.Processing.BorderlessTables.TableImageStructure;
using Img2table.Sharp.Tabular.TableElement;
using Img2table.Sharp.Tabular.Processing.BorderlessTables;

namespace Img2table.Sharp.Tabular.Processing.BorderedTables.Layout
{
    public class Implicit
    {
        public static Table ImplicitContent(Table table, List<Cell> contours, double charLength, bool implicitRows = false, bool implicitColumns = false)
        {
            if (!implicitRows && !implicitColumns)
            {
                return table;
            }

            List<Cell> tbContours = contours.Where(c => c.X1 >= table.X1 && c.X2 <= table.X2 && c.Y1 >= table.Y1 && c.Y2 <= table.Y2).ToList();
            ImageSegment segment = new ImageSegment(table.X1, table.Y1, table.X2, table.Y2, tbContours);

            List<Line> lines = table.Lines;
            if (implicitRows)
            {
                lines.AddRange(ImplicitRowsLines(table, segment));
            }
            if (implicitColumns)
            {
                lines.AddRange(ImplicitColumnsLines(table, segment, charLength));
            }

            List<Cell> cells = CellDetector.DetectCells(lines.Where(line => line.Horizontal).ToList(), lines.Where(line => line.Vertical).ToList());

            return TableCreation.ClusterToTable(cells, tbContours, false);
        }

        static List<Line> ImplicitRowsLines(Table table, ImageSegment segment)
        {
            List<Whitespace> hWs = WhitespaceIdentifier.GetWhitespaces(segment, vertical: false, pct: 1);

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

            if (hWs.Count > 2)
            {
                List<int> fullWsH = hWs.Skip(1).Take(hWs.Count - 2).Where(ws => ws.Width == hWs.Max(w => w.Width)).Select(ws => ws.Height).OrderBy(h => h).ToList();
                int minHeight = fullWsH.Count >= 3 ? (int)(0.5 * fullWsH[fullWsH.Count / 2 + fullWsH.Count % 2 - 1]) : 1;
                hWs = new List<Whitespace> { hWs[0] }.Concat(hWs.Skip(1).Take(hWs.Count - 2).Where(ws => ws.Height >= minHeight)).Concat(new List<Whitespace> { hWs[^1] }).ToList();
            }

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
            List<Whitespace> vWs = WhitespaceIdentifier.GetWhitespaces(segment, vertical: true, min_width: charLength, pct: 1);

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
