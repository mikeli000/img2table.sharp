using Img2table.Sharp.Tabular.TableImage.TableElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Img2table.Sharp.Tabular
{
    public class PagedTable
    {
        public int PageIndex { get; set; }

        public int PageCount { get; set; }

        public string PageImage { get; set; }

        public List<Table> Tables { get; set; }
    }
}
