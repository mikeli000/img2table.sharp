using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using img2table.sharp.web.Services;

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
        public async Task<IActionResult> Post([FromForm] IFormFile uploadFile, [FromForm] bool useEmbeddedHtml = false, [FromForm] bool ignoreMarginalia = false, [FromForm] string docType = "slide")
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

            PDFContentExtractor extractor = new PDFContentExtractor(_httpClientFactory, _rootFolder, useEmbeddedHtml, ignoreMarginalia, docType);
            var res = await extractor.ExtractAsync(fileBytes, uploadFile.FileName);

            return Ok(res);
        }
    }
}
