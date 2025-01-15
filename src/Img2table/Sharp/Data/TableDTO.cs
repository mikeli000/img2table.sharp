using Img2table.Sharp.Tabular.TableImage.TableElement;

namespace Img2table.Sharp.Data
{
    public class TableDTO
    {
        public List<RowDTO> Items { get; set; }
        public string Title { get; set; }
        public bool Borderless { get; set; }

        public TableDTO() { }

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

        public RowDTO() { }

        public RowDTO(Row row)
        {
            Items = row.Items.Select(c => new CellDTO(c)).ToList();
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

        public CellDTO() { }

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
