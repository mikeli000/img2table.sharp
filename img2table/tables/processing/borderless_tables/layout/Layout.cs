using img2table.sharp.img2table.tables.objects;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.objects.Objects;
using static img2table.sharp.img2table.tables.processing.borderless_tables.Model;

namespace img2table.sharp.img2table.tables.processing.borderless_tables.layout
{
    public class Layout
    {
        public static List<TableSegment> segment_image(Mat thresh, List<Line> lines, double char_length, double median_line_sep, List<Table> existing_tables = null)
        {
            // Identify text mask
            var text_thresh = RLSA.identify_text_mask(thresh, lines, char_length, median_line_sep, existing_tables);

            // Identify image elements
            List<Cell> img_elements = ImageElements.get_image_elements(text_thresh, char_length, median_line_sep);
            if (img_elements.Count == 0)
            {
                return new List<TableSegment>();
            }
            
            // Identify column segments
            int y_min = img_elements.Min(el => el.Y1);
            int y_max = img_elements.Max(el => el.Y2);
            ImageSegment image_segment = new ImageSegment(0, y_min, thresh.Cols, y_max, img_elements);

            var col_segments = ColumnSegments.segment_image_columns(image_segment, char_length, lines);

            // Within each column, identify segments that can correspond to tables
            List<TableSegment> tb_segments = new List<TableSegment>();
            foreach (var col_segment in col_segments)
            {
                tb_segments.AddRange(TableSegments.get_table_segments(col_segment, char_length, median_line_sep));
            }

            return tb_segments;
        }
    }
}
