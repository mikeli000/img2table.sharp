using Img2table.Sharp.Tabular.TableImage.TableElement;

namespace Img2table.Sharp.Tabular.TableImage.Processing.BorderedTables.Layout
{
    public class TableDetector
    {
        public static List<Table> DetectTables(List<Cell> cells, List<Cell> elements, List<Line> lines, double charLength)
        {
            List<List<Cell>> list_cluster_cells = CellClustering.ClusterCellsInTables(cells);

            List<List<Cell>> clusters_normalized = NormalizeClusters(list_cluster_cells);
            List<List<Cell>> complete_clusters = AddSemiBorderedCellsToClusters(clusters_normalized, lines, charLength);

            List<Table> tables = complete_clusters.Select(cluster => TableCreation.ClusterToTable(cluster, elements)).ToList();

            return tables.Where(tb => tb.NbRows * tb.NbColumns >= 2).ToList();
        }

        static List<List<Cell>> NormalizeClusters(List<List<Cell>> listClusterCells)
        {
            return listClusterCells.Select(clusterCells => TableCreation.NormalizeTableCells(clusterCells)).ToList();
        }

        static List<List<Cell>> AddSemiBorderedCellsToClusters(List<List<Cell>> clustersNormalized, List<Line> lines, double charLength)
        {
            return clustersNormalized
                .Where(cluster => cluster.Count > 0)
                .Select(cluster => SemiBordered.AddSemiBorderedCells(cluster, lines, charLength))
                .ToList();
        }
    }
}
