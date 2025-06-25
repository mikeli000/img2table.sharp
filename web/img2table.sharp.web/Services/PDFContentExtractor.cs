using System.IO;
using System;
using System.Net.Http;
using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Net;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using System.Collections.Generic;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Text.Json.Serialization;
using System.Text;
using Img2table.Sharp.Tabular;
using img2table.sharp.Img2table.Sharp.Data;

namespace img2table.sharp.web.Services
{
    public class PDFContentExtractor
    {
        public static readonly string TempFolderName = "image2table_9966acf1-c43b-465c-bf7f-dd3c30394676";

        public static float DEFAULT_OVERLAP_RATIO = 0.8f;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _rootFolder;
        private float RenderDPI = 300;
        private ChunkElementProcessor _chunkElementProcessor;
        private bool _useEmbeddedHtml;
        private string _docCategory;

        public PDFContentExtractor(IHttpClientFactory httpClientFactory, string rootFolder, bool useEmbeddedHtml, bool ignoreMarginalia, string docCategory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _rootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));

            _useEmbeddedHtml = useEmbeddedHtml;
            _docCategory = docCategory ?? LayoutDetectorFactory.DocumentCategory.SlideLike;
            _chunkElementProcessor = new ChunkElementProcessor(useEmbeddedHtml, ignoreMarginalia);
        }

        private async Task<ChunkResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName)
        {
            var detector = new LayoutDetectorFactory(_httpClientFactory).Create(_docCategory);
            if (detector == null)
            {
                throw new InvalidOperationException($"No layout detector found for category: {_docCategory}");
            }
            if (pdfFileBytes == null || pdfFileBytes.Length == 0)
            {
                throw new ArgumentException("PDF file bytes cannot be null or empty.", nameof(pdfFileBytes));
            }

            return await detector.DetectAsync(pdfFileBytes, pdfFileName);
        }

        private bool _drawPageChunks = true;
        private bool _saveDectectImage = true;

        public async Task<DocumentChunks> ExtractAsync(byte[] pdfFileBytes, string pdfFileName)
        {
            ChunkResult detectResult = await DetectAsync(pdfFileBytes, pdfFileName);

            string jobFolderName = Guid.NewGuid().ToString();
            string workFolder = Path.Combine(_rootFolder, jobFolderName);
            if (!Directory.Exists(workFolder))
            {
                Directory.CreateDirectory(workFolder);
            }

            string pdfFile = Path.Combine(workFolder, pdfFileName);
            await File.WriteAllBytesAsync(pdfFile, pdfFileBytes);
            using (PDFDocument pdfDoc = PDFDocument.Load(pdfFile))
            {
                var documentChunks = new DocumentChunks();
                documentChunks.DocumentName = pdfFileName;
                var pagedChunks = new List<PagedChunk>();

                int pageCount = pdfDoc.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    // step 1: check if page is tagged, if yes, use tagged content
                    var page = pdfDoc.LoadPage(i);
                    if (page.IsTagged())
                    {
                    }

                    int pageNumber = i + 1;
                    var pageImageName = $"page_{pageNumber}.png";
                    string pageImagePath = Path.Combine(workFolder, pageImageName);
                    pdfDoc.RenderPage(pageImagePath, i, RenderDPI, backgroundColor: Color.White);

                    var predictedPageChunks = detectResult.Results?.FirstOrDefault(r => r.Page == i + 1);
                    var filteredChunks = ChunkUtils.FilterOverlapping(predictedPageChunks.Objects);
                    filteredChunks = ChunkUtils.FilterContainment(filteredChunks);
                    filteredChunks = ChunkUtils.RebuildReadingOrder(filteredChunks);

                    var chunks = BuildPageChunks(pdfDoc, page, workFolder, pageImagePath, filteredChunks, RenderDPI / 72f);
                    var pageChunks = new PagedChunk
                    {
                        PageNumber = pageNumber,
                        PreviewImagePath = $"{WorkDirectoryOptions.RequestPath}/{jobFolderName}/{pageImageName}",
                        Chunks = chunks
                    };
                    pagedChunks.Add(pageChunks);

                    if (_drawPageChunks)
                    {
                        DrawPageChunks(pageImagePath, filteredChunks);
                    }
                    if (_saveDectectImage)
                    {
                        var previewImageName = $"detect_page_{pageNumber}.png";
                        string previewFile = Path.Combine(workFolder, previewImageName);
                        await SaveBase64ImageToFileAsync(previewFile, predictedPageChunks.LabeledImage);
                    }
                }

                documentChunks.PagedChunks = pagedChunks;
                return documentChunks;
            }
        }

        private IList<ChunkElement> BuildPageChunks(PDFDocument pdfDoc, PDFPage pdfPage, string workFolder, string pageImage, IEnumerable<ChunkObject> filteredChunkObjects, float ratio)
        {
            var pageThread = pdfPage.BuildPageThread();
            var textThread = pageThread.GetTextThread();

            var chunks = new List<ChunkElement>();
            var pageElements = TransPageElements(pageThread.GetContentList(), ratio, pdfPage.GetPageHeight());
            foreach (var chunkObject in filteredChunkObjects)
            {
                var chunkType = ChunkType.MappingChunkType(chunkObject.Label);
                if (chunkType == ChunkType.Unknown)
                {
                    continue;
                }

                var chunkBox = RectangleF.FromLTRB((float)chunkObject.BoundingBox[0], (float)chunkObject.BoundingBox[1], (float)chunkObject.BoundingBox[2], (float)chunkObject.BoundingBox[3]);
                var contentElements = FindContentElementsInBox(chunkBox, pageElements);

                var chunkElement = new ChunkElement
                {
                    ChunkObject = chunkObject,
                    ContentElements = contentElements
                };

                if (chunkType == ChunkType.Table)
                {
                    var tableImageName = $"table_{Guid.NewGuid().ToString()}.png";
                    string tableImagePath = Path.Combine(workFolder, tableImageName);
                    ClipTableImage(pageImage, tableImagePath, chunkBox);

                    if (contentElements != null)
                    {
                        bool imagebaseTable = false;
                        if (contentElements.Count() == 0 || contentElements.Count() == 1 && contentElements[0].PageElement is ImageElement)
                        {
                            imagebaseTable = true;
                        }

                        if (imagebaseTable)
                        {
                            var param = TabularParameter.AutoDetect;
                            param.ImplicitRows = true;
                            param.ImplicitColumns = true;
                            param.CellTextOverlapRatio = 0.7f;
                            var imageTabular = new ImageTabular(param);
                            var pagedTable = imageTabular.Process(tableImagePath, true);

                            if (pagedTable != null)
                            {
                                var tables = new PagedTableDTO(pagedTable).Tables;
                                for (int j = tables.Count - 1; j >= 0; j--) // TODO
                                {
                                    var table = tables[j];
                                    TableHTML.Generate(table, out string htmlTable);
                                    chunkElement.MarkdownText += htmlTable;
                                }
                            }
                        }
                        else
                        {
                            var param = TabularParameter.AutoDetect;
                            param.CellTextOverlapRatio = 0.7f;

                            var imageTabular = new ImageTabular(param);
                            var pagedTable = imageTabular.Process(tableImagePath, false);

                            var pdfTabular = new PDFTabular(param);
                            pdfTabular.LoadText(pdfDoc, pdfPage, pagedTable, ratio, useHtml: _useEmbeddedHtml);

                            if (pagedTable != null)
                            {
                                var tables = new PagedTableDTO(pagedTable).Tables;
                                for (int j = tables.Count - 1; j >= 0; j--) // TODO
                                {
                                    var table = tables[j];
                                    TableHTML.Generate(table, out string htmlTable);
                                    chunkElement.MarkdownText += htmlTable;
                                }
                            }
                        }
                    }
                }

                _chunkElementProcessor.Process(chunkElement);
                chunks.Add(chunkElement);
            }

            return chunks;
        }

        private static void ClipTableImage(string pageImage, string clippedImage, RectangleF tableBbox)
        {
            using Mat src = Cv2.ImRead(pageImage);
            Rect roi = new Rect(
                (int)Math.Floor(tableBbox.X),
                (int)Math.Floor(tableBbox.Y),
                (int)Math.Ceiling(tableBbox.Width),
                (int)Math.Ceiling(tableBbox.Height)
            );

            roi = roi.Intersect(new Rect(0, 0, src.Width, src.Height));
            Mat whiteBg = new Mat(src.Size(), src.Type(), new Scalar(255, 255, 255));
            src[roi].CopyTo(whiteBg[roi]);

            Cv2.ImWrite(clippedImage, whiteBg);
        }

        private bool IsChunkType(string label, string chunkType)
        {
            return string.Equals(label, chunkType, StringComparison.OrdinalIgnoreCase);
        }

        private List<ContentElement> TransPageElements(List<PageElement> textElements, float ratio, double pageHeight)
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

        private static List<ContentElement> FindContentElementsInBox(RectangleF chunkBox, List<ContentElement> pageElements)
        {
            var eles = new List<ContentElement>();
            foreach (var tc in pageElements)
            {
                var tcRect = tc.Rect();

                bool contained = IsContained(chunkBox, tcRect);
                if (contained)
                {
                    eles.Add(tc);
                }
                else
                {
                    if (tc.PageElement.Type == PageElement.ElementType.Image)
                    {
                        if (IsContained(tcRect, chunkBox))
                        {
                            eles.Add(tc);
                        }
                    }
                }
            }
            //chunks = chunks.OrderBy(c => c.Left).ToList();
            //chunks = chunks.OrderBy(c => c.Top).ToList();

            return eles;
        }

        private static bool IsContained(RectangleF container, RectangleF dst)
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

            return intersectionArea / dstArea >= DEFAULT_OVERLAP_RATIO;
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

        private static void DrawPageChunks(string pageImagePath, IEnumerable<ChunkObject> chunkObjects)
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
                if (ChunkType.LabelColors.TryGetValue(label, out var c))
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
    }

    public class ContentElement
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public PageElement PageElement { get; set; }

        public RectangleF Rect()
        {
            return RectangleF.FromLTRB(Left, Top, Right, Bottom);
        }

        public string Content
        {
            get
            {
                if (PageElement is TextElement textElement)
                {
                    return textElement.GetText();
                }

                return string.Empty;
            }
        }
    }

    public class ChunkElement
    {
        public ChunkObject ChunkObject { get; set; }

        [JsonIgnore]
        public IEnumerable<ContentElement> ContentElements { get; set; }

        public string MarkdownText { get; set; }
    }

    public class PagedChunk
    {
        public int PageNumber { get; set; }
        public string PreviewImagePath { get; set; }
        public IEnumerable<ChunkElement> Chunks { get; set; }
    }

    public class DocumentChunks
    {
        public string DocumentName { get; set; }
        public IEnumerable<PagedChunk> PagedChunks { get; set; }
        public string Markdown
        {
            get
            {
                if (PagedChunks == null || !PagedChunks.Any())
                {
                    return string.Empty;
                }

                var markdownBuilder = new StringBuilder();
                foreach (var pageChunk in PagedChunks)
                {
                    //markdownBuilder.AppendLine($"# Page {pageChunk.PageNumber}");
                    foreach (var chunk in pageChunk.Chunks)
                    {
                        //markdownBuilder.AppendLine($"## {chunk.ChunkObject.Label}");
                        markdownBuilder.AppendLine(chunk.MarkdownText);
                    }
                }
                return markdownBuilder.ToString();
            }
        }
    }

    public class PDFContentExtractorException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ResponseContent { get; }

        public PDFContentExtractorException(HttpStatusCode statusCode, string content)
            : base($"PDF content extraction failed with status code {(int)statusCode}: {content}")
        {
            StatusCode = statusCode;
            ResponseContent = content;
        }
    }
}
