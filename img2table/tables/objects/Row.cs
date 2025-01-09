using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.objects
{
    public class Row : TableObject
    {
        private List<Cell> _items;

        public Row(List<Cell> cells)
            : base(cells.Min(c => c.X1), cells.Min(c => c.Y1), cells.Max(c => c.X2), cells.Max(c => c.Y2))
        {
            _items = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public List<Cell> Items => _items;
        public int NbColumns => _items.Count;

        public bool VConsistent => _items.All(c => c.Y1 == Y1 && c.Y2 == Y2);

        public Row AddCells(List<Cell> cells)
        {
            _items.AddRange(cells);
            return this;
        }

        public List<Row> SplitInRows(List<int> verticalDelimiters)
        {
            var rowDelimiters = new List<int> { Y1 }.Concat(verticalDelimiters).Concat(new List<int> { Y2 }).ToList();
            var rowBoundaries = rowDelimiters.Zip(rowDelimiters.Skip(1), (i, j) => new { i, j }).ToList();

            var newRows = new List<Row>();
            foreach (var boundary in rowBoundaries)
            {
                var cells = _items.Select(cell =>
                {
                    var newCell = new Cell(cell.X1, boundary.i, cell.X2, boundary.j);
                    return newCell;
                }).ToList();
                newRows.Add(new Row(cells));
            }

            return newRows;
        }

        public override string ToString()
        {
            return $"Row(NbColumns: {NbColumns})";
        }
    }
}
