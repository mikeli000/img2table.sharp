using Microsoft.AspNetCore.Mvc;
using img2table.sharp.web.Models;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using PDFDict.SDK.Sharp.Core;
using System.Drawing;
using PDFDict.SDK.Sharp.Core.Contents;
using Img2table.Sharp.Tabular.TableImage.TableElement;
using System;
using System.Linq;
using OpenCvSharp;

namespace img2table.sharp.web.Controllers
{
    [ApiController]
    [Route("api/extract")]
    public class ExtractController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ExtractController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromForm] IFormFile uploadFile)
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

            PDFContentExtractor extractor = new PDFContentExtractor(_httpClientFactory);
            await extractor.ExtractAsync(fileBytes, uploadFile.FileName);

            return Ok(uploadFile.FileName);
        }
    }
}
