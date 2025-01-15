using Img2table.Sharp.Tabular.TableImage.Processing;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.TableImageStructure;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.Layout
{
    public class TableSegments
    {
        public static List<TableSegment> GetTableSegments(ImageSegment segment, double charLength, double medianLineSep)
        {
            List<ImageSegment> tableAreas = GetTableAreas(segment, charLength, medianLineSep);

            if (tableAreas.Count == 0)
            {
                return new List<TableSegment>();
            }

            tableAreas = tableAreas.OrderBy(tb => tb.Position).ToList();
            var tbAreasGps = new List<List<ImageSegment>> { new List<ImageSegment> { tableAreas.First() } };
            foreach (var tbArea in tableAreas.Skip(1))
            {
                var prevTable = tbAreasGps.Last().Last();
                if (!CoherentTableAreas(prevTable, tbArea, charLength, medianLineSep))
                {
                    tbAreasGps.Add(new List<ImageSegment>());
                }
                tbAreasGps.Last().Add(tbArea);
            }

            var tableSegments = new List<TableSegment>();
            foreach (var tbAreaGp in tbAreasGps)
            {
                int maxWhitespaces = 0;
                foreach (var tbArea in tbAreaGp)
                {
                    if (tbArea.Whitespaces.Count > maxWhitespaces)
                    {
                        maxWhitespaces = tbArea.Whitespaces.Count;
                    }
                }

                if (maxWhitespaces > 3)
                {
                    tableSegments.Add(new TableSegment(tbAreaGp));
                }
            }

            return tableSegments;
        }

        private static List<ImageSegment> GetTableAreas(ImageSegment segment, double charLength, double medianLineSep)
        {
            List<Whitespace> h_ws = WhitespaceIdentifier.GetWhitespaces(segment, vertical: false, pct: 1, min_width: 0.5 * medianLineSep);
            h_ws = h_ws.OrderBy(ws => ws.Y1).ToList();

            if (h_ws.Count == 0)
            {
                h_ws = new List<Whitespace>
                {
                    new Whitespace(new List<Cell>
                    {
                        new Cell(segment.Elements.Min(el => el.X1),
                                 segment.Y1,
                                 segment.Elements.Max(el => el.X2),
                                 segment.Y1)
                    }),
                    new Whitespace(new List<Cell>
                    {
                        new Cell(segment.Elements.Min(el => el.X1),
                                 segment.Y2,
                                 segment.Elements.Max(el => el.X2),
                                 segment.Y2)
                    })
                };
            }

            if (h_ws.First().Y1 > segment.Y1)
            {
                var up_ws = new Whitespace(new List<Cell>
                {
                    new Cell(h_ws.Min(ws => ws.X1),
                             segment.Y1,
                             h_ws.Max(ws => ws.X2),
                             segment.Elements.Min(el => el.Y1))
                });
                h_ws.Insert(0, up_ws);
            }

            if (h_ws.Last().Y2 < segment.Y2)
            {
                var down_ws = new Whitespace(new List<Cell>
                {
                    new Cell(h_ws.Min(ws => ws.X1),
                             segment.Y2,
                             h_ws.Max(ws => ws.X2),
                             segment.Elements.Max(el => el.Y2))
                });
                h_ws.Add(down_ws);
            }

            List<ImageSegment> tableAreas = new List<ImageSegment>();
            int idx = 0;

            for (int i = 0; i < h_ws.Count - 1; i++)
            {
                var up = h_ws[i];
                var down = h_ws[i + 1];
                idx++;

                var delimitedArea = new Cell(
                    Math.Max(Math.Min(up.X1, down.X1) - (int)charLength, 0),
                    up.Y2,
                    Math.Min(Math.Max(up.X2, down.X2) + (int)charLength, segment.X2),
                    down.Y1
                );

                var areaElements = segment.Elements.Where(el =>
                    el.X1 >= delimitedArea.X1 && el.X2 <= delimitedArea.X2 &&
                    el.Y1 >= delimitedArea.Y1 && el.Y2 <= delimitedArea.Y2).ToList();
                var segArea = new ImageSegment(
                    delimitedArea.X1,
                    delimitedArea.Y1,
                    delimitedArea.X2,
                    delimitedArea.Y2,
                    areaElements,
                    position: idx
                );

                if (areaElements.Count > 0)
                {
                    var v_ws = WhitespaceIdentifier.GetRelevantVerticalWhitespaces(segArea, charLength, medianLineSep, pct: 0.66);

                    var middle_ws = v_ws.Where(ws => ws.X1 != segArea.X1 && ws.X2 != segArea.X2).ToList();

                    if (middle_ws.Count >= 1)
                    {
                        var left_ws = new Whitespace(new List<Cell>
                        {
                            new Cell(segArea.X1, segArea.Y1, segArea.Elements.Min(el => el.X1), segArea.Y2)
                        });
                        var right_ws = new Whitespace(new List<Cell>
                        {
                            new Cell(segArea.Elements.Max(el => el.X2), segArea.Y1, segArea.X2, segArea.Y2)
                        });
                        v_ws = v_ws.Where(ws =>
                            !Common.IsContainedCell(ws, left_ws, 0.1) &&
                            !Common.IsContainedCell(ws, right_ws, 0.1) &&
                            (new HashSet<int> { ws.Y1, ws.Y2 }.Intersect(new HashSet<int> { segArea.Y1, segArea.Y2 }).Count() > 0 ||
                             ws.Height >= 0.66 * middle_ws.Max(w => w.Height))
                        ).ToList();

                        segArea.SetWhitespaces(v_ws.Concat(new[] { left_ws, right_ws }).OrderBy(ws => ws.X1 + ws.X2).ToList());
                        tableAreas.Add(segArea);
                    }
                }
            }

            return tableAreas;
        }

        private static List<Cell> MergeConsecutiveWS(List<Whitespace> whitespaces)
        {
            whitespaces = whitespaces.OrderBy(ws => ws.X1 + ws.X2).ToList();

            var wsGroups = new List<List<Whitespace>> { new List<Whitespace> { whitespaces.First() } };
            foreach (var ws in whitespaces.Skip(1))
            {
                if (ws.X1 > wsGroups.Last().Last().X2)
                {
                    wsGroups.Add(new List<Whitespace>());
                }
                wsGroups.Last().Add(ws);
            }

            return wsGroups.Select(wsGp => new Cell(
                wsGp.First().X1,
                wsGp.Min(ws => ws.Y1),
                wsGp.Last().X2,
                wsGp.Max(ws => ws.Y2)
            )).ToList();
        }

        private static bool CoherentTableAreas(ImageSegment tbArea1, ImageSegment tbArea2, double charLength, double medianLineSep)
        {
            double vDiff = Math.Max(tbArea1.Y1, tbArea2.Y1) - Math.Min(tbArea1.Y2, tbArea2.Y2);

            if (Math.Abs(tbArea1.Position.Value - tbArea2.Position.Value) != 1 || vDiff > 2.5 * medianLineSep)
            {
                return false;
            }

            List<Cell> wsTb1, wsTb2;
            if (tbArea1.Position < tbArea2.Position)
            {
                wsTb1 = MergeConsecutiveWS(tbArea1.Whitespaces.Where(ws => ws.Y2 == tbArea1.Y2).ToList());
                wsTb2 = MergeConsecutiveWS(tbArea2.Whitespaces.Where(ws => ws.Y1 == tbArea2.Y1).ToList());
            }
            else
            {
                wsTb1 = MergeConsecutiveWS(tbArea1.Whitespaces.Where(ws => ws.Y1 == tbArea1.Y1).ToList());
                wsTb2 = MergeConsecutiveWS(tbArea2.Whitespaces.Where(ws => ws.Y2 == tbArea2.Y2).ToList());
            }

            Dictionary<int, List<Cell>> dictWsCoherency;
            if (wsTb1.Count >= wsTb2.Count)
            {
                dictWsCoherency = wsTb1.Skip(1).Take(wsTb1.Count - 2)
                    .Select((ws1, idx1) => new { idx1, ws1 })
                    .ToDictionary(
                        x => x.idx1,
                        x => wsTb2.Where(ws2 => Math.Min(x.ws1.X2, ws2.X2) - Math.Max(x.ws1.X1, ws2.X1) >= 0.5 * charLength).ToList()
                    );
            }
            else
            {
                dictWsCoherency = wsTb2.Skip(1).Take(wsTb2.Count - 2)
                    .Select((ws2, idx2) => new { idx2, ws2 })
                    .ToDictionary(
                        x => x.idx2,
                        x => wsTb1.Where(ws1 => Math.Min(ws1.X2, x.ws2.X2) - Math.Max(ws1.X1, x.ws2.X1) >= 0.5 * charLength).ToList()
                    );
            }

            double threshold;
            if (Math.Min(wsTb1.Count, wsTb2.Count) < 4)
            {
                threshold = 1;
            }
            else if (vDiff < medianLineSep)
            {
                threshold = 0.66;
            }
            else
            {
                threshold = 0.8;
            }

            return dictWsCoherency.Values.Average(v => v.Count == 1 ? 1 : 0) >= threshold;
        }

        public static ImageSegment TableSegmentFromGroup(List<ImageSegment> tableSegmentGroup)
        {
            var elements = tableSegmentGroup.SelectMany(seg => seg.Elements).ToList();
            var whitespaces = tableSegmentGroup.SelectMany(seg => seg.Whitespaces).ToList();

            var tableSegment = new ImageSegment(
                tableSegmentGroup.Min(seg => seg.X1),
                tableSegmentGroup.Min(seg => seg.Y1),
                tableSegmentGroup.Max(seg => seg.X2),
                tableSegmentGroup.Max(seg => seg.Y2),
                elements,
                whitespaces
            );

            return tableSegment;
        }
    }
}
