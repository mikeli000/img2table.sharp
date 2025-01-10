using Img2table.Sharp.Core.Tabular.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.Core.Tabular.Processing.BorderedTables.Layout
{
    public class Consecutive
    {
        public static List<Table> MergeConsecutiveTables(List<Table> tables, List<Cell> contours)
        {
            if (tables.Count == 0)
            {
                return new List<Table>();
            }

            var sortedTables = tables.OrderBy(t => t.Y1).ToList();
            var clusters = new List<List<Table>> { new List<Table> { sortedTables[0] } };

            for (int i = 1; i < sortedTables.Count; i++)
            {
                var tb = sortedTables[i];
                var prevTable = clusters.Last().Last();

                var inBetweenContours = contours.Where(c => c.Y1 >= prevTable.Y2 && c.Y2 <= tb.Y1
                                                            && c.X2 >= Math.Min(prevTable.X1, tb.X1)
                                                            && c.X1 <= Math.Max(prevTable.X2, tb.X2)).ToList();

                var prevTbCols = prevTable.Lines.Where(l => l.Vertical).OrderBy(l => l.X1).ToList();
                var tbCols = tb.Lines.Where(l => l.Vertical).OrderBy(l => l.X1).ToList();
                bool coherencyLines = prevTbCols.Zip(tbCols, (l1, l2) => Math.Abs(l1.X1 - l2.X1) <= 2).All(x => x);

                if (!(inBetweenContours.Count == 0 && prevTable.NbColumns == tb.NbColumns && coherencyLines))
                {
                    clusters.Add(new List<Table>());
                }
                clusters.Last().Add(tb);
            }

            var mergedTables = new List<Table>();
            foreach (var cl in clusters)
            {
                if (cl.Count == 1)
                {
                    mergedTables.AddRange(cl);
                }
                else
                {
                    var newRows = cl.SelectMany(tb => tb.Items).ToList();
                    var newTable = new Table(newRows, false);
                    mergedTables.Add(newTable);
                }
            }

            return mergedTables;
        }
    }
}
