using System.IO;
using System;
using System.Net.Http;
using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core;
using System.Threading.Tasks;
using System.Net;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using PDFDict.SDK.Sharp.Core.Contents;
using System.Text;
using Img2table.Sharp.Tabular;
using img2table.sharp.Img2table.Sharp.Data;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace img2table.sharp.web.Services
{
    public class PDFContentExtractor
    {
        public float RenderDPI { get; set; } = 300;
        public float PredictConfidenceThreshold { get; set; } = 0.5f;

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
        private bool _debug_save_dectect_image = true;

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

        private async Task<LayoutDetectionResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName)
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

            LayoutDetectionResult detectResult = await DetectAsync(pdfFileBytes, pdfFileName);
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

        private IDictionary<int, string> RenderTableBorderEnhanced(string pdfFile, LayoutDetectionResult detectResult, string workFolder)
        {
            var tableImageDict = new Dictionary<int, string>();
            var pagesWithTable = ExtractUtils.GetPagesWithTableChunks(detectResult);
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
            LayoutDetectionResult detectResult, string jobFolderName, ConcurrentBag<PagedChunk> pagedChunks, IDictionary<int, string> tableEnhancedImagePathDict)
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
                    filteredChunks = ChunkUtils.RebuildReadingOrder(filteredChunks, RenderDPI); // TODO

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
                        ExtractUtils.DrawPageChunks(pageImagePath, filteredChunks);
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

        private IList<ChunkElement> BuildPageChunks(PDFDocument pdfDoc, PDFPage pdfPage, string workFolder, string pageImagePath, string tableEnhancedImagePath, IEnumerable<ObjectDetectionResult> filteredChunkObjects, float ratio)
        {
            var pageThread = pdfPage.BuildPageThread();
            var textThread = pageThread.GetTextThread();

            var chunks = new List<ChunkElement>();
            var pageElements = TransPageElements(pageThread.GetContentList(), ratio, pdfPage.GetPageHeight());

            if (_debug_draw_text_box)
            {
                ExtractUtils.DrawPageElements(pageImagePath, pageElements);
            }

            foreach (var chunkObject in filteredChunkObjects)
            {
                var chunkType = DetectionLabel.MappingLabel(chunkObject.Label);
                if (chunkType == DetectionLabel.Unknown)
                {
                    continue;
                }

                var chunkBox = RectangleF.FromLTRB((float)chunkObject.BoundingBox[0], (float)chunkObject.BoundingBox[1], (float)chunkObject.BoundingBox[2], (float)chunkObject.BoundingBox[3]);
                var contentElements = ExtractUtils.FindContentElementsInBox(chunkBox, pageElements);

                var chunkElement = new ChunkElement
                {
                    ChunkObject = chunkObject,
                    ContentElements = contentElements
                };

                bool isTableProcessed = false;
                if (chunkType == DetectionLabel.Table)
                {
                    if (/*DrawTableChunk*/ false)
                    {
                        ExtractUtils.DrawTableChunk(pageImagePath, chunkObject.Cells);
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
                                chunkElement.ChunkObject.Label = DetectionLabel.Image;
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

                                    contentElements = ExtractUtils.FindContentElementsInBox(region, pageElements);
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
            var pagedTable = imageTabular.Process(tableImagePath, chunkBox, ExtractUtils.GetTextBoxes(contentElements), false);

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
                chunkElement.ChunkObject.Label = DetectionLabel.Text;
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
    }
}
