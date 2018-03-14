using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace ServerFileSync.Controllers
{
    public class FileTransferController : ApiController
    {
        private string _root = ConfigurationManager.AppSettings["SyncFolder"].ToString();
        private FileSystemFileManager _fileManager;

        public FileTransferController()
        {
            _fileManager =  new FileSystemFileManager(_root);
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Upload()
        {
            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            var multiContents = await request.Content.ReadAsMultipartAsync();

            //Filename as string content
            string fileName = await multiContents.Contents[0].ReadAsStringAsync();

            //The file as byte array
            byte[] fileBytes = await multiContents.Contents[1].ReadAsByteArrayAsync();

            //File.WriteAllBytes(_root+"\\"+fileName, fileBytes);
            _fileManager.Save(fileName, fileBytes);

            //var provider = new CustomMultipartFormDataStreamProvider(_root);

            //var task = request.Content.ReadAsMultipartAsync(provider).
            //    ContinueWith<HttpResponseMessage>(o =>
            //    {
            //        // this is the file name on the server where the file was saved 
            //        string fileName = provider.FileData.First().LocalFileName;
            //        //Stream s = provider.Contents.FirstOrDefault().ReadAsStreamAsync().Result;
            //        //byte[] b = new byte[s.Length];
            //        //s.Read(b, 0, (int)s.Length);

            //        //File.WriteAllBytes(root + "\\" + fileName, b);

            //        return new HttpResponseMessage()
            //        {
            //            Content = new StringContent("File uploaded.")
            //        };
            //    }
            //);

            //Calculate CRC *************************************************************************** TO DO
            string CRC = "";

            //Notify New File
            var hub = FileSyncHubWrapper.Instance;
            //hub.NotifyNewFile(provider.GetOriginalFileName, CRC);
            hub.NotifyNewFile(fileName, CRC);

            //return task;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpGet]
        public bool Exists(string fileName)
        {
            return _fileManager.Exists(fileName);
            //return File.Exists(_root + "\\" + fileName);
        }

        [HttpGet]
        public HttpResponseMessage Download(string fileName)
        {
            //FileStream sourceStream = File.Open(_root + "\\" + fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            FileStream sourceStream = _fileManager.GetStream(fileName);

            HttpResponseMessage fullResponse = Request.CreateResponse(HttpStatusCode.OK);
            fullResponse.Content = new StreamContent(sourceStream);

            fullResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return fullResponse;
        }
    }
}
