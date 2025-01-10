namespace Img2table.Sharp.Tabular.TableElement
{
    public class Extraction
    {
        public class BBox
        {
            public int X1 { get; }
            public int Y1 { get; }
            public int X2 { get; }
            public int Y2 { get; }

            public BBox(int x1, int y1, int x2, int y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }
        }

        public class TableCell
        {
            public BBox BBox { get; }
            public string Value { get; }

            public TableCell(BBox bbox, string value)
            {
                BBox = bbox;
                Value = value;
            }
        }
    }
}
