using img2table.sharp.web.Models;
using System.Collections.Generic;
using System;
using System.Linq;

namespace img2table.sharp.web.Services
{
    public class Columnizer
    {
        public static List<ChunkObject> SortByColumns(List<ChunkObject> body)
        {
            if (body == null || body.Count == 0)
            {
                return body;
            }

            var sorted = new List<ChunkObject>();
            var anchoredChunkObject = body.Select(b => new AnchoredChunkObject { Obj = b }).ToList();
            var lines = SegmentLines(anchoredChunkObject);
            foreach (var line in lines)
            {
                SortLineSegByColumns(line);
                sorted.AddRange(line.ChunkObjects);
            }
            return sorted;
        }

        private static List<LineSeg> SegmentLines(List<AnchoredChunkObject> body, float overlapThreshold = 2f)
        {
            var sorted = body.OrderBy(b => b.Obj.Y0).ToList();
            var lineSegs = new List<LineSeg>();
            for (int i = 0; i < sorted.Count; i++)
            {
                var curr = sorted[i];
                if (curr.Used) 
                {
                    continue;
                }
                
                var overlapped = new List<AnchoredChunkObject>();
                curr.Used = true;
                overlapped.Add(curr);
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (sorted[j].Used) 
                    { 
                        continue; 
                    }

                    var next = sorted[j];
                    if (IsYOverlap(curr.Obj, next.Obj, overlapThreshold))
                    {
                        next.Used = true;
                        overlapped.Add(next);
                    }
                    else
                    {
                        break;
                    }
                }

                if (overlapped.Count > 1)
                {
                    var maxHeightBox = overlapped.OrderByDescending(o => o.Obj.Y1 - o.Obj.Y0).First();
                    foreach (var o in sorted)
                    {
                        if (overlapped.Contains(o))
                        {
                            continue;
                        }

                        if (IsYOverlap(maxHeightBox.Obj, o.Obj, overlapThreshold))
                        {
                            o.Used = true;
                            overlapped.Add(o);
                        }
                    }
                }

                var objs = overlapped.Select(b => b.Obj).ToList();
                var lineSeg = new LineSeg
                {
                    Y0 = (int)curr.Obj.Y0,
                    Y1 = (int)overlapped.Max(o => o.Obj.Y1),
                    Cols = ScanForGapsBetweenBoxes(objs),
                    ChunkObjects = overlapped.Select(o => o.Obj).ToList()
                };

                if (lineSegs.Count >= 1)
                {
                    bool merged = false;
                    var lastLineSeg = lineSegs.Last();
                    if (lastLineSeg.Cols.Count != 0 && lastLineSeg.Cols.Count == lineSeg.Cols.Count) // TODO: check col postion??
                    {
                        var mergedBox = new List<ChunkObject>();
                        mergedBox.AddRange(lastLineSeg.ChunkObjects);
                        mergedBox.AddRange(lineSeg.ChunkObjects);
                        var gaps = ScanForGapsBetweenBoxes(mergedBox);

                        if (gaps.Count == lineSeg.Cols.Count)
                        {
                            lastLineSeg.Y1 = lineSeg.Y1;
                            lastLineSeg.ChunkObjects.AddRange(lineSeg.ChunkObjects);
                            lastLineSeg.Cols = gaps;

                            merged = true;
                        }
                    }

                    if (!merged)
                    {
                        lineSegs.Add(lineSeg);
                    }
                }
                else
                {
                    lineSegs.Add(lineSeg);
                }
            }

            return lineSegs;
        }
        
        class AnchoredChunkObject
        {
            public bool Used { get; set; } = false;
            public ChunkObject Obj;
        }

        class LineSeg
        {
            public int Y0;
            public int Y1;
            public List<int> Cols;
            public List<ChunkObject> ChunkObjects = new List<ChunkObject>();
        }

        private static void SortLineSegByColumns(LineSeg line)
        {
            if (line.Cols == null || line.Cols.Count == 0)
            {
                return;
            }
            
            var sortedObjs = new List<ChunkObject>();
            var columnBounds = new List<(int left, int right)>();

            columnBounds.Add((0, line.Cols[0]));
            for (int i = 0; i < line.Cols.Count - 1; i++)
            {
                columnBounds.Add((line.Cols[i], line.Cols[i + 1]));
            }
            columnBounds.Add((line.Cols.Last(), int.MaxValue));

            int columnCount = columnBounds.Count;
            var columns = new List<List<ChunkObject>>(new List<ChunkObject>[columnCount]);

            for (int i = 0; i < columnCount; i++)
            {
                columns[i] = new List<ChunkObject>();
            }

            foreach (var obj in line.ChunkObjects)
            {
                var x0 = obj.X0;
                var x1 = obj.X1;

                for (int i = 0; i < columnCount; i++)
                {
                    var (left, right) = columnBounds[i];
                    if (x0 >= left && x1 <= right)
                    {
                        columns[i].Add(obj);
                        break;
                    }
                }
            }

            foreach (var colObjs in columns)
            {
                var sortedCol = colObjs.OrderBy(o => Math.Min(o.Y0, o.Y1)).ToList();
                sortedObjs.AddRange(sortedCol);
            }

            line.ChunkObjects = sortedObjs;
        }


        private static bool IsYOverlap(ChunkObject a, ChunkObject b, float threshold = 2.0f)
        {
            var topA = Math.Min(a.Y0, a.Y1);
            var bottomA = Math.Max(a.Y0, a.Y1);
            var topB = Math.Min(b.Y0, b.Y1);
            var bottomB = Math.Max(b.Y0, b.Y1);

            var overlap = Math.Min(bottomA, bottomB) - Math.Max(topA, topB);
            return overlap > threshold;
        }

        // 判断 a 是否比 b 小（用 Y 高度判断）
        private static bool IsSmallerBox(ChunkObject a, ChunkObject b)
        {
            return (a.Y1 - a.Y0) <= (b.Y1 - b.Y0);
        }


        private static List<int> ScanForGapsBetweenBoxes(List<ChunkObject> ocrBoxes)
        {
            var gaps = new List<int>();
            if (ocrBoxes == null || ocrBoxes.Count == 0)
            {
                return gaps;
            }

            int step = 1;
            int minGapW = 5;

            var ocrBoxesCopy = new List<ChunkObject>(ocrBoxes);
            int minX = (int)ocrBoxesCopy.Min(r => r.X0);
            int maxX = (int)ocrBoxesCopy.Max(r => r.X1);

            int currentX = minX + 1;
            while (currentX < maxX)
            {
                if (TryGetIntersectingBox(currentX, ocrBoxesCopy, out var intersectingBox))
                {
                    currentX = (int)(intersectingBox.X1 + 1);
                    ocrBoxesCopy.RemoveAll(box => box.X1 <= currentX);
                }
                else
                {
                    int gapStart = currentX;

                    while (currentX < maxX)
                    {
                        if (TryGetIntersectingBox(currentX, ocrBoxesCopy, out intersectingBox))
                        {
                            break;
                        }
                        else
                        {
                            currentX += step;
                        }
                    }

                    if (currentX == maxX - 1)
                    {
                        break;
                    }

                    int gapEnd = currentX - 1;
                    int gapWidth = gapEnd - gapStart + 1;
                    if (gapWidth >= minGapW)
                    {
                        int gapCenter = (gapStart + gapEnd) / 2;
                        gaps.Add(gapCenter);
                    }

                    currentX = (int)(intersectingBox.X1 + 1);
                    ocrBoxesCopy.RemoveAll(box => box.X1 <= currentX);
                }
            }

            return gaps;
        }

        private static bool TryGetIntersectingBox(int x, List<ChunkObject> ocrBoxes, out ChunkObject intersectBox)
        {
            intersectBox = default;
            foreach (var box in ocrBoxes)
            {
                if (x >= box.X0 && x <= box.X1)
                {
                    intersectBox = box;
                    return true;
                }
            }
            return false;
        }
    }
}
