using Img2table.Sharp.Tabular.TableElement;

namespace Img2table.Sharp.Tabular.Processing.BorderedTables.Layout
{
    public class CellDetector
    {
        public static List<Cell> DetectCells(List<Line> horizontalLines, List<Line> verticalLines)
        {
            List<Cell> cells = Identification.GetCellsDataframe(horizontalLines, verticalLines);

            List<Cell> dedupCells = Deduplication.DeduplicateCells(cells);
            return dedupCells;
        }
    }
}
