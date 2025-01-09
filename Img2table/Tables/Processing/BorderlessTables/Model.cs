using Img2table.Sharp.Img2table.Tables.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Img2table.Sharp.Img2table.Tables.Processing.BorderlessTables
{
    public class Model
    {
        public class Whitespace
        {
            public List<Cell> Cells { get; }

            public Whitespace(List<Cell> cells)
            {
                Cells = cells;
            }

            public int X1 => Cells.Min(c => c.X1);
            public int Y1 => Cells.Min(c => c.Y1);
            public int X2 => Cells.Max(c => c.X2);
            public int Y2 => Cells.Max(c => c.Y2);

            public int Width => Cells.Sum(c => c.Width);
            public int Height => Cells.Sum(c => c.Height);
            public int Area => Cells.Sum(c => c.Area);

            public bool Continuous => Cells.Count == 1;

            public Whitespace Flipped()
            {
                List<Cell> flippedCells = Cells.Select(c => new Cell(c.Y1, c.X1, c.Y2, c.X2)).ToList();
                return new Whitespace(flippedCells);
            }

            public override bool Equals(object obj)
            {
                if (obj is Whitespace other)
                {
                    return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X1, Y1, X2, Y2);
            }

            public override string ToString()
            {
                return $"Whitespace(Cells: [{string.Join(", ", Cells)}])";
            }

            public static implicit operator Cell(Whitespace ws)
            {
                return new Cell(ws.X1, ws.Y1, ws.X2, ws.Y2);
            }
        }

        public class ImageSegment
        {
            public int X1 { get; }
            public int Y1 { get; }
            public int X2 { get; }
            public int Y2 { get; }
            public List<Cell> Elements { get; private set; }
            public List<Whitespace> Whitespaces { get; private set; }
            public int? Position { get; set; }

            public ImageSegment(int x1, int y1, int x2, int y2, List<Cell> elements = null, List<Whitespace> whitespaces = null, int? position = null)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                Elements = elements ?? new List<Cell>();
                Whitespaces = whitespaces ?? new List<Whitespace>();
                Position = position;
            }

            public int Width => X2 - X1;
            public int Height => Y2 - Y1;

            public int ElementHeight => Elements.Any() ? Elements.Max(el => el.Y2) - Elements.Min(el => el.Y1) : Height;

            public void SetElements(List<Cell> elements)
            {
                Elements = elements;
            }

            public void SetWhitespaces(List<Whitespace> whitespaces)
            {
                Whitespaces = whitespaces;
            }

            public override bool Equals(object obj)
            {
                if (obj is ImageSegment other)
                {
                    return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X1, Y1, X2, Y2);
            }

            public override string ToString()
            {
                return $"ImageSegment(X1: {X1}, Y1: {Y1}, X2: {X2}, Y2: {Y2}, Elements: [{string.Join(", ", Elements)}], Whitespaces: [{string.Join(", ", Whitespaces)}], Position: {Position})";
            }
        }

        public class TableSegment
        {
            public List<ImageSegment> TableAreas { get; private set; }

            public TableSegment(List<ImageSegment> tableAreas)
            {
                TableAreas = tableAreas;
            }

            public int X1 => TableAreas.Min(tb_area => tb_area.X1);
            public int Y1 => TableAreas.Min(tb_area => tb_area.Y1);
            public int X2 => TableAreas.Max(tb_area => tb_area.X2);
            public int Y2 => TableAreas.Max(tb_area => tb_area.Y2);

            public List<Cell> Elements => TableAreas.SelectMany(tb_area => tb_area.Elements).ToList();

            public List<Whitespace> Whitespaces => TableAreas.SelectMany(tb_area => tb_area.Whitespaces).ToList();

            public override string ToString()
            {
                return $"TableSegment(X1: {X1}, Y1: {Y1}, X2: {X2}, Y2: {Y2}, Elements: [{string.Join(", ", Elements)}])";
            }

        }

        public class VerticalWS
        {
            public Whitespace Ws { get; set; }
            public int Position { get; set; } = 0;
            public bool Top { get; set; } = true;
            public bool Bottom { get; set; } = true;
            public bool Used { get; set; } = false;

            public VerticalWS(Whitespace ws, int position = 0, bool top = true, bool bottom = true, bool used = false)
            {
                Ws = ws;
                Position = position;
                Top = top;
                Bottom = bottom;
                Used = used;
            }

            public int X1 => Ws.X1;
            public int Y1 => Ws.Y1;
            public int X2 => Ws.X2;
            public int Y2 => Ws.Y2;
            public int Width => Ws.X2 - Ws.X1;
            public int Height => Ws.Y2 - Ws.Y1;
            public bool Continuous => Ws.Continuous;

            public override string ToString()
            {
                return $"VerticalWS(Ws: {Ws}, Position: {Position}, Top: {Top}, Bottom: {Bottom}, Used: {Used})";
            }
        }

        public class Column
        {
            public List<VerticalWS> Whitespaces { get; set; }
            public bool Top { get; set; } = true;
            public bool Bottom { get; set; } = true;
            public int TopPosition { get; set; } = 0;
            public int BottomPosition { get; set; } = 0;

            public Column(List<VerticalWS> whitespaces, bool top = true, bool bottom = true, int topPosition = 0, int bottomPosition = 0)
            {
                Whitespaces = whitespaces;
                Top = top;
                Bottom = bottom;
                TopPosition = topPosition;
                BottomPosition = bottomPosition;
            }

            public int X1 => Whitespaces.Max(v_ws => v_ws.Ws.X1);
            public int Y1 => Whitespaces.Min(v_ws => v_ws.Ws.Y1);
            public int X2 => Whitespaces.Min(v_ws => v_ws.Ws.X2);
            public int Y2 => Whitespaces.Max(v_ws => v_ws.Ws.Y2);

            public int Height
            {
                get
                {
                    var yValues = new HashSet<int>(Whitespaces.SelectMany(v_ws => v_ws.Ws.Cells.SelectMany(c => Enumerable.Range(c.Y1, c.Y2 - c.Y1 + 1))));
                    return yValues.Count - 1;
                }
            }

            public bool Continuous => Whitespaces.All(v_ws => v_ws.Continuous);

            public static Column FromWs(VerticalWS v_ws)
            {
                return new Column(new List<VerticalWS> { v_ws }, v_ws.Top, v_ws.Bottom, v_ws.Position, v_ws.Position);
            }

            public bool Corresponds(VerticalWS v_ws, double charLength)
            {
                //if (BottomPosition == 0)
                //{
                //    return true;
                //}
                if (v_ws.Position != BottomPosition + 1)
                {
                    return false;
                }
                else if (!Bottom || !v_ws.Top)
                {
                    return false;
                }

                // Condition on position
                return Math.Min(X2, v_ws.X2) - Math.Max(X1, v_ws.X1) >= 0.5 * charLength;
            }

            public void Add(VerticalWS v_ws)
            {
                Whitespaces.Add(v_ws);
                TopPosition = Math.Min(TopPosition, v_ws.Position);
                BottomPosition = Math.Max(BottomPosition, v_ws.Position);

                if (v_ws.Position == TopPosition)
                {
                    Top = v_ws.Top;
                }

                if (v_ws.Position == BottomPosition)
                {
                    Bottom = v_ws.Bottom;
                }
            }

            public override string ToString()
            {
                return $"Column(Whitespaces: [{string.Join(", ", Whitespaces)}], Top: {Top}, Bottom: {Bottom}, TopPosition: {TopPosition}, BottomPosition: {BottomPosition})";
            }
        }

        public class ColumnGroup
        {
            public List<Column> Columns { get; set; }
            public double CharLength { get; set; }
            public List<Cell> Elements { get; set; }

            public ColumnGroup(List<Column> columns, double charLength, List<Cell> elements = null)
            {
                Columns = columns;
                CharLength = charLength;
                Elements = elements ?? new List<Cell>();
                ReprocessColumns();
            }

            private void ReprocessColumns()
            {
                Columns = Columns.OrderBy(col => col.X1).ToList();

                if (Columns.Count >= 2 && Elements.Count > 0)
                {
                    int xLeft = Elements.Min(el => el.X1);
                    int xRight = Elements.Max(el => el.X2);

                    Columns[0] = new Column(Columns[0].Whitespaces.Select(v_ws => new VerticalWS(
                        new Whitespace(new List<Cell> { new Cell(xLeft - (int)(0.5 * CharLength), v_ws.Ws.Y1, xLeft - (int)(0.5 * CharLength), v_ws.Ws.Y2) }),
                        v_ws.Position, v_ws.Top, v_ws.Bottom)).ToList());

                    Columns[Columns.Count - 1] = new Column(Columns[Columns.Count - 1].Whitespaces.Select(v_ws => new VerticalWS(
                        new Whitespace(new List<Cell> { new Cell(xRight + (int)(0.5 * CharLength), v_ws.Ws.Y1, xRight + (int)(0.5 * CharLength), v_ws.Ws.Y2) }),
                        v_ws.Position, v_ws.Top, v_ws.Bottom)).ToList());
                }
            }

            public int X1 => Columns.Any() ? Columns.Min(d => d.X1) : 0;
            public int Y1 => Columns.Any() ? Columns.Min(d => d.Y1) : 0;
            public int X2 => Columns.Any() ? Columns.Max(d => d.X2) : 0;
            public int Y2 => Columns.Any() ? Columns.Max(d => d.Y2) : 0;

            public Cell Bbox => new Cell(X1, Y1, X2, Y2);

            public int Height => Y2 - Y1;
            public int Width => X2 - X1;
            public int Area => (X2 - X1) * (Y2 - Y1);

            public override bool Equals(object obj)
            {
                if (obj is ColumnGroup other)
                {
                    return Columns.SequenceEqual(other.Columns) && Elements.ToHashSet().SetEquals(other.Elements);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Columns, CharLength, Elements);
            }

            public override string ToString()
            {
                return $"ColumnGroup(Columns: [{string.Join(", ", Columns)}], Elements: [{string.Join(", ", Elements)}], CharLength: {CharLength})";
            }
        }
    }
}
