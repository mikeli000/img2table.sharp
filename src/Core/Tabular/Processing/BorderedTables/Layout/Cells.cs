using Img2table.Sharp.Core.Tabular.Object;
using static Img2table.Sharp.Core.Tabular.Object.Objects;

namespace img2table.sharp.Core.Tabular.Processing.BorderedTables.Layout
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
