using Img2table.Sharp.Tabular.TableImage.Processing;
using Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using static Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.TableImageStructure;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderlessTables.Layout
{
    public class ColumnSegments
    {
        public static List<ImageSegment> SegmentImageColumns(ImageSegment imageSegment, double charLength, List<Line> lines)
        {
            List<Cell> verticalWS = GetVerticalWS(imageSegment, charLength, lines);

            var columnGroups = IdentifyColumnGroups(imageSegment, verticalWS);

            if (columnGroups.Count == 0)
            {
                return new List<ImageSegment> { imageSegment };
            }

            List<ImageSegment> colSegments = GetSegmentsFromColumns(imageSegment, columnGroups);

            List<ImageSegment> finalSegments = new List<ImageSegment>();
            foreach (var segment in colSegments)
            {
                List<Cell> segmentElements = new List<Cell>();
                foreach (var el in imageSegment.Elements)
                {
                    if (el.X1 >= segment.X1 && el.X2 <= segment.X2 && el.Y1 >= segment.Y1 && el.Y2 <= segment.Y2)
                    {
                        segmentElements.Add(el);
                    }
                }
                if (segmentElements.Count > 0)
                {
                    segment.SetElements(segmentElements);
                    finalSegments.Add(segment);
                }
            }

            return finalSegments;
        }

        private static List<ImageSegment> GetColumnGroupSegments(List<Cell> colGroup)
        {
            colGroup = colGroup.OrderBy(ws => ws.X1 + ws.X2).ToList();
            List<ImageSegment> col_segments = new List<ImageSegment>();

            for (int i = 0; i < colGroup.Count - 1; i++)
            {
                var left_ws = colGroup[i];
                var right_ws = colGroup[i + 1];
                int y1_segment = Math.Max(left_ws.Y1, right_ws.Y1);
                int y2_segment = Math.Min(left_ws.Y2, right_ws.Y2);
                int x1_segment = (left_ws.X1 + left_ws.X2) / 2;
                int x2_segment = (right_ws.X1 + right_ws.X2) / 2;
                var segment = new ImageSegment(x1_segment, y1_segment, x2_segment, y2_segment, new List<Cell>());
                col_segments.Add(segment);
            }

            var cols_rectangle = new Rectangle(
                col_segments.Min(seg => seg.X1),
                col_segments.Min(seg => seg.Y1),
                col_segments.Max(seg => seg.X2) - col_segments.Min(seg => seg.X1),
                col_segments.Max(seg => seg.Y2) - col_segments.Min(seg => seg.Y1)
            );

            var existing_segments = col_segments.Select(segment => new Rectangle(segment.X1, segment.Y1, segment.Width, segment.Height)).ToList();
            var remaining_segments = IdentifyRemainingSegments(cols_rectangle, existing_segments)
                .Select(area => new ImageSegment(area.X1, area.Y1, area.X2, area.Y2, new List<Cell>())).ToList();

            return col_segments.Concat(remaining_segments).ToList();
        }

        private static List<Cell> IdentifyRemainingSegments(Rectangle searchedRectangle, List<Rectangle> obstacles)
        {
            var queue = new Queue<Tuple<double, Rectangle, List<Rectangle>>>();
            queue.Enqueue(Tuple.Create((double)-searchedRectangle.Area, searchedRectangle, obstacles));

            List<Rectangle> segments = new List<Rectangle>();
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                double q = item.Item1;
                Rectangle r = item.Item2;
                List<Rectangle> obs = item.Item3;

                if (obs.Count == 0)
                {
                    segments.Add(r);

                    foreach (var element in queue)
                    {
                        if (element.Item2.Overlaps(r))
                        {
                            element.Item3.Add(r);
                        }
                    }

                    continue;
                }

                Rectangle pivot = obs.OrderBy(o => o.Distance(r)).First();

                List<Rectangle> rects = new List<Rectangle>
                {
                    new Rectangle(pivot.X2, r.Y1, r.X2, r.Y2),
                    new Rectangle(r.X1, r.Y1, pivot.X1, r.Y2),
                    new Rectangle(r.X1, pivot.Y2, r.X2, r.Y2),
                    new Rectangle(r.X1, r.Y1, r.X2, pivot.Y1)
                };

                foreach (var rect in rects)
                {
                    if (rect.Area > searchedRectangle.Area / 100)
                    {
                        List<Rectangle> rectObstacles = obs.Where(o => o.Overlaps(rect)).ToList();
                        queue.Enqueue(Tuple.Create(-rect.Area + new Random().NextDouble(), rect, rectObstacles));
                    }
                }
            }

            return segments.Select(seg => seg.ToCell()).ToList();
        }

        private static List<ImageSegment> GetSegmentsFromColumns(ImageSegment imageSegment, List<List<Cell>> columnGroups)
        {
            List<ImageSegment> colGroupSegments = columnGroups.SelectMany(col_gp => GetColumnGroupSegments(col_gp)).ToList();

            ImageSegment top_segment = new ImageSegment(imageSegment.X1, imageSegment.Y1, imageSegment.X2, colGroupSegments.Min(seg => seg.Y1));
            ImageSegment bottom_segment = new ImageSegment(imageSegment.X1, colGroupSegments.Max(seg => seg.Y2), imageSegment.X2, imageSegment.Y2);
            ImageSegment left_segment = new ImageSegment(imageSegment.X1, colGroupSegments.Min(seg => seg.Y1), colGroupSegments.Min(seg => seg.X1), colGroupSegments.Max(seg => seg.Y2));
            ImageSegment right_segment = new ImageSegment(colGroupSegments.Max(seg => seg.X2), colGroupSegments.Min(seg => seg.Y1), imageSegment.X2, colGroupSegments.Max(seg => seg.Y2));

            List<ImageSegment> imgSegments = colGroupSegments.Concat(new[] { top_segment, bottom_segment, left_segment, right_segment }).ToList();
            var img_sects = imgSegments.Select(segment => new Rectangle(segment.X1, segment.Y1, segment.Width, segment.Height)).ToList();

            List<ImageSegment> missingSegments = IdentifyRemainingSegments(new Rectangle(imageSegment.X1, imageSegment.Y1, imageSegment.Width, imageSegment.Height), img_sects)
                .Select(area => new ImageSegment(area.X1, area.Y1, area.X2, area.Y2)).ToList();

            return imgSegments.Concat(missingSegments).ToList();
        }

        private static List<Cell> GetVerticalWS(ImageSegment imageSegment, double charLength, List<Line> lines)
        {
            var v_ws = WhitespaceIdentifier.GetWhitespaces(imageSegment, vertical: true, pct: 0.5);
            v_ws = v_ws.Where(ws => ws.Width >= charLength || ws.X1 == imageSegment.X1 || ws.X2 == imageSegment.X2).ToList();

            if (v_ws.Count == 0)
            {
                return new List<Cell>();
            }

            List<Cell> line_ws = new List<Cell>();
            List<Line> h_lines = lines.Where(line => line.Horizontal).ToList();
            foreach (var ws in v_ws)
            {
                var crossing_h_lines = h_lines.Where(line => ws.Y1 < line.Y1 && line.Y1 < ws.Y2
                    && Math.Min(ws.X2, line.X2) - Math.Max(ws.X1, line.X1) >= 0.5 * ws.Width)
                    .OrderBy(line => line.Y1).ToList();

                if (crossing_h_lines.Count > 0)
                {
                    var y_values = new List<int> { ws.Y1, ws.Y2 }
                        .Concat(crossing_h_lines.Select(line => line.Y1 - line.Thickness ?? 0))
                        .Concat(crossing_h_lines.Select(line => line.Y1 + line.Thickness ?? 0))
                        .OrderBy(y => y).ToList();

                    for (int i = 0; i < y_values.Count - 1; i += 2)
                    {
                        int y_top = y_values[i];
                        int y_bottom = y_values[i + 1];
                        if (y_bottom - y_top >= 0.5 * imageSegment.Height)
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

            line_ws = line_ws.OrderBy(ws => ws.X1 + ws.X2).ToList();
            var line_ws_groups = new List<List<Cell>> { new List<Cell> { line_ws[0] } };
            for (int i = 1; i < line_ws.Count; i++)
            {
                var ws = line_ws[i];
                var prev_ws = line_ws_groups.Last().Last();

                int x1_area = Math.Min(prev_ws.X2, ws.X1);
                int x2_area = Math.Max(prev_ws.X2, ws.X1);
                int y1_area = Math.Max(prev_ws.Y1, ws.Y1);
                int y2_area = Math.Min(prev_ws.Y2, ws.Y2);
                var area = new Cell(x1_area, y1_area, x2_area, y2_area);

                var separating_elements = imageSegment.Elements.Where(el => el.X1 >= area.X1 && el.X2 <= area.X2
                    && el.Y1 >= area.Y1 && el.Y2 <= area.Y2).ToList();

                if (separating_elements.Count > 0)
                {
                    line_ws_groups.Add(new List<Cell>());
                }
                line_ws_groups.Last().Add(ws);
            }

            var final_ws = line_ws_groups.Select(cl =>
            {
                var tallestWhitespaces = cl.Where(ws => ws.Height == cl.Max(w => w.Height)).ToList();
                return tallestWhitespaces.OrderBy(w => w.Area).Last();
            }).ToList();

            return final_ws;
        }

        private static List<List<Cell>> IdentifyColumnGroups(ImageSegment imageSegment, List<Cell> verticalWS)
        {
            var middle_ws = verticalWS.Where(ws => !new HashSet<int> { ws.X1, ws.X2 }.Intersect(new HashSet<int> { imageSegment.X1, imageSegment.X2 }).Any()).ToList();
            var edge_ws = verticalWS.Where(ws => new HashSet<int> { ws.X1, ws.X2 }.Intersect(new HashSet<int> { imageSegment.X1, imageSegment.X2 }).Any()).ToList();

            Func<Cell, Cell, bool> top_matches = (col_1, col_2) => Math.Abs(col_1.Y1 - col_2.Y1) / Math.Max(col_1.Height, col_2.Height) <= 0.05;
            Func<Cell, Cell, bool> bottom_matches = (col_1, col_2) => Math.Abs(col_1.Y2 - col_2.Y2) / Math.Max(col_1.Height, col_2.Height) <= 0.05;

            var top_col_groups = TableObjectCluster.ClusterItems(middle_ws, top_matches).Select(cl => cl.Concat(edge_ws).ToList()).ToList();
            var bottom_col_groups = TableObjectCluster.ClusterItems(middle_ws, bottom_matches).Select(cl => cl.Concat(edge_ws).ToList()).ToList();

            var col_groups = top_col_groups.Concat(bottom_col_groups).Where(gp => IsColumnSection(gp)).OrderByDescending(gp => gp.Count).ToList();

            var filtered_col_groups = new List<List<Cell>>();
            foreach (var col_gp in col_groups)
            {
                int y_min = col_gp.Min(ws => ws.Y1);
                int y_max = col_gp.Max(ws => ws.Y2);
                var matching_ws = verticalWS.Where(ws => Math.Min(ws.Y2, y_max) - Math.Max(ws.Y1, y_min) > 0.2 * ws.Height
                    && !new HashSet<int> { ws.X1, ws.X2 }.Intersect(new HashSet<int> { imageSegment.X1, imageSegment.X2 }).Any()).ToList();
                if (!matching_ws.Except(col_gp).Any())
                {
                    filtered_col_groups.Add(col_gp);
                }
            }

            if (filtered_col_groups.Count == 0)
            {
                return new List<List<Cell>>();
            }

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

        private static bool IsColumnSection(List<Cell> wsGroup)
        {
            if (wsGroup.Count < 3 || wsGroup.Count > 4)
            {
                return false;
            }

            wsGroup = wsGroup.OrderBy(ws => ws.X1 + ws.X2).ToList();
            List<int> col_widths = new List<int>();
            for (int i = 0; i < wsGroup.Count - 1; i++)
            {
                int width = wsGroup[i + 1].X1 - wsGroup[i].X2;
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
