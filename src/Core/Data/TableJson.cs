using Img2table.Sharp.Core.Tabular.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace img2table.sharp.Core.Data
{
    public class TableJson
    {
        public static string Serialize(List<Table> tables)
        {
            var dtoTables = tables.Select(t => new TableDTO(t)).ToList();
            return JsonSerializer.Serialize(dtoTables);
        }

        public static List<Table> Deserialize(string json)
        {
            var dtoTables = JsonSerializer.Deserialize<List<TableDTO>>(json);
            return dtoTables.Select(dto => dto.ToTable()).ToList();
        }
    }
}
