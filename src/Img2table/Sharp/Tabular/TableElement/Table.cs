namespace Img2table.Sharp.Tabular.TableElement
{
    public class Table : TableObject
    {
        private List<Row> _items;
        private string _title;
        private bool _borderless;

        public Table(List<Row> rows, bool borderless = false)
            : base(rows == null || rows.Count == 0 ? 0 : rows.Min(r => r.X1),
                  rows == null || rows.Count == 0 ? 0 : rows.Min(r => r.Y1),
                  rows == null || rows.Count == 0 ? 0 : rows.Max(r => r.X2),
                  rows == null || rows.Count == 0 ? 0 : rows.Max(r => r.Y2))
        {
            _items = rows ?? new List<Row>();
            _title = null;
            _borderless = borderless;
        }

        public List<Row> Items => _items;
        public string Title => _title;
        public bool Borderless => _borderless;

        public void SetTitle(string title)
        {
            _title = title;
        }

        public int NbRows => _items.Count;
        public int NbColumns => _items.FirstOrDefault()?.NbColumns ?? 0;

        public Cell Cell => new Cell(X1, Y1, X2, Y2);

        public List<Line> Lines
        {
            get
            {
                var hLines = new List<Line>();
                var vLines = new List<Line>();

                foreach (var cell in _items.SelectMany(row => row.Items))
                {
                    vLines.Add(new Line(cell.X1, cell.Y1, cell.X1, cell.Y2));
                    vLines.Add(new Line(cell.X2, cell.Y1, cell.X2, cell.Y2));

                    hLines.Add(new Line(cell.X1, cell.Y1, cell.X2, cell.Y1));
                    hLines.Add(new Line(cell.X1, cell.Y2, cell.X2, cell.Y2));
                }

                var vLinesGroups = vLines
                    .OrderBy(l => l.X1)
                    .ThenBy(l => l.Y1)
                    .GroupBy(l => l.X1)
                    .Select(g => new Line(g.Min(l => l.X1), g.Min(l => l.Y1), g.Max(l => l.X2), g.Max(l => l.Y2)))
                    .ToList();

                var hLinesGroups = hLines
                    .OrderBy(l => l.Y1)
                    .ThenBy(l => l.X1)
                    .GroupBy(l => l.Y1)
                    .Select(g => new Line(g.Min(l => l.X1), g.Min(l => l.Y1), g.Max(l => l.X2), g.Max(l => l.Y2)))
                    .ToList();

                return vLinesGroups.Concat(hLinesGroups).ToList();
            }
        }

        public void RemoveRows(List<int> rowIds)
        {
            var remainingRows = Enumerable.Range(0, NbRows).Except(rowIds).ToList();

            if (remainingRows.Count > 1)
            {
                var gaps = remainingRows.Zip(remainingRows.Skip(1), (idRow, idNext) => new { idRow, idNext })
                    .Where(pair => pair.idNext - pair.idRow > 1)
                    .ToList();

                foreach (var gap in gaps)
                {
                    int yGap = (int)Math.Round((Items[gap.idRow].Y2 + Items[gap.idNext].Y1) / 2.0);

                    foreach (var cell in Items[gap.idRow].Items)
                    {
                        cell.Y2 = Math.Max(cell.Y2, yGap);
                    }
                    foreach (var cell in Items[gap.idNext].Items)
                    {
                        cell.Y1 = Math.Min(cell.Y1, yGap);
                    }
                }
            }

            foreach (var idx in rowIds.OrderByDescending(id => id))
            {
                _items.RemoveAt(idx);
            }
        }

        public void RemoveColumns(List<int> colIds)
        {
            var remainingCols = Enumerable.Range(0, NbColumns).Except(colIds).ToList();

            if (remainingCols.Count > 1)
            {
                var gaps = remainingCols.Zip(remainingCols.Skip(1), (idCol, idNext) => new { idCol, idNext })
                    .Where(pair => pair.idNext - pair.idCol > 1)
                    .ToList();

                foreach (var gap in gaps)
                {
                    int xGap = (int)Math.Round(Items.Average(row => (row.Items[gap.idCol].X2 + row.Items[gap.idNext].X1) / 2.0));

                    foreach (var row in Items)
                    {
                        row.Items[gap.idCol].X2 = Math.Max(row.Items[gap.idCol].X2, xGap);
                        row.Items[gap.idNext].X1 = Math.Min(row.Items[gap.idNext].X1, xGap);
                    }
                }
            }

            foreach (var idx in colIds.OrderByDescending(id => id))
            {
                foreach (var row in Items)
                {
                    row.Items.RemoveAt(idx);
                }
            }
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Table Title: {Title ?? "Untitled"}");
            sb.AppendLine($"Number of Rows: {NbRows}");
            sb.AppendLine($"Number of Columns: {NbColumns}");
            sb.AppendLine($"Borderless: {_borderless}");
            sb.AppendLine("Rows:");

            for (int i = 0; i < NbRows; i++)
            {
                sb.AppendLine($"  Row {i + 1}:");
                for (int j = 0; j < NbColumns; j++)
                {
                    var cell = _items[i].Items[j];
                    sb.AppendLine($"    Cell[{i},{j}] - X1: {cell.X1}, Y1: {cell.Y1}, X2: {cell.X2}, Y2: {cell.Y2}");
                }
            }

            return sb.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is Table other)
            {
                return _borderless == other._borderless &&
                       _title == other._title &&
                       _items.SequenceEqual(other._items);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_borderless, _title, _items);
        }
    }
}
