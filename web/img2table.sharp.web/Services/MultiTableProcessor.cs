using OpenCvSharp;
using System.Collections.Generic;
using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace img2table.sharp.web.Services
{
    public class MultiTableProcessor
    {
        public static float PT_MinGap = 7.2f;
        public static float PT_MinCol = 72f;

        public static List<RectangleF> BreakdownTables(string tableImgFile, RectangleF tableBbox, float renderDPI = 300)
        {
            if (tableImgFile == null || !File.Exists(tableImgFile))
            {
                return null;
            }
            
            int minGapWidth = (int) Math.Round((renderDPI / 72) * PT_MinGap);
            int minColWidth = (int) Math.Round((renderDPI / 72) * PT_MinCol);
            using var img = Cv2.ImRead(tableImgFile);

            var tableRect = new Rect(
                (int)tableBbox.X,
                (int)tableBbox.Y,
                (int)tableBbox.Width,
                (int)tableBbox.Height
            );

            using var gray = new Mat();
            Cv2.CvtColor(img, gray, ColorConversionCodes.BGR2GRAY);
            using var binary = new Mat();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            bool hasInvalid = false;
            var gaps = FindXSeps(binary, minGapWidth);
            if (gaps.Count == 0)
            {
                var h_ranges = FindYSeps(binary, tableRect, minGapWidth);
                if (h_ranges.Count > 0)
                {
                    var ll = SepYRegion(h_ranges, tableRect);
                    hasInvalid = ll.Any(r => r.Width < minColWidth || r.Height < minGapWidth);
                    return hasInvalid ? null : ToRectangleF(ll);
                }
                return null;
            }

            var regions = new List<Rect>();
            int l, r, t, b;
            int lastR = 0;
            Rect region;
            List<Rect> seps;
            for (int i = 0; i < gaps.Count; i++)
            {
                var gap = gaps[i];
                if (i == 0)
                {
                    l = tableRect.Left;
                    r = gap.Item1;
                    t = tableRect.Top;
                    b = tableRect.Bottom;

                    region = Rect.FromLTRB(l, t, r, b);
                    seps = ProcessRegion(binary, region, minGapWidth);
                    if (seps?.Count > 0)
                    {
                        regions.AddRange(seps);
                    }
                    else
                    {
                        regions.Add(region);
                    }

                    lastR = gap.Item2;
                    continue;
                }

                l = lastR;
                r = gap.Item1;
                t = tableRect.Top;
                b = tableRect.Bottom;

                region = Rect.FromLTRB(l, t, r, b);
                seps = ProcessRegion(binary, region, minGapWidth);
                if (seps?.Count > 0)
                {
                    regions.AddRange(seps);
                }
                else
                {
                    regions.Add(region);
                }

                lastR = gap.Item2;
            }

            l = lastR;
            r = tableRect.Right;
            t = tableRect.Top;
            b = tableRect.Bottom;
            region = Rect.FromLTRB(l, t, r, b);
            seps = ProcessRegion(binary, region, minGapWidth);
            if (seps?.Count > 0)
            {
                regions.AddRange(seps);
            }
            else
            {
                regions.Add(region);
            }

            //for (int i = 0; i < regions.Count; i++)
            //{
            //    var region = regions[i];
            //    Cv2.Rectangle(binary, region, new Scalar(0, 255, 0), 7);
            //}
            //Cv2.ImWrite(@"C:\dev\testfiles\ai_testsuite\pdf\table\kv-test\mul_table\hgr.png", binary);

            hasInvalid = regions.Any(r => r.Width < minColWidth || r.Height < minGapWidth);
            return hasInvalid? null: ToRectangleF(regions);
        }

        private static List<Rect> ProcessRegion(Mat binary, Rect region, int minGap)
        {
            using var regionMat = binary.Clone();
            regionMat.SetTo(new Scalar(255));
            binary[region].CopyTo(regionMat[region]);

            var h_ranges = FindYSeps(regionMat, region, minGap);
            if (h_ranges.Count > 0)
            {
                return SepYRegion(h_ranges, region);
            }

            return null;
        }

        private static List<RectangleF> ToRectangleF(List<Rect> rects)
        {
            if (rects == null || rects.Count == 0)
            {
                return null;
            }

            return rects.Select(rect => new RectangleF(rect.X, rect.Y, rect.Width, rect.Height)).ToList();
        }

        private static List<Rect> SepYRegion(List<(int, int)> yGaps, Rect region)
        {
            var regions = new List<Rect>();
            int lastB = 0;
            for (int i = 0; i < yGaps.Count; i++)
            {
                var gap = yGaps[i];
                if (i == 0)
                {
                    var l = region.Left;
                    var r = region.Right;
                    var t = region.Top;
                    var b = gap.Item1;
                    regions.Add(Rect.FromLTRB(l, t, r, b));
                    lastB = gap.Item2;
                }

                if (i > 0 && i < yGaps.Count - 1)
                {
                    var l = region.Left;
                    var r = region.Right;
                    var t = lastB;
                    var b = gap.Item1;
                    regions.Add(Rect.FromLTRB(l, t, r, b));
                    lastB = gap.Item2;
                }

                if (i == yGaps.Count - 1)
                {
                    var l = region.Left;
                    var r = region.Right;
                    var t = lastB;
                    var b = region.Bottom;
                    regions.Add(Rect.FromLTRB(l, t, r, b));
                }
            }

            return regions;
        }

        private static List<(int, int)> FindYSeps(Mat binary, Rect region, int minGap)
        {
            var separators = new List<int>();
            int[] density = new int[binary.Height];

            for (int i = 0; i < binary.Height; i++)
            {
                int blankCount = 0;
                for (int j = 0; j < binary.Width; j++)
                {
                    if (binary.Get<byte>(i, j) == 255)
                    {
                        blankCount++;
                    }
                }
                density[i] = blankCount;
            }

            var top = region.Top;
            var bottom = region.Bottom;
            List<(int, int)> ranges = new List<(int, int)>();
            for (int i = top; i < bottom; i++)
            {
                if (binary.Width != density[i])
                {
                    continue;
                }

                int start = i;
                while (i < bottom && binary.Width == density[i])
                {
                    i++;
                }
                int end = i;

                if (start > minGap && end < bottom - minGap)
                {
                    if (end - start > minGap)
                    {
                        ranges.Add((start, end));
                    }
                }
            }

            return ranges;
        }

        private static List<(int, int)> FindXSeps(Mat binary, int minGap)
        {
            var separators = new List<int>();
            int[] density = new int[binary.Width];

            for (int i = 0; i < binary.Width; i++)
            {
                int blankCount = 0;
                for (int j = 0; j < binary.Height; j++)
                {
                    if (binary.Get<byte>(j, i) == 255)
                    {
                        blankCount++;
                    }
                }
                density[i] = blankCount;
            }

            List<(int, int)> ranges = new List<(int, int)>();
            for (int i = 0; i < density.Length; i++)
            {
                if (binary.Height != density[i])
                {
                    continue;
                }

                int start = i;
                while (i < density.Length && binary.Height == density[i])
                {
                    i++;
                }
                int end = i;

                if (start > minGap && end < binary.Width - minGap)
                {
                    if (end - start > minGap)
                    {
                        ranges.Add((start, end));
                    }
                }
            }

            return ranges;
        }
    }
}
