using Img2table.Sharp.Core.Tables.Objects;
using static Img2table.Sharp.Core.Tables.Objects.Objects;

namespace Img2table.Sharp.Core.Tables.Processing.BorderedTables.Cells
{
    public class Cells
    {
        public static List<Cell> GetCells(List<Line> horizontalLines, List<Line> verticalLines)
        {
            List<Cell> cells = Identification.GetCellsDataframe(horizontalLines, verticalLines);

            List<Cell> dedupCells = Deduplication.DeduplicateCells(cells);
            return dedupCells;
        }
    }
}
