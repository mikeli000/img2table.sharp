using PDFDict.SDK.Sharp.Core.Contents;
using System.Text;

namespace Img2table.Sharp.Tabular.TableImage.TableElement
{
    public class Cell : TableObject
    {
        private StringBuilder _text = new StringBuilder();
        public string Content => _text.ToString();

        public string HtmlContent { get; set; }
        public int Baseline { get; set; }

        public Cell(int x1, int y1, int x2, int y2, string content = null)
            : base(x1, y1, x2, y2)
        {
            if (!string.IsNullOrEmpty(content))
            {
                _text.Append(content);
            }
        }

        public void AddText(string appendText, bool endSpace = false, bool newline = false)
        {
            if (!string.IsNullOrEmpty(appendText))
            {
                _text.Append(appendText);
                if (endSpace)
                {
                    _text.Append(" ");
                }

                if (newline)
                {
                    _text.AppendLine();
                }
            }
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
                if (Width == 0 || Height == 0)
                {
                    return false;
                }
                return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
            }
            return false;
        }

        public override string ToString()
        {
            return $"Cell(X1: {X1}, Y1: {Y1}, X2: {X2}, Y2: {Y2}, Content: {Content})";
        }

        public string CellKey
        {
            get
            {
                return $"{X1}_{Y1}_{X2}_{Y2}";
            }
        }

        public static bool IsSpaceBetween(Cell left, Cell right, int threshold = 5)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return Math.Abs(right.X1 - left.X2) >= threshold;
        }
    }
}
