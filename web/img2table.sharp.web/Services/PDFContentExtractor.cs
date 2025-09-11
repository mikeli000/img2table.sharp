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
        private float _renderDPI { get; set; } = ExtractOptions.DEFAULT_RENDER_RESOLUTION;
        private float _predictConfidenceThreshold = ExtractOptions.PREDICT_CONFIDENCE_THRESHOLD;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _rootFolder;
        
        private ChunkElementProcessor _chunkElementProcessor;
        private ExtractOptions _extractOptions;

        public PDFContentExtractor(IHttpClientFactory httpClientFactory, string rootFolder, ExtractOptions extractOptions)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _rootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));

            _extractOptions = _extractOptions?? new ExtractOptions();
        }

        private async Task<LayoutDetectionResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName)
        {
            var detector = new LayoutDetectorFactory(_httpClientFactory).Create(_extractOptions.DocCategory);
            if (detector == null)
            {
                throw new InvalidOperationException($"No layout detector found for category: {_extractOptions.DocCategory}");
            }
            if (pdfFileBytes == null || pdfFileBytes.Length == 0)
            {
                throw new ArgumentException("PDF file bytes cannot be null or empty.", nameof(pdfFileBytes));
            }

            return await detector.DetectAsync(pdfFileBytes, pdfFileName, _renderDPI, _predictConfidenceThreshold);
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
                UseEmbeddedHtml = _extractOptions.UseEmbeddedHtml,
                IgnoreMarginalia = _extractOptions.IgnoreMarginalia,
                OutputFigureAsImage = _extractOptions.OutputFigureAsImage,
                EnableOCR = _extractOptions.EnableOCR,
                EmbedBase64ImageData = _extractOptions.EmbedImagesAsBase64,
            };
            _chunkElementProcessor = new ChunkElementProcessor(workFolder, jobFolderName, chunkElementProcessorParameter);
            string pdfFile = Path.Combine(workFolder, pdfFileName);
            await File.WriteAllBytesAsync(pdfFile, pdfFileBytes);

            var tableEnhancedImagePathDict = ExtractUtils.RenderTableBorderEnhanced(pdfFile, detectResult, workFolder, _renderDPI);

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
                        pdfDoc.RenderPage(pageImagePath, pageIdx, _renderDPI, backgroundColor: Color.White);
                    }

                    var predictedPageChunks = detectResult?.Results?.FirstOrDefault(r => r.Page == pageIdx + 1);
                    var filteredChunks = ChunkUtils.FilterOverlapping(predictedPageChunks.Objects, ExtractOptions.PREDICT_CONFIDENCE_THRESHOLD_TABLE);
                    filteredChunks = ChunkUtils.FilterContainment(filteredChunks);
                    filteredChunks = ChunkUtils.RebuildReadingOrder(filteredChunks, _renderDPI); // TODO

                    var chunks = BuildPageChunks(pdfDoc, page, workFolder, pageImagePath, tableEnhancedImagePath, filteredChunks, _renderDPI / 72f);
                    var pageChunks = new PagedChunk
                    {
                        PageNumber = pageNumber,
                        PreviewImagePath = $"{WorkDirectoryOptions.RequestPath}/{jobFolderName}/{pageImageName}",
                        Chunks = chunks
                    };
                    pagedChunks.Add(pageChunks);

                    if (ExtractDebugOptions._debug_draw_page_chunks)
                    {
                        ExtractUtils.DrawPageChunks(pageImagePath, filteredChunks);
                    }
                    if (ExtractDebugOptions._debug_save_dectect_image)
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
            var pageElements = ExtractUtils.TransPageElements(pageThread.GetContentList(), ratio, pdfPage.GetPageHeight());

            if (ExtractDebugOptions._debug_draw_text_box)
            {
                ExtractUtils.DrawPageElements(pageImagePath, pageElements);
            }

            foreach (var chunkObject in filteredChunkObjects)
            {
                var chunkType = DetectionLabel.NormalizeLabel(chunkObject.Label);
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
                                TabularPDF(param, tableImagePath, chunkBox, contentElements, pdfDoc, pdfPage, ratio, chunkElement, _extractOptions.UseEmbeddedHtml);
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
                                    TabularPDF(param, tableImagePath, region, contentElements, pdfDoc, pdfPage, ratio, chunkElement, _extractOptions.UseEmbeddedHtml);
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

    }
}
