using Img2table.Sharp.Tabular.TableElement;
using OpenCvSharp;

namespace Img2table.Sharp.Tabular.Processing.BorderlessTables.Layout
{
    public class ImageElements
    {
        public static List<Cell> GetImageElements(Mat thresh, double char_length, double median_line_sep)
        {
            Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(thresh, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            List<Cell> elements = new List<Cell>();
            foreach (var contour in contours)
            {
                Rect rect = Cv2.BoundingRect(contour);
                int x = rect.X;
                int y = rect.Y;
                int w = rect.Width;
                int h = rect.Height;

                if (Math.Min(h, w) >= 0.5 * char_length && Math.Max(h, w) >= char_length
                    || w / (double)h >= 2 && 0.5 * char_length <= w && w <= 1.5 * char_length)
                {
                    elements.Add(new Cell(x, y, x + w, y + h));
                }
            }

            return elements;
        }
    }
}
