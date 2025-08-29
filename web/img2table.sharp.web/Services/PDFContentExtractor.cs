using System.IO;
using System;
using System.Net.Http;
using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core;
using System.Threading.Tasks;
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
using System.Collections.Concurrent;
using System.Diagnostics;
using img2table.sharp.Img2table.Sharp.Tabular.TableImage;

namespace img2table.sharp.web.Services
{
    public class PDFContentExtractor
    {
        public static readonly string TempFolderName = "image2table_9966acf1-c43b-465c-bf7f-dd3c30394676";

        public float RenderDPI { get; set; } = 300;
        public float PredictConfidenceThreshold { get; set; } = 0.2f;

        public static float DEFAULT_TEXT_OVERLAP_RATIO = 0.75f;
        public static float DEFAULT_IMAGE_OVERLAP_RATIO = 0.9f;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _rootFolder;
        
        private ChunkElementProcessor _chunkElementProcessor;
        private bool _useEmbeddedHtml;
        private bool _ignoreMarginalia;
        private bool _outputFigureAsImage;
        private bool _enableOCR;
        private bool _embedImagesAsBase64;
        private string _docCategory;

        // debug params
        private bool _debug_draw_page_chunks = false;
        private bool _debug_draw_text_box = false;
        private bool _debug_save_dectect_image = false;

        public PDFContentExtractor(IHttpClientFactory httpClientFactory, string rootFolder, ExtractOptions extractOptions)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _rootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));

            _useEmbeddedHtml = extractOptions.UseEmbeddedHtml;
            _ignoreMarginalia = extractOptions.IgnoreMarginalia;
            _outputFigureAsImage = extractOptions.OutputFigureAsImage;
            _enableOCR = extractOptions.EnableOCR;
            _embedImagesAsBase64 = extractOptions.EmbedImagesAsBase64;
            _docCategory = extractOptions.DocCategory ?? LayoutDetectorFactory.DocumentCategory.AcademicPaper;
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

            return await detector.DetectAsync(pdfFileBytes, pdfFileName, RenderDPI, PredictConfidenceThreshold);
        }

        public async Task<DocumentChunks> ExtractAsync(byte[] pdfFileBytes, string pdfFileName)
        {
            var stopwatch = Stopwatch.StartNew();

            ChunkResult detectResult = await DetectAsync(pdfFileBytes, pdfFileName);
            if (detectResult == null)
            {
                throw new PDFContentExtractorException(HttpStatusCode.InternalServerError, "Layout detection failed.");
            }

            string jobFolderName = Guid.NewGuid().ToString();
            string workFolder = Path.Combine(_rootFolder, jobFolderName);
            if (!Directory.Exists(workFolder))
            {
                Directory.CreateDirectory(workFolder);
            }

            var chunkElementProcessorParameter = new ChunkElementProcessorParameter
            {
                UseEmbeddedHtml = _useEmbeddedHtml,
                IgnoreMarginalia = _ignoreMarginalia,
                OutputFigureAsImage = _outputFigureAsImage,
                EnableOCR = _enableOCR,
                EmbedBase64ImageData = _embedImagesAsBase64,
            };
            _chunkElementProcessor = new ChunkElementProcessor(workFolder, jobFolderName, chunkElementProcessorParameter);
            string pdfFile = Path.Combine(workFolder, pdfFileName);
            await File.WriteAllBytesAsync(pdfFile, pdfFileBytes);

            var tableEnhancedImagePathDict = RenderTableBorderEnhanced(pdfFile, detectResult, workFolder);

            using (PDFDocument pdfDoc = PDFDocument.Load(pdfFile))
            {
                var documentChunks = new DocumentChunks();
                documentChunks.DocumentName = pdfFileName;
                documentChunks.JobId = jobFolderName;

                var pagedChunks = new ConcurrentBag<PagedChunk>();
                int pageCount = pdfDoc.GetPageCount();

                var partition = Partition(pdfDoc, pageCount);
                if (partition.Count > 1)
                {
                    var tasks = partition.Select(pageList => ExtractPagesAsync(pdfDoc, pageList, workFolder, detectResult, jobFolderName, pagedChunks, tableEnhancedImagePathDict)).ToArray();
                    await Task.WhenAll(tasks);
                }
                else
                {
                    await ExtractPagesAsync(pdfDoc, partition[0], workFolder, detectResult, jobFolderName, pagedChunks, tableEnhancedImagePathDict);
                }

                documentChunks.PagedChunks = pagedChunks.OrderBy(t => t.PageNumber);

                bool _saveMarkdownToFile = true;
                if (_saveMarkdownToFile)
                {
                    string mdFile = Path.Combine(workFolder, pdfFileName + ".md");
                    File.WriteAllBytes(mdFile, Encoding.UTF8.GetBytes(documentChunks.Markdown));
                }

                stopwatch.Stop();
                documentChunks.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                return documentChunks;
            }
        }

        private IDictionary<int, string> RenderTableBorderEnhanced(string pdfFile, ChunkResult detectResult, string workFolder)
        {
            var tableImageDict = new Dictionary<int, string>();
            var pagesWithTable = GetPagesWithTableChunks(detectResult);
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
                    pdfDoc.RenderPage(tableImagePath, pdfPage, RenderDPI, backgroundColor: Color.White);
                    tableImageDict[pageNumber] = tableImagePath;
                }
            }

            return tableImageDict;
        }

        private static string GetTableImageFileName(int pageNumber)
        {
            return $"page_{pageNumber}_table_image.png";
        }

        private List<List<Tuple<int, PDFPage>>> Partition(PDFDocument pdfDoc, int pageCount)
        {
            int coreCount = Environment.ProcessorCount;
            int threadCount = Math.Min(pageCount, coreCount / 2);
            threadCount = threadCount > 3 ? 3 : threadCount;
            threadCount = threadCount <= 0 ? 1 : threadCount;

            // debug
            threadCount = 1;

            var partition = new List<List<Tuple<int, PDFPage>>>();
            for (int i = 0; i < threadCount; i++)
            {
                partition.Add(new List<Tuple<int, PDFPage>>());
            }

            for (int i = 0; i < pageCount; i++)
            {
                int threadIndex = i % threadCount;
                partition[threadIndex].Add(new (i, pdfDoc.LoadPage(i)));
            }

            return partition;
        }

        private readonly object _renderLock = new object();

        private Task ExtractPagesAsync(PDFDocument pdfDoc, List<Tuple<int, PDFPage>> pageList, string workFolder, 
            ChunkResult detectResult, string jobFolderName, ConcurrentBag<PagedChunk> pagedChunks, IDictionary<int, string> tableEnhancedImagePathDict)
        {
            return Task.Run(async () =>
            {
                foreach (var pageT in pageList)
                {
                    int pageIdx = pageT.Item1;
                    var page = pageT.Item2;

                    if (page.IsTagged())
                    {
                    }

                    int pageNumber = pageIdx + 1;
                    var pageImageName = $"page_{pageNumber}.png";
                    tableEnhancedImagePathDict.TryGetValue(pageNumber, out var tableEnhancedImagePath);

                    string pageImagePath = Path.Combine(workFolder, pageImageName);
                    lock (_renderLock)
                    {
                        pdfDoc.RenderPage(pageImagePath, pageIdx, RenderDPI, backgroundColor: Color.White);
                    }

                    var predictedPageChunks = detectResult?.Results?.FirstOrDefault(r => r.Page == pageIdx + 1);
                    var filteredChunks = ChunkUtils.FilterOverlapping(predictedPageChunks.Objects);
                    filteredChunks = ChunkUtils.FilterContainment(filteredChunks);
                    filteredChunks = ChunkUtils.RebuildReadingOrder(filteredChunks); // TODO

                    var chunks = BuildPageChunks(pdfDoc, page, workFolder, pageImagePath, tableEnhancedImagePath, filteredChunks, RenderDPI / 72f);
                    var pageChunks = new PagedChunk
                    {
                        PageNumber = pageNumber,
                        PreviewImagePath = $"{WorkDirectoryOptions.RequestPath}/{jobFolderName}/{pageImageName}",
                        Chunks = chunks
                    };
                    pagedChunks.Add(pageChunks);

                    if (_debug_draw_page_chunks)
                    {
                        DrawPageChunks(pageImagePath, filteredChunks);
                    }
                    if (_debug_save_dectect_image)
                    {
                        var previewImageName = $"detect_page_{pageNumber}.png";
                        string previewFile = Path.Combine(workFolder, previewImageName);
                        await ChunkUtils.SaveBase64ImageToFileAsync(previewFile, predictedPageChunks.LabeledImage);
                    }
                }
            });
        }

        private IList<ChunkElement> BuildPageChunks(PDFDocument pdfDoc, PDFPage pdfPage, string workFolder, string pageImagePath, string tableEnhancedImagePath, IEnumerable<ChunkObject> filteredChunkObjects, float ratio)
        {
            var pageThread = pdfPage.BuildPageThread();
            var textThread = pageThread.GetTextThread();

            var chunks = new List<ChunkElement>();
            var pageElements = TransPageElements(pageThread.GetContentList(), ratio, pdfPage.GetPageHeight());

            if (_debug_draw_text_box)
            {
                DrawPageElements(pageImagePath, pageElements);
            }

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

                bool isTableProcessed = false;
                if (chunkType == ChunkType.Table)
                {
                    if (/*DrawTableChunk*/ false)
                    {
                        DrawTableChunk(pageImagePath, chunkObject.Cells);
                    }

                    if (contentElements != null)
                    {
                        bool imagebaseTable = false;
                        if (contentElements.Count() == 0 || contentElements.Count() == 1 && contentElements[0].PageElement is ImageElement)
                        {
                            imagebaseTable = true;
                        }

                        var param = TabularParameter.AutoDetect;
                        param.CellTextOverlapRatio = 0.7f;
                        if (imagebaseTable)
                        {
                            var tableImageName = $"table_{Guid.NewGuid().ToString()}.png";
                            string tableImagePath = Path.Combine(workFolder, tableImageName);
                            ChunkUtils.ClipImage(pageImagePath, tableImagePath, chunkBox);

                            var imageTabular = new ImageTabular(param);
                            var pagedTable = imageTabular.Process(tableImagePath, chunkBox, loadText: true);

                            if (pagedTable != null && pagedTable.Tables?.Count() > 0)
                            {
                                var tables = new PagedTableDTO(pagedTable).Tables;
                                foreach (var table in tables) // TODO
                                {
                                    TableHTML.Generate(table, out string htmlTable, true);
                                    chunkElement.MarkdownText += htmlTable;
                                }
                                isTableProcessed = true;
                            }
                            else
                            {
                                isTableProcessed = false;
                                chunkElement.ChunkObject.Label = ChunkType.Image;
                            }
                        }
                        else
                        {
                            var tableImageName = $"table_{Guid.NewGuid().ToString()}.png";
                            string tableImagePath = Path.Combine(workFolder, tableImageName);
                            if (tableEnhancedImagePath != null)
                            {
                                ChunkUtils.ClipImage(tableEnhancedImagePath, tableImagePath, chunkBox);
                            }
                            else
                            {
                                ChunkUtils.ClipImage(pageImagePath, tableImagePath, chunkBox);
                            }

                            var mTables = MultiTableProcessor.BreakdownTables(tableImagePath, chunkBox);
                            if (mTables == null || mTables.Count() == 0)
                            {
                                TabularPDF(param, tableImagePath, chunkBox, contentElements, pdfDoc, pdfPage, ratio, chunkElement, _useEmbeddedHtml);
                            }
                            else
                            {
                                foreach (var region in mTables)
                                {
                                    tableImageName = $"table_{Guid.NewGuid().ToString()}.png";
                                    tableImagePath = Path.Combine(workFolder, tableImageName);
                                    if (tableEnhancedImagePath != null)
                                    {
                                        ChunkUtils.ClipImage(tableEnhancedImagePath, tableImagePath, region);
                                    }
                                    else
                                    {
                                        ChunkUtils.ClipImage(pageImagePath, tableImagePath, region);
                                    }

                                    contentElements = FindContentElementsInBox(region, pageElements);
                                    chunkElement.ContentElements = contentElements;
                                    TabularPDF(param, tableImagePath, region, contentElements, pdfDoc, pdfPage, ratio, chunkElement, _useEmbeddedHtml);
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(_chunkElementProcessor.Process(chunkElement, pageImagePath)))
                {
                    chunks.Add(chunkElement);
                }
            }

            return chunks;
        }

        private static bool TabularPDF(TabularParameter param, string tableImagePath, RectangleF? chunkBox, List<ContentElement> contentElements, 
            PDFDocument pdfDoc, PDFPage pdfPage, float ratio, ChunkElement chunkElement, bool useHtml)
        {
            var imageTabular = new ImageTabular(param);
            var pagedTable = imageTabular.Process(tableImagePath, chunkBox, GetTextBoxes(contentElements), false);

            var pdfTabular = new PDFTabular(param);
            pdfTabular.LoadText(pdfDoc, pdfPage, pagedTable, ratio, useHtml);

            bool isTableProcessed = false;
            if (pagedTable != null && pagedTable.Tables?.Count() > 0)
            {
                var tables = new PagedTableDTO(pagedTable).Tables;
                foreach (var table in tables) // TODO
                {
                    TableHTML.Generate(table, out string htmlTable, true);
                    chunkElement.MarkdownText += htmlTable;
                }
                isTableProcessed = true;
            }
            else
            {
                isTableProcessed = false;
                chunkElement.ChunkObject.Label = ChunkType.Text;
            }

            return isTableProcessed;
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

        private static List<TextRect> GetTextBoxes(List<ContentElement> contentElements)
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

        private static List<ContentElement> FindContentElementsInBox(RectangleF chunkBox, List<ContentElement> pageElements)
        {
            var eles = new List<ContentElement>();
            foreach (var tc in pageElements)
            {
                var tcRect = tc.Rect();
                bool contained = IsContained(chunkBox, tcRect, DEFAULT_TEXT_OVERLAP_RATIO);
                if (contained)
                {
                    eles.Add(tc);
                }
                else
                {
                    if (tc.PageElement.Type == PageElement.ElementType.Image)
                    {
                        if (IsContained(chunkBox, tcRect, DEFAULT_IMAGE_OVERLAP_RATIO))
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

        private static void DrawTableChunk(string pageImagePath, IEnumerable<TableCellChunk> cells)
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

        private static void DrawPageElements(string pageImagePath, List<ContentElement> pageElements)
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

        private static List<int> GetPagesWithTableChunks(ChunkResult detectResult)
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

        private static bool IsTableObject(ChunkObject chunkObject)
        {
            if (chunkObject?.Label == null)
            {
                return false;
            }

            return string.Equals(chunkObject.Label, ChunkType.Table, StringComparison.OrdinalIgnoreCase);
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

        public string OCRText { get; set; } = string.Empty;

        public string Content
        {
            get
            {
                if (PageElement is TextElement textElement)
                {
                    return textElement.GetText();
                }
                else if (PageElement is ImageElement)
                {
                    return OCRText;
                }

                return string.Empty;
            }
        }
    }

    public class ChunkElement
    {
        [JsonPropertyName("chunkObject")]
        public ChunkObject ChunkObject { get; set; }

        [JsonIgnore]
        public IEnumerable<ContentElement> ContentElements { get; set; }

        [JsonPropertyName("markdownText")]
        public string MarkdownText { get; set; }
    }

    public class PagedChunk
    {
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }

        [JsonPropertyName("previewImagePath")]
        public string PreviewImagePath { get; set; }

        [JsonPropertyName("chunks")]
        public IEnumerable<ChunkElement> Chunks { get; set; }
    }

    public class DocumentChunks
    {
        [JsonPropertyName("documentName")]
        public string DocumentName { get; set; }

        [JsonPropertyName("pagedChunks")]
        public IEnumerable<PagedChunk> PagedChunks { get; set; }

        [JsonPropertyName("jobId")]
        public string JobId { get; set; }

        [JsonPropertyName("elapsedMilliseconds")]
        public long ElapsedMilliseconds { get; set; } = 0;

        [JsonPropertyName("markdown")]
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

    public class ExtractOptions
    {
        public bool UseEmbeddedHtml { get; set; } = false;
        public bool IgnoreMarginalia { get; set; } = false;
        public bool EnableOCR { get; set; } = false;
        public bool EmbedImagesAsBase64 { get; set; } = false;
        public bool OutputFigureAsImage { get; set; } = false;
        public string DocCategory { get; set; } = LayoutDetectorFactory.DocumentCategory.AcademicPaper;
    }
}
