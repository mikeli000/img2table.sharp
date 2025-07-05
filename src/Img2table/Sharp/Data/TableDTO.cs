using Img2table.Sharp.Tabular.TableImage.TableElement;

namespace Img2table.Sharp.Data
{
    public class TableDTO
    {
        public List<RowDTO> Items { get; set; }
        public string Title { get; set; }
        public bool Borderless { get; set; }

        public TableDTO(Table table)
        {
            ToTableDTO(table);
            Title = table.Title;
            Borderless = table.Borderless;
        }

        private void ToTableDTO(Table table)
        {
            int n_row = table.NbRows;
            int n_col = table.NbColumns;

            Items = new List<RowDTO>();
            var rowSpanCellDict = new Dictionary<string, CellDTO>();
            for (int i = 0; i < table.Items.Count; i++)
            {
                var currRow = new RowDTO(table.Items[i], rowSpanCellDict);
                Items.Add(currRow);
            }
        }

        public Table ToTable()
        {
            var rows = Items.Select(dto => dto.ToRow()).ToList();
            var table = new Table(rows, Borderless);
            table.SetTitle(Title);
            return table;
        }
    }

    public class RowDTO
    {
        public List<CellDTO> Items { get; set; }

        public RowDTO(Row row, IDictionary<string, CellDTO> rowSpanCellDict)
        {
            var colSpanCellDict = new Dictionary<string, CellDTO>();
            Items = new List<CellDTO>();
            var temp = new Dictionary<string, CellDTO>();

            for (var i = 0; i < row.Cells.Count; i++)
            {
                var cell = row.Cells[i];
                bool isSpannedCell = false;
                if (rowSpanCellDict.TryGetValue(cell.CellKey, out var y_cell))
                {
                    y_cell.RowSpan++;
                    isSpannedCell = true;
                }

                if (colSpanCellDict.TryGetValue(cell.CellKey, out var x_cell))
                {
                    x_cell.ColSpan++;
                    isSpannedCell = true;
                }

                if (isSpannedCell)
                {
                    continue;
                }

                var currDTO = new CellDTO(cell);
                colSpanCellDict[cell.CellKey] = currDTO;
                temp[cell.CellKey] = currDTO;
                Items.Add(currDTO);
            }

            foreach (var v in temp)
            {
                if (!rowSpanCellDict.ContainsKey(v.Key))
                {
                    rowSpanCellDict[v.Key] = v.Value;
                }
            }   
        }

        public Row ToRow()
        {
            return new Row(Items.Select(dto => dto.ToCell()).ToList());
        }
    }

    public class CellDTO
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public string Content { get; set; }

        public int RowSpan { get; set; } = 1;
        public int ColSpan { get; set; } = 1;

        public CellDTO(Cell cell)
        {
            X1 = cell.X1;
            Y1 = cell.Y1;
            X2 = cell.X2;
            Y2 = cell.Y2;
            Content = cell.Content;
        }

        public Cell ToCell()
        {
            return new Cell(X1, Y1, X2, Y2, Content);
        }

        public override bool Equals(object? obj)
        {
            if (obj is CellDTO other)
            {
                return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
            }
            return false;
        }
    }

}
