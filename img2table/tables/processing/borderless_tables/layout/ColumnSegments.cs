using img2table.sharp.img2table.tables.objects;
using System.Linq;
using System.Collections.Generic;
using static img2table.sharp.img2table.tables.objects.Objects;
using static img2table.sharp.img2table.tables.processing.borderless_tables.Model;
using System.Collections.Concurrent;

namespace img2table.sharp.img2table.tables.processing.borderless_tables.layout
{
    public class ColumnSegments
    {
        public static List<ImageSegment> segment_image_columns(ImageSegment image_segment, double char_length, List<Line> lines)
        {
            // 识别垂直空白区域
            List<Cell> vertical_ws = get_vertical_ws(image_segment, char_length, lines);

            // 识别列组
            var column_groups = identify_column_groups(image_segment, vertical_ws);

            if (column_groups.Count == 0)
            {
                return new List<ImageSegment> { image_segment };
            }

            // 从列中识别所有段
            List<ImageSegment> col_segments = get_segments_from_columns(image_segment, column_groups);

            // 填充组中的元素
            List<ImageSegment> final_segments = new List<ImageSegment>();
            foreach (var segment in col_segments)
            {
                List<Cell> segment_elements = new List<Cell>();
                foreach (var el in image_segment.Elements)
                {
                    if (el.X1 >= segment.X1 && el.X2 <= segment.X2 && el.Y1 >= segment.Y1 && el.Y2 <= segment.Y2)
                    {
                        segment_elements.Add(el);
                    }
                }
                if (segment_elements.Count > 0)
                {
                    segment.SetElements(segment_elements);
                    final_segments.Add(segment);
                }
            }

            return final_segments;
        }

        static List<ImageSegment> get_column_group_segments(List<Cell> col_group)
        {
            // 计算由列限定的段
            col_group = col_group.OrderBy(ws => ws.X1 + ws.X2).ToList();
            List<ImageSegment> col_segments = new List<ImageSegment>();

            for (int i = 0; i < col_group.Count - 1; i++)
            {
                var left_ws = col_group[i];
                var right_ws = col_group[i + 1];
                int y1_segment = Math.Max(left_ws.Y1, right_ws.Y1);
                int y2_segment = Math.Min(left_ws.Y2, right_ws.Y2);
                int x1_segment = (left_ws.X1 + left_ws.X2) / 2;
                int x2_segment = (right_ws.X1 + right_ws.X2) / 2;
                var segment = new ImageSegment(x1_segment, y1_segment, x2_segment, y2_segment, new List<Cell>());
                col_segments.Add(segment);
            }

            // 创建由段定义的矩形并识别区域中的剩余段
            var cols_rectangle = new Rectangle(
                col_segments.Min(seg => seg.X1),
                col_segments.Min(seg => seg.Y1),
                col_segments.Max(seg => seg.X2) - col_segments.Min(seg => seg.X1),
                col_segments.Max(seg => seg.Y2) - col_segments.Min(seg => seg.Y1)
            );

            var existing_segments = col_segments.Select(segment => new Rectangle(segment.X1, segment.Y1, segment.Width, segment.Height)).ToList();
            var remaining_segments = identify_remaining_segments(cols_rectangle, existing_segments)
                .Select(area => new ImageSegment(area.X1, area.Y1, area.X2, area.Y2, new List<Cell>())).ToList();

            return col_segments.Concat(remaining_segments).ToList();
        }

        static List<Cell> identify_remaining_segments(Rectangle searched_rectangle, List<Rectangle> obstacles)
        {
            // 初始化队列
            var queue = new Queue<Tuple<double, Rectangle, List<Rectangle>>>();
            queue.Enqueue(Tuple.Create((double)(-searched_rectangle.Area), searched_rectangle, obstacles));

            List<Rectangle> segments = new List<Rectangle>();
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                double q = item.Item1;
                Rectangle r = item.Item2;
                List<Rectangle> obs = item.Item3;

                if (obs.Count == 0)
                {
                    // 更新段
                    segments.Add(r);

                    // 更新队列中的元素
                    foreach (var element in queue)
                    {
                        if (element.Item2.Overlaps(r))
                        {
                            element.Item3.Add(r);
                        }
                    }

                    continue;
                }

                // 获取最相关的障碍物
                Rectangle pivot = obs.OrderBy(o => o.Distance(r)).First();

                // 创建新的矩形
                List<Rectangle> rects = new List<Rectangle>
                {
                    new Rectangle(pivot.X2, r.Y1, r.X2, r.Y2),
                    new Rectangle(r.X1, r.Y1, pivot.X1, r.Y2),
                    new Rectangle(r.X1, pivot.Y2, r.X2, r.Y2),
                    new Rectangle(r.X1, r.Y1, r.X2, pivot.Y1)
                };

                foreach (var rect in rects)
                {
                    if (rect.Area > searched_rectangle.Area / 100)
                    {
                        List<Rectangle> rectObstacles = obs.Where(o => o.Overlaps(rect)).ToList();
                        queue.Enqueue(Tuple.Create(-rect.Area + new Random().NextDouble(), rect, rectObstacles));
                    }
                }
            }

            return segments.Select(seg => seg.ToCell()).ToList();
        }

        static List<ImageSegment> get_segments_from_columns(ImageSegment image_segment, List<List<Cell>> column_groups)
        {
            // 从列组中识别图像段
            List<ImageSegment> col_group_segments = column_groups.SelectMany(col_gp => get_column_group_segments(col_gp)).ToList();

            // 识别列外的段
            ImageSegment top_segment = new ImageSegment(image_segment.X1, image_segment.Y1, image_segment.X2, col_group_segments.Min(seg => seg.Y1));
            ImageSegment bottom_segment = new ImageSegment(image_segment.X1, col_group_segments.Max(seg => seg.Y2), image_segment.X2, image_segment.Y2);
            ImageSegment left_segment = new ImageSegment(image_segment.X1, col_group_segments.Min(seg => seg.Y1), col_group_segments.Min(seg => seg.X1), col_group_segments.Max(seg => seg.Y2));
            ImageSegment right_segment = new ImageSegment(col_group_segments.Max(seg => seg.X2), col_group_segments.Min(seg => seg.Y1), image_segment.X2, col_group_segments.Max(seg => seg.Y2));

            // 创建图像段并识别缺失的段
            List<ImageSegment> img_segments = col_group_segments.Concat(new[] { top_segment, bottom_segment, left_segment, right_segment }).ToList();
            var img_sects = img_segments.Select(segment => new Rectangle(segment.X1, segment.Y1, segment.Width, segment.Height)).ToList();

            List<ImageSegment> missing_segments = identify_remaining_segments(new Rectangle(image_segment.X1, image_segment.Y1, image_segment.Width, image_segment.Height), img_sects)
                .Select(area => new ImageSegment(area.X1, area.Y1, area.X2, area.Y2)).ToList();

            return img_segments.Concat(missing_segments).ToList();
        }
        
        static List<Cell> get_vertical_ws(ImageSegment image_segment, double char_length, List<Line> lines)
        {
            // 识别垂直空白区域
            var v_ws = Whitespaces.get_whitespaces(image_segment, vertical: true, pct: 0.5);
            v_ws = v_ws.Where(ws => ws.Width >= char_length || ws.X1 == image_segment.X1 || ws.X2 == image_segment.X2).ToList();

            if (v_ws.Count == 0)
            {
                return new List<Cell>();
            }

            // 切割带有水平线的空白区域
            List<Cell> line_ws = new List<Cell>();
            List<Line> h_lines = lines.Where(line => line.Horizontal).ToList();
            foreach (var ws in v_ws)
            {
                // 获取交叉的水平线
                var crossing_h_lines = h_lines.Where(line => ws.Y1 < line.Y1 && line.Y1 < ws.Y2
                    && Math.Min(ws.X2, line.X2) - Math.Max(ws.X1, line.X1) >= 0.5 * ws.Width)
                    .OrderBy(line => line.Y1).ToList();

                if (crossing_h_lines.Count > 0)
                {
                    // 获取空白区域和交叉线的y值
                    var y_values = new List<int> { ws.Y1, ws.Y2 }
                        .Concat(crossing_h_lines.Select(line => line.Y1 - line.Thickness?? 0))
                        .Concat(crossing_h_lines.Select(line => line.Y1 + line.Thickness?? 0))
                        .OrderBy(y => y).ToList();

                    // 创建新的子空白区域
                    for (int i = 0; i < y_values.Count - 1; i += 2)
                    {
                        int y_top = y_values[i];
                        int y_bottom = y_values[i + 1];
                        if (y_bottom - y_top >= 0.5 * image_segment.Height)
                        {
                            line_ws.Add(new Cell(ws.X1, y_top, ws.X2, y_bottom));
                        }
                    }
                }
                else
                {
                    line_ws.Add(ws);
                }
            }

            if (line_ws.Count == 0)
            {
                return new List<Cell>();
            }

            // 创建相邻空白区域的组
            line_ws = line_ws.OrderBy(ws => ws.X1 + ws.X2).ToList();
            var line_ws_groups = new List<List<Cell>> { new List<Cell> { line_ws[0] } };
            for (int i = 1; i < line_ws.Count; i++)
            {
                var ws = line_ws[i];
                var prev_ws = line_ws_groups.Last().Last();

                // 获取由两个空白区域限定的区域
                int x1_area = Math.Min(prev_ws.X2, ws.X1);
                int x2_area = Math.Max(prev_ws.X2, ws.X1);
                int y1_area = Math.Max(prev_ws.Y1, ws.Y1);
                int y2_area = Math.Min(prev_ws.Y2, ws.Y2);
                var area = new Cell(x1_area, y1_area, x2_area, y2_area);

                // 获取分隔元素
                var separating_elements = image_segment.Elements.Where(el => el.X1 >= area.X1 && el.X2 <= area.X2
                    && el.Y1 >= area.Y1 && el.Y2 <= area.Y2).ToList();

                if (separating_elements.Count > 0)
                {
                    line_ws_groups.Add(new List<Cell>());
                }
                line_ws_groups.Last().Add(ws);
            }

            // 仅保留每组中最高的空白区域
            var final_ws = line_ws_groups.Select(cl =>
            {
                var tallestWhitespaces = cl.Where(ws => ws.Height == cl.Max(w => w.Height)).ToList();
                return tallestWhitespaces.OrderBy(w => w.Area).Last();
            }).ToList();

            return final_ws;
        }

        static List<List<Cell>> identify_column_groups(ImageSegment image_segment, List<Cell> vertical_ws)
        {
            // 识别图像中间和边缘的空白区域
            var middle_ws = vertical_ws.Where(ws => !new HashSet<int> { ws.X1, ws.X2 }.Intersect(new HashSet<int> { image_segment.X1, image_segment.X2 }).Any()).ToList();
            var edge_ws = vertical_ws.Where(ws => new HashSet<int> { ws.X1, ws.X2 }.Intersect(new HashSet<int> { image_segment.X1, image_segment.X2 }).Any()).ToList();

            // 基于顶部/底部对齐创建列组
            Func<Cell, Cell, bool> top_matches = (col_1, col_2) => Math.Abs(col_1.Y1 - col_2.Y1) / Math.Max(col_1.Height, col_2.Height) <= 0.05;
            Func<Cell, Cell, bool> bottom_matches = (col_1, col_2) => Math.Abs(col_1.Y2 - col_2.Y2) / Math.Max(col_1.Height, col_2.Height) <= 0.05;

            var top_col_groups = Processing.cluster_items(middle_ws, top_matches).Select(cl => cl.Concat(edge_ws).ToList()).ToList();
            var bottom_col_groups = Processing.cluster_items(middle_ws, bottom_matches).Select(cl => cl.Concat(edge_ws).ToList()).ToList();

            // 识别对应于列的组
            var col_groups = top_col_groups.Concat(bottom_col_groups).Where(gp => is_column_section(gp)).OrderByDescending(gp => gp.Count).ToList();

            // 获取包含所有相关空白区域的组
            var filtered_col_groups = new List<List<Cell>>();
            foreach (var col_gp in col_groups)
            {
                int y_min = col_gp.Min(ws => ws.Y1);
                int y_max = col_gp.Max(ws => ws.Y2);
                var matching_ws = vertical_ws.Where(ws => Math.Min(ws.Y2, y_max) - Math.Max(ws.Y1, y_min) > 0.2 * ws.Height
                    && !new HashSet<int> { ws.X1, ws.X2 }.Intersect(new HashSet<int> { image_segment.X1, image_segment.X2 }).Any()).ToList();
                if (!matching_ws.Except(col_gp).Any())
                {
                    filtered_col_groups.Add(col_gp);
                }
            }

            if (filtered_col_groups.Count == 0)
            {
                return new List<List<Cell>>();
            }

            // 去重列组
            var dedup_col_groups = new List<List<Cell>> { filtered_col_groups.First() };
            foreach (var col_gp in filtered_col_groups.Skip(1))
            {
                if (!dedup_col_groups.Any(gp => new HashSet<Cell>(col_gp).SetEquals(gp)))
                {
                    dedup_col_groups.Add(col_gp);
                }
            }

            return dedup_col_groups;
        }

        static bool is_column_section(List<Cell> ws_group)
        {
            // 检查潜在列的数量
            if (ws_group.Count < 3 || ws_group.Count > 4)
            {
                return false;
            }

            // 检查列宽度是否一致
            ws_group = ws_group.OrderBy(ws => ws.X1 + ws.X2).ToList();
            List<int> col_widths = new List<int>();
            for (int i = 0; i < ws_group.Count - 1; i++)
            {
                int width = ws_group[i + 1].X1 - ws_group[i].X2;
                col_widths.Add(width);
            }

            return col_widths.Max() / (double)col_widths.Min() <= 1.25;
        }

        class Rectangle
        {
            public int X1 { get; }
            public int Y1 { get; }
            public int X2 { get; }
            public int Y2 { get; }

            public int Width => X2 - X1;
            public int Height => Y2 - Y1;
            public int Area => Width * Height;

            public Rectangle(int x1, int y1, int x2, int y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }

            public static Rectangle FromCell(Cell cell)
            {
                return new Rectangle(cell.X1, cell.Y1, cell.X2, cell.Y2);
            }

            public (double, double) Center => ((X1 + X2) / 2.0, (Y1 + Y2) / 2.0);

            public Cell ToCell()
            {
                return new Cell(X1, Y1, X2, Y2);
            }

            public double Distance(Rectangle other)
            {
                var (centerX, centerY) = Center;
                var (otherCenterX, otherCenterY) = other.Center;
                return Math.Pow(centerX - otherCenterX, 2) + Math.Pow(centerY - otherCenterY, 2);
            }

            public bool Overlaps(Rectangle other)
            {
                int xLeft = Math.Max(X1, other.X1);
                int yTop = Math.Max(Y1, other.Y1);
                int xRight = Math.Min(X2, other.X2);
                int yBottom = Math.Min(Y2, other.Y2);

                return Math.Max(xRight - xLeft, 0) * Math.Max(yBottom - yTop, 0) > 0;
            }

            public override string ToString()
            {
                return $"Rectangle(X1: {X1}, Y1: {Y1}, X2: {X2}, Y2: {Y2})";
            }
        }
    }
}
