using img2table.sharp.img2table.tables.objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.processing.bordered_tables.tables
{
    public class CellClustering
    {
        public static List<List<Cell>> cluster_cells_in_tables(List<Cell> cells)
        {
            // 获取相邻单元格对
            List<HashSet<int>> adjacentCells = GetAdjacentCells(cells);

            // 根据相邻单元格对创建聚类
            List<HashSet<int>> clusters = FindComponents(adjacentCells);

            // 返回单元格对象列表
            List<List<Cell>> listTableCells = clusters.Select(cluster => cluster.Select(idx => cells[idx]).ToList()).ToList();

            return listTableCells;
        }

        static List<HashSet<int>> GetAdjacentCells(List<Cell> cells)
        {
            if (cells.Count == 0)
            {
                return new List<HashSet<int>>();
            }

            var dfCells = cells.Select((c, idx) => new
            {
                idx,
                c.X1,
                c.Y1,
                c.X2,
                c.Y2,
                Height = c.Height,
                Width = c.Width
            }).ToList();

            var adjacentCells = new List<HashSet<int>>();

            for (int i = 0; i < dfCells.Count; i++)
            {
                for (int j = 0; j < dfCells.Count; j++)
                {
                    if (i == j) continue;

                    var cell1 = dfCells[i];
                    var cell2 = dfCells[j];

                    // 计算水平和垂直重叠
                    int xOverlap = Math.Min(cell1.X2, cell2.X2) - Math.Max(cell1.X1, cell2.X1);
                    int yOverlap = Math.Min(cell1.Y2, cell2.Y2) - Math.Max(cell1.Y1, cell2.Y1);

                    // 计算水平和垂直差异
                    int diffX = new[] { Math.Abs(cell1.X1 - cell2.X1), Math.Abs(cell1.X1 - cell2.X2), Math.Abs(cell1.X2 - cell2.X1), Math.Abs(cell1.X2 - cell2.X2) }.Min();
                    int diffY = new[] { Math.Abs(cell1.Y1 - cell2.Y1), Math.Abs(cell1.Y1 - cell2.Y2), Math.Abs(cell1.Y2 - cell2.Y1), Math.Abs(cell1.Y2 - cell2.Y2) }.Min();

                    // 计算水平和垂直差异的阈值
                    int threshX = Math.Min(5, (int)(0.05 * Math.Min(cell1.Width, cell2.Width)));
                    int threshY = Math.Min(5, (int)(0.05 * Math.Min(cell1.Height, cell2.Height)));

                    // 过滤相邻单元格
                    if ((yOverlap > 5 && diffX <= threshX) || (xOverlap > 5 && diffY <= threshY))
                    {
                        adjacentCells.Add(new HashSet<int> { cell1.idx, cell2.idx });
                    }
                }
            }

            return adjacentCells;
        }

        static List<HashSet<int>> FindComponents(List<HashSet<int>> edges)
        {
            var parent = new Dictionary<int, int>();

            int Find(int x)
            {
                if (!parent.ContainsKey(x))
                {
                    parent[x] = x;
                }
                if (parent[x] != x)
                {
                    parent[x] = Find(parent[x]);
                }
                return parent[x];
            }

            void Union(int x, int y)
            {
                int rootX = Find(x);
                int rootY = Find(y);
                if (rootX != rootY)
                {
                    parent[rootX] = rootY;
                }
            }

            foreach (var edge in edges)
            {
                var enumerator = edge.GetEnumerator();
                enumerator.MoveNext();
                int first = enumerator.Current;
                while (enumerator.MoveNext())
                {
                    Union(first, enumerator.Current);
                }
            }

            var components = new Dictionary<int, HashSet<int>>();
            foreach (var node in parent.Keys)
            {
                int root = Find(node);
                if (!components.ContainsKey(root))
                {
                    components[root] = new HashSet<int>();
                }
                components[root].Add(node);
            }

            return components.Values.ToList();
        }
    }
}
