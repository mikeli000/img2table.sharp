using img2table.sharp.web.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
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
}
