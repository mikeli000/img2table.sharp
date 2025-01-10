namespace Img2table.Sharp.Tabular.TableElement
{
    public class Cell : TableObject
    {
        public string Content { get; }

        public Cell(int x1, int y1, int x2, int y2, string content = null)
            : base(x1, y1, x2, y2)
        {
            Content = content;
        }

        public Extraction.TableCell TableCell
        {
            get
            {
                Extraction.BBox bbox = new Extraction.BBox(X1, Y1, X2, Y2);
                return new Extraction.TableCell(bbox, Content);
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X1, Y1, X2, Y2, Content);
        }

        public override bool Equals(object? obj)
        {
            if (obj is Cell other)
            {
                return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
            }
            return false;
        }


        public override string ToString()
        {
            return $"Cell(X1: {X1}, Y1: {Y1}, X2: {X2}, Y2: {Y2}, Content: {Content})";
        }
    }
}
