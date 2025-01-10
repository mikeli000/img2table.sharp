using Img2table.Sharp.Tabular.TableElement;
using OpenCvSharp;
using static Img2table.Sharp.Tabular.Processing.BorderlessTables.TableImageStructure;

namespace Img2table.Sharp.Tabular.Processing.BorderlessTables.Layout
{
    public class ImageLayout
    {
        public static List<TableSegment> SegmentImage(Mat thresh, List<Line> lines, double charLength, double medianLineSep, List<Table> existingTables = null)
        {
            var textThresh = RLSA.IdentifyTextMask(thresh, lines, charLength, existingTables);

            List<Cell> imgElements = ImageElements.GetImageElements(textThresh, charLength, medianLineSep);
            if (imgElements.Count == 0)
            {
                return new List<TableSegment>();
            }

            int minY = imgElements.Min(el => el.Y1);
            int maxY = imgElements.Max(el => el.Y2);
            ImageSegment imageSegment = new ImageSegment(0, minY, thresh.Cols, maxY, imgElements);

            var colSegments = ColumnSegments.SegmentImageColumns(imageSegment, charLength, lines);
            List<TableSegment> tbSegments = new List<TableSegment>();
            foreach (var col_segment in colSegments)
            {
                tbSegments.AddRange(TableSegments.GetTableSegments(col_segment, charLength, medianLineSep));
            }

            return tbSegments;
        }
    }
}
