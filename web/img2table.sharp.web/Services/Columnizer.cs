using img2table.sharp.web.Models;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;
using System.Drawing;

namespace img2table.sharp.web.Services
{
    public class Columnizer
    {
        public static float PT_MinHorGap = 7.2f;

        public static List<ObjectDetectionResult> SortByColumns(List<ObjectDetectionResult> body)
        {
            if (body == null || body.Count == 0)
            {
                return body;
            }

            var sorted = new List<ObjectDetectionResult>();
            var anchoredChunkObject = body.Select(b => new AnchoredChunkObject { Obj = b }).ToList();
            var lines = SegmentLines(anchoredChunkObject);
            foreach (var line in lines)
            {
                SortLineSegByColumns(line);
                sorted.AddRange(line.ChunkObjects);
            }
            return sorted;
        }

        public static List<List<ObjectDetectionResult>> Columnize(List<ObjectDetectionResult> bodyChunks, float renderDPI = 300)
        {
            if (bodyChunks == null || bodyChunks.Count == 0)
            {
                return null;
            }

            var columns = new List<List<ObjectDetectionResult>>();

            int minGapW = (int)Math.Round((renderDPI / 72) * PT_MinHorGap);
            var rects = bodyChunks.Select(b => new Rectangle((int)b.X0, (int)b.Y0, (int)(b.X1 - b.X0), (int)(b.Y1 - b.Y0))).ToList();
            var cols = ScanForGapsBetweenBoxes(rects, minGapW);

            // TODO
            if (cols == null || cols.Count() == 0)
            {
                columns.Add(SortByColumns(bodyChunks));
                return columns;
            }

            var boxesCopy = new List<ObjectDetectionResult>(bodyChunks);
            for (int i = 0; i < cols.Count; i++)
            {
                int sep = cols[i];
                var group = boxesCopy.Where(c => c.X1 < sep);
                if (group == null || group.Count() == 0)
                {
                    continue;
                }
                group = group.OrderBy(c => c.Y0);
                columns.Add(group.ToList());

                foreach (var c in group)
                {
                    boxesCopy.Remove(c);
                }
            }

            if (boxesCopy.Count > 0)
            {
                columns.Add(boxesCopy.OrderBy(c => c.Y0).ToList());
            }

            return columns;
        }

        private static List<int> ScanForGapsBetweenBoxes(IEnumerable<Rectangle> chunkBoxes, double minGapW)
        {
            var gaps = new List<int>();
            if (chunkBoxes == null || chunkBoxes.Count() == 0)
            {
                return gaps;
            }

            int step = 1;
            var boxesCopy = new List<Rectangle>(chunkBoxes);
            int minX = boxesCopy.Min(r => r.Left);
            int maxX = boxesCopy.Max(r => r.Right);

            int currentX = minX + 1;
            while (currentX < maxX)
            {
                if (TryGetIntersectingBox(currentX, boxesCopy, out var intersectingBox))
                {
                    currentX = intersectingBox.Right + 1;
                    boxesCopy.RemoveAll(box => box.Right <= currentX);
                }
                else
                {
                    int gapStart = currentX;

                    while (currentX < maxX)
                    {
                        if (TryGetIntersectingBox(currentX, boxesCopy, out intersectingBox))
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

                    currentX = intersectingBox.Right + 1;
                    boxesCopy.RemoveAll(box => box.Right <= currentX);
                }
            }

            return gaps;
        }

        private static bool TryGetIntersectingBox(int x, List<Rectangle> boxes, out Rectangle intersectBox)
        {
            intersectBox = default;
            foreach (var box in boxes)
            {
                if (x >= box.Left && x <= box.Right)
                {
                    intersectBox = box;
                    return true;
                }
            }
            return false;
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
                    var bottomBox = TryRecursiseLook(overlapped, curr, sorted, overlapThreshold);
                    while (bottomBox != null)
                    {
                        bottomBox = TryRecursiseLook(overlapped, bottomBox, sorted, overlapThreshold);
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
                        var mergedBox = new List<ObjectDetectionResult>();
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

        private static AnchoredChunkObject TryRecursiseLook(List<AnchoredChunkObject> overlapped, AnchoredChunkObject curr, List<AnchoredChunkObject> sorted, float overlapThreshold)
        {
            bool found = false;
            var bottomBox = overlapped.OrderByDescending(o => o.Obj.Y1).First();
            if (bottomBox != curr)
            {
                foreach (var o in sorted)
                {
                    if (overlapped.Contains(o))
                    {
                        continue;
                    }

                    if (IsYOverlap(bottomBox.Obj, o.Obj, overlapThreshold))
                    {
                        o.Used = true;
                        overlapped.Add(o);
                        found = true;
                    }
                }
            }

            return found? bottomBox: null;
        }
        
        class AnchoredChunkObject
        {
            public bool Used { get; set; } = false;
            public ObjectDetectionResult Obj;
        }

        class LineSeg
        {
            public int Y0;
            public int Y1;
            public List<int> Cols;
            public List<ObjectDetectionResult> ChunkObjects = new List<ObjectDetectionResult>();
        }

        private static void SortLineSegByColumns(LineSeg line)
        {
            if (line.Cols == null || line.Cols.Count == 0)
            {
                return;
            }
            
            var sortedObjs = new List<ObjectDetectionResult>();
            var columnBounds = new List<(int left, int right)>();

            columnBounds.Add((0, line.Cols[0]));
            for (int i = 0; i < line.Cols.Count - 1; i++)
            {
                columnBounds.Add((line.Cols[i], line.Cols[i + 1]));
            }
            columnBounds.Add((line.Cols.Last(), int.MaxValue));

            int columnCount = columnBounds.Count;
            var columns = new List<List<ObjectDetectionResult>>(new List<ObjectDetectionResult>[columnCount]);

            for (int i = 0; i < columnCount; i++)
            {
                columns[i] = new List<ObjectDetectionResult>();
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


        private static bool IsYOverlap(ObjectDetectionResult a, ObjectDetectionResult b, float threshold = 2.0f)
        {
            var topA = Math.Min(a.Y0, a.Y1);
            var bottomA = Math.Max(a.Y0, a.Y1);
            var topB = Math.Min(b.Y0, b.Y1);
            var bottomB = Math.Max(b.Y0, b.Y1);

            var overlap = Math.Min(bottomA, bottomB) - Math.Max(topA, topB);
            return overlap > threshold;
        }

        // 判断 a 是否比 b 小（用 Y 高度判断）
        private static bool IsSmallerBox(ObjectDetectionResult a, ObjectDetectionResult b)
        {
            return (a.Y1 - a.Y0) <= (b.Y1 - b.Y0);
        }


        private static List<int> ScanForGapsBetweenBoxes(List<ObjectDetectionResult> ocrBoxes)
        {
            var gaps = new List<int>();
            if (ocrBoxes == null || ocrBoxes.Count == 0)
            {
                return gaps;
            }

            int step = 1;
            int minGapW = 5;

            var ocrBoxesCopy = new List<ObjectDetectionResult>(ocrBoxes);
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

        private static bool TryGetIntersectingBox(int x, List<ObjectDetectionResult> ocrBoxes, out ObjectDetectionResult intersectBox)
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
