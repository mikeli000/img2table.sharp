using Img2table.Sharp.Data;
using Img2table.Sharp.Tabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Data
{
    public class PagedTableDTO
    {
        public List<TableDTO> Tables { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public string PageImage { get; }

        public PagedTableDTO(PagedTable pagedTable) 
        {
            PageIndex = pagedTable.PageIndex;
            PageCount = pagedTable.PageCount;
            PageImage = pagedTable.PageImage;
            
            Tables = pagedTable.Tables.Select(t => new TableDTO(t)).ToList();
        }
    }
}
