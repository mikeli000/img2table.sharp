using Img2table.Sharp.Tabular.TableImage.TableElement;
using System.Text.Json;

namespace Img2table.Sharp.Data
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
