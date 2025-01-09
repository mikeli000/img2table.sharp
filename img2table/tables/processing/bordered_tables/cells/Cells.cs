using img2table.sharp.img2table.tables.objects;
using static img2table.sharp.img2table.tables.objects.Objects;

namespace img2table.sharp.img2table.tables.processing.bordered_tables.cells
{
    public class Cells
    {
        public static List<Cell> get_cells(List<Line> horizontalLines, List<Line> verticalLines)
        {
            // 创建包含水平和垂直线条的单元格数据框
            List<Cell> cells = Identification.get_cells_dataframe(horizontalLines, verticalLines);

            // 去重单元格
            List<Cell> dedupCells = Deduplication.deduplicate_cells(cells);
            return dedupCells;
        }
    }
}
