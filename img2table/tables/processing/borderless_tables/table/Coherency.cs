using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.objects.Table;

namespace img2table.sharp.img2table.tables.processing.borderless_tables.table
{
    public class Coherency
    {
        public static bool check_row_coherency(objects.Table table, double median_line_sep)
        {
            if (table.NbRows < 2)
            {
                return false;
            }

            // Get median row separation
            var rowSeparations = new List<float>();
            for (int i = 0; i < table.NbRows - 1; i++)
            {
                var upperRow = table.Items[i];
                var lowerRow = table.Items[i + 1];
                float separation = (lowerRow.Items.First().Y1 + lowerRow.Items.First().Y2 - upperRow.Items.First().Y1 - upperRow.Items.First().Y2) / 2.0f;
                rowSeparations.Add(separation);
            }

            rowSeparations.Sort();
            float medianRowSeparation = rowSeparations[rowSeparations.Count / 2];

            return medianRowSeparation >= median_line_sep / 3;
        }

        public static bool check_table_coherency(objects.Table table, double medianLineSep, double charLength)
        {
            // Check row coherency of table
            bool rowCoherency = check_row_coherency(table, medianLineSep);

            // Check column coherency of table
            bool columnCoherency = check_column_coherency(table, charLength);

            return rowCoherency && columnCoherency;
        }

        public static bool check_column_coherency(objects.Table table, double charLength)
        {
            if (table.NbColumns < 2)
            {
                return false;
            }

            // Get column widths
            List<double> colWidths = new List<double>();
            for (int idx = 0; idx < table.NbColumns; idx++)
            {
                var colElements = table.Items.Select(row => row.Items[idx]).ToList();
                double colWidth = colElements.Min(el => el.X2) - colElements.Max(el => el.X1);
                colWidths.Add(colWidth);
            }

            return Utils.Median(colWidths.ToArray()) >= 3 * charLength;
        }
    }
}
