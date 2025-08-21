using Img2table.Sharp.Tabular.TableImage.TableElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Img2table.Sharp.Tabular.TableImage.TableElement
{
    public class KeyValueTable : Table
    {
        public KeyValueTable(List<Row> rows, bool borderless = false) : base(rows, borderless)
        {
        }
    }
}
