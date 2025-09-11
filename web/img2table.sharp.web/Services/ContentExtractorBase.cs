using img2table.sharp.web.Models;
using OpenCvSharp;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Collections.Generic;
using System;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;
using System.IO;
using System.Drawing;
using System.Linq;
using PDFDict.SDK.Sharp.Core;

namespace img2table.sharp.web.Services
{
    public class ContentExtractorBase
    {

        public static IDictionary<int, string> RenderTableBorderEnhanced(string pdfFile, LayoutDetectionResult detectResult, string workFolder, float renderDPI)
        {
            var tableImageDict = new Dictionary<int, string>();
            var pagesWithTable = ContentExtractorBase.GetPagesWithTableChunks(detectResult);
            if (pagesWithTable.Count == 0)
            {
                return tableImageDict;
            }

            using (PDFDocument pdfDoc = PDFDocument.Load(pdfFile))
            {
                int pageCount = pdfDoc.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    int pageNumber = i + 1;
                    if (!pagesWithTable.Contains(pageNumber))
                    {
                        continue;
                    }

                    var pdfPage = pdfDoc.LoadPage(i);
                    pdfPage.EnhancePathRendering();

                    string tableImagePath = Path.Combine(workFolder, GetTableImageFileName(pageNumber));
                    pdfDoc.RenderPage(tableImagePath, pdfPage, renderDPI, backgroundColor: Color.White);
                    tableImageDict[pageNumber] = tableImagePath;
                }
            }

            return tableImageDict;
        }

        public static string GetTableImageFileName(int pageNumber)
        {
            return $"page_{pageNumber}_table_image.png";
        }

        public static List<ContentElement> TransPageElements(List<PageElement> textElements, float ratio, double pageHeight)
        {
            List<ContentElement> transPageElements = new List<ContentElement>();
            double ph = pageHeight * ratio;
            foreach (var ele in textElements)
            {
                int top = (int)Math.Round(ph - ele.BBox.Top * ratio - ele.BBox.Height * ratio);
                int bottom = (int)Math.Round(top + ele.BBox.Height * ratio);
                int left = (int)Math.Round(ele.BBox.Left * ratio);
                int right = (int)Math.Round(ele.BBox.Right * ratio);

                ContentElement c = new ContentElement()
                {
                    Left = left,
                    Top = top,
                    Right = right,
                    Bottom = bottom,
                    PageElement = ele
                };
                transPageElements.Add(c);
            }

            return transPageElements;
        }

        public static void DrawPageElements(string pageImagePath, List<ContentElement> pageElements)
        {
            if (!File.Exists(pageImagePath))
            {
                Console.WriteLine($"Image not found: {pageImagePath}");
                return;
            }

            using var image = Cv2.ImRead(pageImagePath, ImreadModes.Color);
            int thickness = 1;

            foreach (var chunk in pageElements)
            {
                if (chunk.PageElement is TextElement)
                {
                    var x1 = chunk.Left;
                    var y1 = chunk.Top;
                    var x2 = chunk.Right;
                    var y2 = chunk.Bottom;

                    var scalarColor = new Scalar(255, 0, 0);
                    // Draw rectangle
                    Cv2.Rectangle(image, new OpenCvSharp.Point(x1, y1), new OpenCvSharp.Point(x2, y2), scalarColor, thickness);
                }
            }

            Cv2.ImWrite(pageImagePath, image);
        }

        public static void DrawPageChunks(string pageImagePath, IEnumerable<ObjectDetectionResult> chunkObjects)
        {
            if (!File.Exists(pageImagePath))
            {
                Console.WriteLine($"Image not found: {pageImagePath}");
                return;
            }

            using var image = Cv2.ImRead(pageImagePath, ImreadModes.Color);
            int thickness = 4;
            double fontScale = 2;

            foreach (var chunk in chunkObjects)
            {
                if (chunk.BoundingBox is not { Length: 4 }) continue;

                var x1 = (int)chunk.BoundingBox[0];
                var y1 = (int)chunk.BoundingBox[1];
                var x2 = (int)chunk.BoundingBox[2];
                var y2 = (int)chunk.BoundingBox[3];

                string label = chunk.Label ?? "Unknown";
                double conf = chunk.Confidence ?? 0.0;

                var scalarColor = new Scalar(0, 255, 0);
                if (DetectionLabel.LabelColors.TryGetValue(label, out var c))
                {
                    scalarColor = new Scalar(c.R, c.G, c.B);
                }

                // Draw rectangle
                Cv2.Rectangle(image, new OpenCvSharp.Point(x1, y1), new OpenCvSharp.Point(x2, y2), scalarColor, thickness);

                // Draw label text
                string text = $"{label} {conf:F2}";
                Cv2.PutText(image, text, new OpenCvSharp.Point(x1, y1 - 5), HersheyFonts.HersheyDuplex, fontScale, scalarColor, 1);
            }

            Cv2.ImWrite(pageImagePath, image);
        }

        public static List<int> GetPagesWithTableChunks(LayoutDetectionResult detectResult)
        {
            var pageIndicesWithTables = new List<int>();

            if (detectResult?.Results == null)
            {
                return pageIndicesWithTables;
            }

            foreach (var pageChunk in detectResult.Results)
            {
                if (pageChunk.Objects?.Any(obj => IsTableObject(obj)) == true)
                {
                    if (pageChunk.Page.HasValue)
                    {
                        pageIndicesWithTables.Add(pageChunk.Page.Value);
                    }
                }
            }

            return pageIndicesWithTables;
        }

        private static bool IsTableObject(ObjectDetectionResult chunkObject)
        {
            if (chunkObject?.Label == null)
            {
                return false;
            }

            return string.Equals(chunkObject.Label, DetectionLabel.Table, StringComparison.OrdinalIgnoreCase);
        }

        public static List<TextRect> GetTextBoxes(List<ContentElement> contentElements)
        {
            var rects = new List<TextRect>();
            foreach (var tc in contentElements)
            {
                if (tc.PageElement is TextElement)
                {
                    rects.Add(new TextRect(tc.Rect(), tc.Content));
                }
            }

            return rects;
        }

        public static List<ContentElement> FindContentElementsInBox(RectangleF chunkBox, List<ContentElement> pageElements)
        {
            var eles = new List<ContentElement>();
            foreach (var tc in pageElements)
            {
                var tcRect = tc.Rect();
                bool contained = IsContained(chunkBox, tcRect, ExtractOptions.DEFAULT_TEXT_OVERLAP_RATIO);
                if (contained)
                {
                    eles.Add(tc);
                }
                else
                {
                    if (tc.PageElement.Type == PageElement.ElementType.Image)
                    {
                        if (IsContained(chunkBox, tcRect, ExtractOptions.DEFAULT_IMAGE_OVERLAP_RATIO))
                        {
                            eles.Add(tc);
                        }
                    }
                }
            }

            return eles;
        }

        private static bool IsContained(RectangleF container, RectangleF dst, float overlapRatio)
        {
            RectangleF intersection = RectangleF.Intersect(container, dst);

            if (intersection.IsEmpty)
            {
                return false;
            }

            if (intersection.Equals(dst))
            {
                return true;
            }

            float intersectionArea = intersection.Width * intersection.Height;
            float dstArea = dst.Width * dst.Height;

            return intersectionArea / dstArea >= overlapRatio;
        }

        public static void DrawTableChunk(string pageImagePath, IEnumerable<TableCellDetectionResult> cells)
        {
            if (!File.Exists(pageImagePath))
            {
                Console.WriteLine($"Image not found: {pageImagePath}");
                return;
            }

            using var image = Cv2.ImRead(pageImagePath, ImreadModes.Color);
            int thickness = 1;

            foreach (var cell in cells)
            {
                var x1 = cell.X0;
                var y1 = cell.Y0;
                var x2 = cell.X1;
                var y2 = cell.Y1;

                var scalarColor = new Scalar(255, 0, 255);
                Cv2.Rectangle(image, new OpenCvSharp.Point(x1, y1), new OpenCvSharp.Point(x2, y2), scalarColor, thickness);
            }

            Cv2.ImWrite(pageImagePath, image);
        }
    }
}
