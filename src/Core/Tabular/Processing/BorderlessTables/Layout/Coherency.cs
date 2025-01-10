using Img2table.Sharp.Core.Tabular;
using Img2table.Sharp.Core.Tabular.Object;

namespace img2table.sharp.Core.Tabular.Processing.BorderlessTables.Layout
{
    public class Coherency
    {
        public static bool check_table_coherency(Table table, double medianLineSep, double charLength)
        {
            bool rowCoherency = CheckRowCoherency(table, medianLineSep);
            bool columnCoherency = CheckColumnCoherency(table, charLength);

            return rowCoherency && columnCoherency;
        }

        private static bool CheckRowCoherency(Table table, double medianLineSep)
        {
            if (table.NbRows < 2)
            {
                return false;
            }

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

            return medianRowSeparation >= medianLineSep / 3;
        }

        private static bool CheckColumnCoherency(Table table, double charLength)
        {
            if (table.NbColumns < 2)
            {
                return false;
            }

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
