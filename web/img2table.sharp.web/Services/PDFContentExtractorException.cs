using System.Net;
using System;

namespace img2table.sharp.web.Services
{
    public class PDFContentExtractorException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ResponseContent { get; }

        public PDFContentExtractorException(HttpStatusCode statusCode, string content)
            : base($"PDF content extraction failed with status code {(int)statusCode}: {content}")
        {
            StatusCode = statusCode;
            ResponseContent = content;
        }
    }
}
