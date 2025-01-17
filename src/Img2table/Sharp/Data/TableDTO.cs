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
            Items = table.Items.Select(r => new RowDTO(r)).ToList();
            Title = table.Title;
            Borderless = table.Borderless;
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

        public RowDTO(Row row)
        {
            Items = new List<CellDTO>();

            Cell prev = null;
            CellDTO prevDTO = null;
            for (var i = 0; i < row.Cells.Count; i++)
            {
                var curr = row.Cells[i];
                var currDTO = new CellDTO(curr);
                if (curr.Equals(prev))
                {
                    prevDTO.ColSpan++;
                }
                else
                {
                    prev = curr;
                    prevDTO = currDTO;
                }

                Items.Add(currDTO);
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

        private int RowSpan { get; set; } = 1;
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
    }

}
