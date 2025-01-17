using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Data
{
    public class TableMarkdown
    {
        public static void Generate(PagedTableDTO pageTableDto, string outputFile)
        {
            var tables = pageTableDto.Tables;
            var sb = new StringBuilder();

            foreach (var table in tables)
            {
                if (!string.IsNullOrEmpty(table.Title))
                {
                    sb.AppendLine($"# {table.Title}");
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine("##");
                }

                if (table.Items.Count > 0)
                {
                    // Add header row
                    var headerRow = table.Items.First();
                    WriteTableRow(sb, string.Join(" | ", headerRow.Items.Select(c => c.Content)));
                    WriteTableRow(sb, string.Join(" | ", headerRow.Items.Select(c => "---")));

                    // Add data rows
                    foreach (var row in table.Items.Skip(1))
                    {
                        WriteTableRow(sb, string.Join(" | ", row.Items.Select(c => c.Content)));
                    }
                }
            }

            File.WriteAllText(outputFile, sb.ToString());
        }

        private static void WriteTableRow(StringBuilder buf, string rowText) 
        {
            buf.Append(" | ");
            buf.Append(rowText);
            buf.Append(" | ");
            buf.AppendLine();
        }
    }
}
