using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.img2table.tables.processing
{
    public class Processing
    {
        public static List<List<T>> cluster_items<T>(List<T> items, Func<T, T, bool> clusteringFunc)
        {
            // 根据聚类函数创建聚类
            List<HashSet<int>> clusters = new List<HashSet<int>>();
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i; j < items.Count; j++)
                {
                    // 检查两个项目是否对应
                    bool corresponds = clusteringFunc(items[i], items[j]) || items[i].Equals(items[j]);

                    // 如果两个项目对应，找到匹配的聚类或创建一个新的聚类
                    if (corresponds)
                    {
                        var matchingClusters = clusters.Where(cl => cl.Contains(i) || cl.Contains(j)).ToList();
                        if (matchingClusters.Any())
                        {
                            var newCluster = new HashSet<int> { i, j };
                            foreach (var cl in matchingClusters)
                            {
                                newCluster.UnionWith(cl);
                            }
                            clusters = clusters.Except(matchingClusters).ToList();
                            clusters.Add(newCluster);
                        }
                        else
                        {
                            clusters.Add(new HashSet<int> { i, j });
                        }
                    }
                }
            }

            return clusters.Select(c => c.Select(idx => items[idx]).ToList()).ToList();
        }
    }
}
