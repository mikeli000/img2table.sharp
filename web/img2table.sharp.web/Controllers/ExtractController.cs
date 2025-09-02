using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using img2table.sharp.web.Services;
using static img2table.sharp.web.Services.LayoutDetectorFactory;

namespace img2table.sharp.web.Controllers
{
    [ApiController]
    [Route("api/extract")]
    public class ExtractController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _rootFolder;

        public ExtractController(IHttpClientFactory httpClientFactory, IOptions<WorkDirectoryOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _rootFolder = options.Value.RootFolder;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] IFormFile uploadFile, [FromForm] bool useEmbeddedHtml = false, 
            [FromForm] bool ignoreMarginalia = false, [FromForm] bool autoOCR = false,  [FromForm] bool embedImagesAsBase64 = false, [FromForm] string docType = "slide")
        {
            if (uploadFile == null || uploadFile.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await uploadFile.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            docType = DocumentCategory.PPDocLayoutPlusL;
            ExtractOptions extractOptions = new ExtractOptions
            {
                UseEmbeddedHtml = useEmbeddedHtml,
                IgnoreMarginalia = ignoreMarginalia,
                OutputFigureAsImage = true,
                DocCategory = docType,
                EnableOCR = autoOCR,
                EmbedImagesAsBase64 = embedImagesAsBase64
            };

            PDFContentExtractor extractor = new PDFContentExtractor(_httpClientFactory, _rootFolder, extractOptions);
            var res = await extractor.ExtractAsync(fileBytes, uploadFile.FileName);

            return Ok(res);
        }
    }
}
