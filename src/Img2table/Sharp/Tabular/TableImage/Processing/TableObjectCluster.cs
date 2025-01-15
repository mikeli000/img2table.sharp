namespace Img2table.Sharp.Tabular.TableImage.Processing
{
    public class TableObjectCluster
    {
        public static List<List<T>> ClusterItems<T>(List<T> items, Func<T, T, bool> clusteringFunc)
        {
            List<HashSet<int>> clusters = new List<HashSet<int>>();
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i; j < items.Count; j++)
                {
                    bool corresponds = clusteringFunc(items[i], items[j]) || items[i].Equals(items[j]);

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
