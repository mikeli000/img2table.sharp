using img2table.sharp.web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace img2table.sharp.web.Services
{
    public class LayoutDetectorFactory
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public class DocumentCategory
        {
            public const string SlideLike = "slide";
            public const string AcademicPaper = "academic";
            public const string PPDocLayoutPlusL = "PP-DocLayout_plus-L";
            public const string PPDocLayoutPlusFull = "PP-DocLayout_plus-full";
        }

        public LayoutDetectorFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public LayoutDetector Create(string category)
        {
            if (category == DocumentCategory.SlideLike)
            {
                return new Yolov8xDoclaynetLayoutDetector(_httpClientFactory);
            }
            else if (category == DocumentCategory.AcademicPaper)
            {
                return new DocStructBenchLayoutDetector(_httpClientFactory);
            }
            else if (category == DocumentCategory.PPDocLayoutPlusL)
            {
                return new PPDocLayoutPlusLLayoutDetector(_httpClientFactory);
            }
            else if (category == DocumentCategory.PPDocLayoutPlusFull)
            {
                return new PPDocLayoutPlusLLayoutFullDetector(_httpClientFactory);
            }

            return new Yolov8xDoclaynetLayoutDetector(_httpClientFactory);
        }
    }

    public interface LayoutDetector
    {
        Task<ChunkResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName, float renderDPI, float predictConfidenceThreshold);
    }

    public class Yolov8xDoclaynetLayoutDetector: LayoutDetector
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly string DocumentLayoutExtractorServiceUrl = "http://localhost:8000/detect";

        public Yolov8xDoclaynetLayoutDetector(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ChunkResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName, float renderDPI, float predictConfidenceThreshold)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var formData = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(pdfFileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            formData.Add(fileContent, "file", pdfFileName);

            formData.Add(new StringContent(renderDPI + ""), "dpi");
            formData.Add(new StringContent(predictConfidenceThreshold + ""), "confidence");
            var response = await httpClient.PostAsync(DocumentLayoutExtractorServiceUrl, formData);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new PDFContentExtractorException(response.StatusCode, errorContent);
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var detectResult = JsonSerializer.Deserialize<ChunkResult>(jsonResponse);

            return detectResult;
        }
    }

    public class DocStructBenchLayoutDetector : LayoutDetector
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly string DocumentLayoutExtractorServiceUrl = "http://localhost:8999/detect";

        public DocStructBenchLayoutDetector(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ChunkResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName, float renderDPI, float predictConfidenceThreshold)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var formData = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(pdfFileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            formData.Add(fileContent, "file", pdfFileName);

            formData.Add(new StringContent(renderDPI + ""), "dpi");
            formData.Add(new StringContent(predictConfidenceThreshold + ""), "confidence");
            var response = await httpClient.PostAsync(DocumentLayoutExtractorServiceUrl, formData);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new PDFContentExtractorException(response.StatusCode, errorContent);
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var detectResult = JsonSerializer.Deserialize<ChunkResult>(jsonResponse);

            return detectResult;
        }
    }

    public class PPDocLayoutPlusLLayoutDetector : LayoutDetector
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly string DocumentLayoutExtractorServiceUrl = "http://localhost:8099/detect";

        public PPDocLayoutPlusLLayoutDetector(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ChunkResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName, float renderDPI, float predictConfidenceThreshold)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var formData = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(pdfFileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            formData.Add(fileContent, "file", pdfFileName);

            formData.Add(new StringContent(renderDPI + ""), "dpi");
            formData.Add(new StringContent(predictConfidenceThreshold + ""), "confidence");
            var response = await httpClient.PostAsync(DocumentLayoutExtractorServiceUrl, formData);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new PDFContentExtractorException(response.StatusCode, errorContent);
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var detectResult = JsonSerializer.Deserialize<ChunkResult>(jsonResponse);

            return detectResult;
        }
    }

    public class PPDocLayoutPlusLLayoutFullDetector : LayoutDetector
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private static readonly string DocumentLayoutExtractorServiceUrl = "http://localhost:8099/doc-layout";

        public PPDocLayoutPlusLLayoutFullDetector(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ChunkResult> DetectAsync(byte[] pdfFileBytes, string pdfFileName, float renderDPI, float predictConfidenceThreshold)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(15);
            using var formData = new MultipartFormDataContent();

            var fileContent = new ByteArrayContent(pdfFileBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            formData.Add(fileContent, "file", pdfFileName);

            formData.Add(new StringContent(renderDPI + ""), "dpi");
            formData.Add(new StringContent(predictConfidenceThreshold + ""), "confidence");
            var response = await httpClient.PostAsync(DocumentLayoutExtractorServiceUrl, formData);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new PDFContentExtractorException(response.StatusCode, errorContent);
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var detectionResults = JsonSerializer.Deserialize<List<DetectionResult>>(jsonResponse);

            var detectResult = MappingToChunkResult(detectionResults);
            return detectResult;
        }

        private static ChunkResult MappingToChunkResult(List<DetectionResult> detectionResults)
        {
            var chunkResult = new ChunkResult();
            
            var pageChunks = new List<PageChunk>();
            foreach (var detectionResult in detectionResults)
            {
                PageChunk pageChunk = new PageChunk();
                pageChunk.Page = detectionResult.Page;
                var objects = new List<ChunkObject>();

                if (detectionResult.DetectionObjectResult?.ObjectResults != null)
                {
                    int tableIndex = 0;
                    foreach (var obj in detectionResult.DetectionObjectResult?.ObjectResults)
                    {
                        var chunkObj = new ChunkObject();
                        objects.Add(chunkObj);
                        chunkObj.Label = obj.Label;
                        chunkObj.BoundingBox = obj.BoundingBox?.Select(b => (double)b).ToArray();

                        if (chunkObj.Label.ToLower() == "table")
                        {
                            var tables = detectionResult.DetectionObjectResult?.TableResListResults;
                            if (tables != null && tables.Count() > tableIndex)
                            {
                                var cells = new List<TableCellChunk>();
                                var table = tables.ElementAt(tableIndex);

                                foreach (var cell in table.FlatCells)
                                {
                                    var tableCellChunk = new TableCellChunk();
                                    tableCellChunk.BoundingBox = new int[] {
                                        (int)cell[0], (int)cell[1],
                                        (int)cell[2], (int)cell[3]
                                    };
                                    cells.Add(tableCellChunk);
                                }

                                chunkObj.Cells = cells;
                                tableIndex++;
                            }
                        }
                    }
                }
                pageChunk.Objects = objects;
                pageChunks.Add(pageChunk);
            }

            chunkResult.Results = pageChunks;
            return chunkResult;
        }

        public class DetectionResult
        {
            [JsonPropertyName("page")]
            public int? Page { get; set; }

            [JsonPropertyName("detections")]
            public DetectionObjectResult DetectionObjectResult { get; set; }
        }

        public class DetectionObjectResult
        {
            [JsonPropertyName("parsing_res_list")]
            public IEnumerable<ObjectResult> ObjectResults { get; set; }

            [JsonPropertyName("table_res_list")]
            public IEnumerable<TableResListResult> TableResListResults { get; set; }

            //[JsonPropertyName("statistics")]
            //public DetectionStatisticsResult StatisticsResult { get; set; }
        }

        public class DetectionStatisticsResult
        {
            [JsonPropertyName("total_elements")]
            public int TotalElements { get; set; }

            [JsonPropertyName("element_counts")]
            public IDictionary<string, int> ElementCounts { get; set; } = new Dictionary<string, int>();
        }

        public class ObjectResult
        {
            [JsonPropertyName("block_label")]
            public string Label { get; set; }

            [JsonPropertyName("block_bbox")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public int[] BoundingBox { get; set; }

            [JsonPropertyName("block_content")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public string Content { get; set; } = string.Empty;

            [JsonIgnore]
            public int X0 => BoundingBox[0];
            [JsonIgnore]
            public int Y0 => BoundingBox[1];
            [JsonIgnore]
            public int X1 => BoundingBox[2];
            [JsonIgnore]
            public int Y1 => BoundingBox[3];
        }

        public class TableResListResult
        {
            //[JsonPropertyName("table_id")]
            //public int? TableId { get; set; }

            [JsonPropertyName("cell_box_list")]
            public IEnumerable<double[]> Cells { get; set; }

            [JsonIgnore]
            public IEnumerable<double[]> FlatCells
            {
                get
                {
                    return Cells?.Select(c => c);
                }
            }

            //[JsonPropertyName("structure")]
            //public TableStructureResult Structure { get; set; }
        }

        public class TableStructureResult
        {
            [JsonPropertyName("rows")]
            public IEnumerable<TableRowResult> Rows { get; set; }
        }

        public class TableRowResult
        {
            [JsonPropertyName("row_id")]
            public int? RowId { get; set; }

            [JsonPropertyName("cells")]
            public IEnumerable<TableCellResult> Cells { get; set; }
        }

        public class TableCellResult
        {
            //[JsonPropertyName("cell_id")]
            //public int? CellId { get; set; }

            //[JsonPropertyName("content")]
            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            //public string Content { get; set; } = string.Empty;

            //[JsonPropertyName("rowspan")]
            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            //public int? RowSpan { get; set; } = 1;

            //[JsonPropertyName("colspan")]
            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            //public int? ColSpan { get; set; } = 1;

            [JsonPropertyName("bbox")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public int[] BoundingBox { get; set; }
        }
    }
}
