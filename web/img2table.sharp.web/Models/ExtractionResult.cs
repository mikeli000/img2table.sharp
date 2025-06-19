using System.Collections.Generic;

namespace img2table.sharp.web.Models
{
    public class ExtractionResult
    {
        public List<string> Titles { get; set; } = new();
        public List<string> Lists { get; set; } = new();
        public List<TableInfo> Tables { get; set; } = new();
    }

    public class TableInfo
    {
        public int TableId { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
