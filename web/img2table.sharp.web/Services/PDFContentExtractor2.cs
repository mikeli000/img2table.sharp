using img2table.sharp.web.Models;
using PDFDict.SDK.Sharp.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.web.Services
{
    public class PDFContentExtractor2
    {
        private float _renderDPI { get; set; } = ExtractOptions.DEFAULT_RENDER_RESOLUTION;
        private float _predictConfidenceThreshold = ExtractOptions.PREDICT_CONFIDENCE_THRESHOLD;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _rootFolder;

        private ChunkElementProcessor _chunkElementProcessor;
        private ExtractOptions _extractOptions;

        public PDFContentExtractor2(IHttpClientFactory httpClientFactory, string rootFolder, ExtractOptions extractOptions)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _rootFolder = rootFolder ?? throw new ArgumentNullException(nameof(rootFolder));

            _extractOptions = _extractOptions ?? new ExtractOptions();
        }

        private async Task<LayoutDetectionResult> DetectAsync(byte[] imageBytes, string pdfFileName, int pageIndex)
        {
            var detector = new LayoutDetectorFactory(_httpClientFactory).Create(_extractOptions.DocCategory);
            if (detector == null)
            {
                throw new InvalidOperationException($"No layout detector found for category: {_extractOptions.DocCategory}");
            }
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("PDF file bytes cannot be null or empty.", nameof(imageBytes));
            }

            return await detector.DetectPageImageAsync(imageBytes, pageIndex, pdfFileName, _renderDPI, _predictConfidenceThreshold);
        }

        public async Task<DocumentChunks> ExtractAsync(byte[] pdfFileBytes, string pdfFileName)
        {
            var stopwatch = Stopwatch.StartNew();

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

            //LayoutDetectionResult detectResult = await DetectAsync(pdfFileBytes, pdfFileName);
            //if (detectResult == null)
            //{
            //    throw new PDFContentExtractorException(HttpStatusCode.InternalServerError, "Layout detection failed.");
            //}
            //var tableEnhancedImagePathDict = RenderTableBorderEnhanced(pdfFile, detectResult, workFolder);

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
                partition[threadIndex].Add(new(i, pdfDoc.LoadPage(i)));
            }

            return partition;
        }

        private readonly object _renderLock = new object();
    }
}
