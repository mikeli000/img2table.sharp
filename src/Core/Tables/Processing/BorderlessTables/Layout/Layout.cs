using Img2table.Sharp.Core.Tables.Objects;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Img2table.Sharp.Core.Tables.Objects.Objects;
using static Img2table.Sharp.Core.Tables.Processing.BorderlessTables.Model;

namespace Img2table.Sharp.Core.Tables.Processing.BorderlessTables.layout
{
    public class Layout
    {
        public static List<TableSegment> SegmentImage(Mat thresh, List<Line> lines, double charLength, double medianLineSep, List<Objects.Table> existingTables = null)
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
