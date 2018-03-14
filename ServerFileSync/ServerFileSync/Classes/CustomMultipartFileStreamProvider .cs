using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;

namespace ServerFileSync
{
    public class CustomMultipartFormDataStreamProvider : MultipartFormDataStreamProvider
    {
        private string _originalFileName = "";
        public CustomMultipartFormDataStreamProvider(string path) : base(path) { }

        // MultipartFormDataStreamProvider by default sets a Body-Part-Guid name to the file, to get the filaName from ContentDisposition this override is needed.
        public override string GetLocalFileName(HttpContentHeaders headers)
        {
            var name = !string.IsNullOrWhiteSpace(headers.ContentDisposition.FileName) ? headers.ContentDisposition.FileName : Guid.NewGuid().ToString();
            _originalFileName = name.Replace("\"", string.Empty);
            return _originalFileName;
        }

        public string GetOriginalFileName { get => _originalFileName; }
    }
}