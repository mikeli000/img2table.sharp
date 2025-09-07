using img2table.sharp.web.Services;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text;
using System.Linq;

namespace img2table.sharp.web.Models
{
    public class ChunkElement
    {
        [JsonPropertyName("chunkObject")]
        public ObjectDetectionResult ChunkObject { get; set; }

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
}
