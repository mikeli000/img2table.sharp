using img2table.sharp.web.Models;
using img2table.sharp.web.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace img2table.sharp.api.sample
{
    public class ExtractAPISample
    {
        //public static string baseUrl = "http://20.187.144.96:5000";
        public static string baseUrl = "https://localhost:8876";
        public static readonly string ApiUrl = $"{baseUrl}/api/extract";

        public static async Task<DocumentChunks> ExtractAsync(byte[] fileBytes, string fileName, bool useEmbeddedHtml = false,
            bool ignoreMarginalia = false, bool autoOCR = false, bool embedImagesAsBase64 = true, string docType = "slide")
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(10);
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                    content.Add(fileContent, "uploadFile", fileName);
                    content.Add(new StringContent(useEmbeddedHtml.ToString()), "useEmbeddedHtml");
                    content.Add(new StringContent(ignoreMarginalia.ToString()), "ignoreMarginalia");
                    content.Add(new StringContent(autoOCR.ToString()), "autoOCR");
                    content.Add(new StringContent(embedImagesAsBase64.ToString()), "embedImagesAsBase64");
                    content.Add(new StringContent(docType), "docType");

                    var response = await httpClient.PostAsync(ApiUrl, content);
                    response.EnsureSuccessStatusCode();

                    var responseContent = await response.Content.ReadAsStringAsync();
                    var documentChunks = System.Text.Json.JsonSerializer.Deserialize<DocumentChunks>(responseContent);
                    return documentChunks;
                }
            }
        }

        public static async Task DownloadMarkdownAsync(DocumentChunks documentChunks, string dstFile)
        {
            var url = $@"${baseUrl}/job/${documentChunks.JobId}/${documentChunks.DocumentName}.md";

            Console.WriteLine($"Markdown URL: {url}");
            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
                var markdownContent = await response.Content.ReadAsStringAsync();
                File.WriteAllText(dstFile, markdownContent);
            }
        }
    }
}
