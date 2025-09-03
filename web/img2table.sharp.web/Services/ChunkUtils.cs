using img2table.sharp.web.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace img2table.sharp.web.Services
{
    public static class ChunkUtils
    {
        public static bool IsOverlapping(double[] boxA, double[] boxB, double iouThreshold = 0.8)
        {
            double xA = Math.Max(boxA[0], boxB[0]);
            double yA = Math.Max(boxA[1], boxB[1]);
            double xB = Math.Min(boxA[2], boxB[2]);
            double yB = Math.Min(boxA[3], boxB[3]);

            double interWidth = Math.Max(0, xB - xA);
            double interHeight = Math.Max(0, yB - yA);
            double interArea = interWidth * interHeight;

            double boxAArea = (boxA[2] - boxA[0]) * (boxA[3] - boxA[1]);
            double boxBArea = (boxB[2] - boxB[0]) * (boxB[3] - boxB[1]);

            double iou = interArea / (boxAArea + boxBArea - interArea);

            return iou > iouThreshold;
        }

        public static List<ChunkObject> FilterOverlapping(IEnumerable<ChunkObject> objects, double iouThreshold = 0.8)
        {
            var result = new List<ChunkObject>();

            foreach (var obj in objects)
            {
                var overlapping = result.FirstOrDefault(existing =>
                    IsOverlapping(existing.BoundingBox, obj.BoundingBox, iouThreshold));

                if (overlapping == null)
                {
                    result.Add(obj);
                }
                else
                {
                    if ((obj.Confidence ?? 0) > (overlapping.Confidence ?? 0))
                    {
                        result.Remove(overlapping);
                        result.Add(obj);
                    }
                }
            }

            return result;
        }

        public static List<ChunkObject> FilterContainment(IEnumerable<ChunkObject> objects)
        {
            var result = new List<ChunkObject>();

            var sorted = objects.OrderByDescending(obj => Area(obj.BoundingBox));
            foreach (var obj in sorted)
            {
                bool isContained = result.Any(existing =>
                    IsContainment(existing.BoundingBox, obj.BoundingBox));

                if (!isContained)
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        public static bool IsContainment(double[] bigRect, double[] smallArea, double epsilon = 2.0, double containmentThreshold = 0.9)
        {
            bool ret =  bigRect[0] <= smallArea[0] + epsilon &&
                   bigRect[1] <= smallArea[1] + epsilon &&
                   bigRect[2] >= smallArea[2] - epsilon &&
                   bigRect[3] >= smallArea[3] - epsilon;
            if (ret)
            {
                return true;
            }

            double bArea = Area(smallArea);
            if (bArea <= 0)
            {
                return false;
            }

            double intersectionLeft = Math.Max(bigRect[0], smallArea[0]);
            double intersectionTop = Math.Max(bigRect[1], smallArea[1]);
            double intersectionRight = Math.Min(bigRect[2], smallArea[2]);
            double intersectionBottom = Math.Min(bigRect[3], smallArea[3]);

            double intersectionWidth = Math.Max(0, intersectionRight - intersectionLeft);
            double intersectionHeight = Math.Max(0, intersectionBottom - intersectionTop);
            double intersectionArea = intersectionWidth * intersectionHeight;

            double containmentRatio = intersectionArea / bArea;
            return containmentRatio >= containmentThreshold;
        }

        public static double Area(double[] rect)
        {
            var width = rect[2] - rect[0];
            var height = rect[3] - rect[1];
            return width * height;
        }

        public static List<ChunkObject> RebuildReadingOrder(IEnumerable<ChunkObject> objects, float renderDPI = 300)
        {
            var headers = objects.Where(c => c.Label == ChunkType.PageHeader || c.Label == ChunkType.Header).ToList().OrderBy(c => c.X1);
            var footers = objects.Where(c => c.Label == ChunkType.PageFooter || c.Label == ChunkType.Footer).ToList().OrderBy(c => c.X1);

            var artifacts = objects.Where(c => c.Label == ChunkType.Abandon || c.Label == ChunkType.Footnote || c.Label == ChunkType.Number || c.Label == ChunkType.PageNumber).ToList().OrderBy(c => c.X1);
            var body = objects.Except(headers).Except(footers).Except(artifacts).ToList();

            var result = new List<ChunkObject>();

            result.AddRange(headers);
            //result.AddRange(Columnizer.SortByColumns(body));

            var cols = Columnizer.Columnize(body, renderDPI);
            foreach (var col in cols)
            {
                result.AddRange(col);
            }
            result.AddRange(footers);

            return result;
        }

        public static void SplitIntoColumns(List<ChunkObject> boxes, out List<ChunkObject> left, out List<ChunkObject> right)
        {
            var centers = boxes
                .Select(b => (b.X0 + b.X1) / 2)
                .OrderBy(c => c)
                .ToList();

            double maxGap = 0;
            double splitX = 0;

            for (int i = 0; i < centers.Count - 1; i++)
            {
                var gap = centers[i + 1] - centers[i];
                if (gap > maxGap)
                {
                    maxGap = gap;
                    splitX = (centers[i + 1] + centers[i]) / 2;
                }
            }

            left = SortByReadingOrder(boxes.Where(b => (b.X0 + b.X1) / 2 <= splitX).ToList());
            right = SortByReadingOrder(boxes.Where(b => (b.X0 + b.X1) / 2 > splitX).ToList());
        }

        public static List<ChunkObject> SortByReadingOrder(List<ChunkObject> boxes)
        {
            return boxes
                .OrderBy(b => b.Y0)
                .ThenBy(b => b.X0)
                .ToList();
        }

        public static void ClipChunkRectImage(string pageImage, string clippedImage, ChunkObject chunkObject, bool useOriginalSize = true)
        {
            var chunkBox = RectangleF.FromLTRB((float)chunkObject.BoundingBox[0], (float)chunkObject.BoundingBox[1], (float)chunkObject.BoundingBox[2], (float)chunkObject.BoundingBox[3]);
            ClipImage(pageImage, clippedImage, chunkBox, useOriginalSize);
        }

        public static void ClipImage(string pageImage, string clippedImage, RectangleF tableBbox, bool useOriginalSize = true)
        {
            using Mat src = Cv2.ImRead(pageImage);
            Rect roi = new Rect(
                (int)Math.Floor(tableBbox.X),
                (int)Math.Floor(tableBbox.Y),
                (int)Math.Ceiling(tableBbox.Width + 4), // TODO
                (int)Math.Ceiling(tableBbox.Height)
            );

            roi = roi.Intersect(new Rect(0, 0, src.Width, src.Height));

            if (useOriginalSize)
            {
                Mat whiteBg = new Mat(src.Size(), src.Type(), new Scalar(255, 255, 255));
                src[roi].CopyTo(whiteBg[roi]);
                Cv2.ImWrite(clippedImage, whiteBg);
            }
            else
            {
                Mat clipped = new Mat(src, roi);
                Cv2.ImWrite(clippedImage, clipped);
            }
        }

        public static async Task SaveBase64ImageToFileAsync(string outputFilePath, string base64String)
        {
            if (base64String.Contains(","))
            {
                base64String = base64String.Substring(base64String.IndexOf(",") + 1);
            }

            byte[] imageBytes = Convert.FromBase64String(base64String);
            await File.WriteAllBytesAsync(outputFilePath, imageBytes);
        }

        public static string EncodeBase64ImageData(string imageFile)
        {
            try
            {
                if (!File.Exists(imageFile))
                {
                    Console.WriteLine($"File not found {imageFile}");
                    return null;
                }

                byte[] imageBytes = File.ReadAllBytes(imageFile);
                string base64String = Convert.ToBase64String(imageBytes);

                string extension = Path.GetExtension(imageFile).ToLower();
                string mimeType = GetMimeType(extension);

                string imageData = $"data:{mimeType};base64,{base64String}";
                return imageData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to encode image to base64: {ex.Message}");
            }

            return null;
        }

        private static string GetMimeType(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "image/jpeg"
            };
        }
    }
}
