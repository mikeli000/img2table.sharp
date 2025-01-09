using img2table.sharp.img2table.tables.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.processing.borderless_tables.Model;

namespace img2table.sharp.img2table.tables.processing.borderless_tables.layout
{
    public class TableSegments
    {
        public static List<TableSegment> get_table_segments(ImageSegment segment, double charLength, double medianLineSep)
        {
            // 获取表格区域
            List<ImageSegment> tableAreas = get_table_areas(segment, charLength, medianLineSep);

            if (tableAreas.Count == 0)
            {
                return new List<TableSegment>();
            }

            // 创建表格区域的组
            tableAreas = tableAreas.OrderBy(tb => tb.Position).ToList();
            var tbAreasGps = new List<List<ImageSegment>> { new List<ImageSegment> { tableAreas.First() } };
            foreach (var tbArea in tableAreas.Skip(1))
            {
                var prevTable = tbAreasGps.Last().Last();
                if (!coherent_table_areas(prevTable, tbArea, charLength, medianLineSep))
                {
                    tbAreasGps.Add(new List<ImageSegment>());
                }
                tbAreasGps.Last().Add(tbArea);
            }

            // 创建对应于潜在表格的图像段
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

            //var tableSegments = tbAreasGps
            //    .Where(tbAreaGp => tbAreaGp.Max(tbArea => tbArea.Whitespaces.Count) > 3)
            //    .Select(tbAreaGp => new TableSegment(tbAreaGp))
            //    .ToList();

            return tableSegments;
        }

        static List<ImageSegment> get_table_areas(ImageSegment segment, double charLength, double medianLineSep)
        {
            // 识别段中的水平空白区域
            List<Whitespace> h_ws = Whitespaces.get_whitespaces(segment, vertical: false, pct: 1, min_width: 0.5 * medianLineSep);
            h_ws = h_ws.OrderBy(ws => ws.Y1).ToList();

            // 处理没有找到空白区域的情况
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

            // 在顶部或底部创建空白区域
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

            // 检查水平空白区域之间的区域以识别它们是否可以对应于表格
            List<ImageSegment> tableAreas = new List<ImageSegment>();
            int idx = 0;

            for (int i = 0; i < h_ws.Count - 1; i++)
            {
                var up = h_ws[i];
                var down = h_ws[i + 1];
                idx++;

                // 获取限定区域
                var delimitedArea = new Cell(
                    Math.Max(Math.Min(up.X1, down.X1) - (int)charLength, 0),
                    up.Y2,
                    Math.Min(Math.Max(up.X2, down.X2) + (int)charLength, segment.X2),
                    down.Y1
                );

                // 识别相应的元素并创建相应的段
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
                    // 识别区域中的垂直空白区域
                    var v_ws = Whitespaces.get_relevant_vertical_whitespaces(segArea, charLength, medianLineSep, pct: 0.66);

                    // 识别不在边界上的空白区域数量
                    var middle_ws = v_ws.Where(ws => ws.X1 != segArea.X1 && ws.X2 != segArea.X2).ToList();

                    // 如果区域中至少有3列，则它是一个可能的表格区域
                    if (middle_ws.Count >= 1)
                    {
                        // 添加边缘空白区域
                        var left_ws = new Whitespace(new List<Cell>
                        {
                            new Cell(segArea.X1, segArea.Y1, segArea.Elements.Min(el => el.X1), segArea.Y2)
                        });
                        var right_ws = new Whitespace(new List<Cell>
                        {
                            new Cell(segArea.Elements.Max(el => el.X2), segArea.Y1, segArea.X2, segArea.Y2)
                        });
                        v_ws = v_ws.Where(ws =>
                            !Common.is_contained_cell(ws, left_ws, 0.1) &&
                            !Common.is_contained_cell(ws, right_ws, 0.1) &&
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

        static List<Cell> merge_consecutive_ws(List<Whitespace> whitespaces)
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

        static bool coherent_table_areas(ImageSegment tbArea1, ImageSegment tbArea2, double charLength, double medianLineSep)
        {
            // 计算垂直差异
            double vDiff = Math.Max(tbArea1.Y1, tbArea2.Y1) - Math.Min(tbArea1.Y2, tbArea2.Y2);

            // 如果区域不连续或分隔过大，则不一致
            if (Math.Abs(tbArea1.Position.Value - tbArea2.Position.Value) != 1 || vDiff > 2.5 * medianLineSep)
            {
                return false;
            }

            // 获取相关空白区域
            List<Cell> wsTb1, wsTb2;
            if (tbArea1.Position < tbArea2.Position)
            {
                wsTb1 = merge_consecutive_ws(tbArea1.Whitespaces.Where(ws => ws.Y2 == tbArea1.Y2).ToList());
                wsTb2 = merge_consecutive_ws(tbArea2.Whitespaces.Where(ws => ws.Y1 == tbArea2.Y1).ToList());
            }
            else
            {
                wsTb1 = merge_consecutive_ws(tbArea1.Whitespaces.Where(ws => ws.Y1 == tbArea1.Y1).ToList());
                wsTb2 = merge_consecutive_ws(tbArea2.Whitespaces.Where(ws => ws.Y2 == tbArea2.Y2).ToList());
            }

            // 检查“中间”空白区域的一致性
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

            // 计算一致性阈值
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

        static ImageSegment table_segment_from_group(List<ImageSegment> tableSegmentGroup)
        {
            // 获取所有元素
            var elements = tableSegmentGroup.SelectMany(seg => seg.Elements).ToList();
            var whitespaces = tableSegmentGroup.SelectMany(seg => seg.Whitespaces).ToList();

            // 创建 ImageSegment 对象
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
