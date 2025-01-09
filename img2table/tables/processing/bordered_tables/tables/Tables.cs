using img2table.sharp.img2table.tables.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static img2table.sharp.img2table.tables.objects.Objects;

namespace img2table.sharp.img2table.tables.processing.bordered_tables.tables
{
    public class Tables
    {
        public static List<Table> get_tables(List<Cell> cells, List<Cell> elements, List<Line> lines, double charLength)
        {
            // Cluster cells into tables
            List<List<Cell>> list_cluster_cells = CellClustering.cluster_cells_in_tables(cells);

            // Normalize cells in clusters
            List<List<Cell>> clusters_normalized = NormalizeClusters(list_cluster_cells);
            List<List<Cell>> complete_clusters = AddSemiBorderedCellsToClusters(clusters_normalized, lines, charLength);

            // Create tables from cells clusters
            List<Table> tables = complete_clusters.Select(cluster => TableCreation.cluster_to_table(cluster, elements)).ToList();

            return tables.Where(tb => tb.NbRows * tb.NbColumns >= 2).ToList();
        }

        static List<List<Cell>> NormalizeClusters(List<List<Cell>> listClusterCells)
        {
            return listClusterCells.Select(clusterCells => TableCreation.normalize_table_cells(clusterCells)).ToList();
        }

        static List<List<Cell>> AddSemiBorderedCellsToClusters(List<List<Cell>> clustersNormalized, List<Line> lines, double charLength)
        {
            return clustersNormalized
                .Where(cluster => cluster.Count > 0)
                .Select(cluster => SemiBordered.add_semi_bordered_cells(cluster, lines, charLength))
                .ToList();
        }


    }
}
