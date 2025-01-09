using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.objects
{
    public class Objects
    {
        public class Line : TableObject
        {
            public int? Thickness { get; }

            public Line(int x1, int y1, int x2, int y2, int? thickness = null)
                : base(x1, y1, x2, y2)
            {
                Thickness = thickness;
            }

            public double Angle
            {
                get
                {
                    double deltaX = X2 - X1;
                    double deltaY = Y2 - Y1;
                    return Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
                }
            }

            public double Length
            {
                get
                {
                    return Math.Sqrt(Height * Height + Width * Width);
                }
            }

            public bool Horizontal
            {
                get
                {
                    return Angle % 180 == 0;
                }
            }

            public bool Vertical
            {
                get
                {
                    return Angle % 180 == 90;
                }
            }

            public Dictionary<string, object> Dict
            {
                get
                {
                    return new Dictionary<string, object>
            {
                { "x1", X1 },
                { "x2", X2 },
                { "y1", Y1 },
                { "y2", Y2 },
                { "width", Width },
                { "height", Height },
                { "thickness", Thickness }
            };
                }
            }

            public Line Transpose()
            {
                return new Line(Y1, X1, Y2, X2, Thickness);
            }

            public Line Reprocess()
            {
                // 重新分配坐标
                int _x1 = Math.Min(X1, X2);
                int _x2 = Math.Max(X1, X2);
                int _y1 = Math.Min(Y1, Y2);
                int _y2 = Math.Max(Y1, Y2);
                X1 = _x1;
                X2 = _x2;
                Y1 = _y1;
                Y2 = _y2;

                // 修正“几乎”水平或垂直的行
                if (Math.Abs(Angle) <= 5)
                {
                    int yVal = (int)Math.Round((Y1 + Y2) / 2.0);
                    Y1 = Y2 = yVal;
                }
                else if (Math.Abs(Angle - 90) <= 5)
                {
                    int xVal = (int)Math.Round((X1 + X2) / 2.0);
                    X1 = X2 = xVal;
                }

                return this;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X1, Y1, X2, Y2, Thickness);
            }

            public override string ToString()
            {
                return $"Line(X1: {X1}, Y1: {Y1}, X2: {X2}, Y2: {Y2}, Thickness: {Thickness})";
            }
        }
    }
}
