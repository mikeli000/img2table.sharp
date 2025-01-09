using Img2table.Sharp.Img2table.Tables.Objects;
using static Img2table.Sharp.Img2table.Tables.Objects.Objects;

namespace Img2table.Sharp.Img2table.Tables.Processing.BorderedTables.Tables
{
    public class Tables
    {
        public static List<Table> GetTables(List<Cell> cells, List<Cell> elements, List<Line> lines, double charLength)
        {
            // Cluster cells into tables
            List<List<Cell>> list_cluster_cells = CellClustering.ClusterCellsInTables(cells);

            // Normalize cells in clusters
            List<List<Cell>> clusters_normalized = NormalizeClusters(list_cluster_cells);
            List<List<Cell>> complete_clusters = AddSemiBorderedCellsToClusters(clusters_normalized, lines, charLength);

            // Create tables from cells clusters
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
