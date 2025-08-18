using OpenCvSharp;
using System.Drawing;

namespace img2table.sharp.Img2table.Sharp.Tabular.TableImage
{
    public struct TextRect
    {
        public Rect Rect { get; }
        public string Text { get; set; }

        public TextRect(Rect rect, string text)
        {
            Rect = rect;
            Text = text;
        }

        public TextRect(RectangleF rect, string text)
        {
            Rect = new Rect((int)(rect.X + 0.5), (int)(rect.Y + 0.5), (int)(rect.Width + 0.5), (int)(rect.Height + 0.5));
            Text = text;
        }

        public int X => Rect.X;
        public int Y => Rect.Y;
        public int Width => Rect.Width;
        public int Height => Rect.Height;
        public int Right => Rect.Right;
        public int Bottom => Rect.Bottom;
        public int Top => Rect.Top;
        public int Left => Rect.Left;


        public static implicit operator Rect(TextRect rwt) => rwt.Rect;

        public static implicit operator TextRect(Rect rect) => new TextRect(rect, string.Empty);
    }
}
