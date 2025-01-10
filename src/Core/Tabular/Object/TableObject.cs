using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Img2table.Sharp.Core.Tabular.Object
{
    public class TableObject
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }

        private int? _height;
        private int? _width;
        private int? _area;

        public TableObject(int x1, int y1, int x2, int y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public Tuple<int, int, int, int> BBox(int margin = 0, int heightMargin = 0, int widthMargin = 0)
        {
            int x1, y1, x2, y2;

            if (margin != 0)
            {
                x1 = X1 - margin;
                y1 = Y1 - margin;
                x2 = X2 + margin;
                y2 = Y2 + margin;
            }
            else
            {
                x1 = X1 - widthMargin;
                y1 = Y1 - heightMargin;
                x2 = X2 + widthMargin;
                y2 = Y2 + heightMargin;
            }

            return Tuple.Create(x1, y1, x2, y2);
        }

        public int Height
        {
            get
            {
                if (!_height.HasValue)
                {
                    _height = Y2 - Y1;
                }
                return _height.Value;
            }
        }

        public int Width
        {
            get
            {
                if (!_width.HasValue)
                {
                    _width = X2 - X1;
                }
                return _width.Value;
            }
        }

        public int Area
        {
            get
            {
                if (!_area.HasValue)
                {
                    _area = Height * Width;
                }
                return _area.Value;
            }
        }
    }
}
