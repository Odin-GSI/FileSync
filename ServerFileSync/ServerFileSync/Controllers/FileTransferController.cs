using ServerFileSync.Interfaces;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Http;

namespace ServerFileSync.Controllers
{
    public class FileTransferController : ApiController
    {
        private string _root;
        private IFileManager _fileManager;
        private IFileNotifier _hubWrapper;

        public FileTransferController()
        {
            _root = ConfigurationManager.AppSettings["SyncFolder"].ToString();
            _fileManager = new FileSystemFileManager(_root);
            _hubWrapper = FileSyncHubWrapper.Instance;
        }

        /// <summary>
        /// Constructor for Unit Testing
        /// </summary>
        /// <param name="fileManager"></param>
        /// <param name="hubWrapper"></param>
        /// <param name="root"></param>
        public FileTransferController(IFileManager fileManager, IFileNotifier hubWrapper, string root)
        {
            _fileManager = fileManager;
            _hubWrapper = hubWrapper;
            _root = root;
        }

        [HttpPost]
        public HttpResponseMessage Overwrite()
        {
            var fileName = Request.Content.ReadAsStringAsync().Result;

            try
            {
                _fileManager.Move(getTempFileName(fileName), fileName);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        [HttpDelete]
        public HttpResponseMessage DeletePartial(string fileName)
        {
            try
            {
                _fileManager.Delete(getTempFileName(fileName));

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Upload()
        {
            HttpRequestMessage request = this.Request;
            if (!request.Content.IsMimeMultipartContent())
            {
                return new HttpResponseMessage(HttpStatusCode.UnsupportedMediaType);
            }

            var multiContents = await request.Content.ReadAsMultipartAsync();

            try
            {
                string fileName = await GetFileNameFromRequest(multiContents);

                byte[] fileBytes = await GetFileBytesFromRequest(multiContents);

                if (_fileManager.Exists(fileName))
                {
                    _fileManager.Save(getTempFileName(fileName), fileBytes);
                    return new HttpResponseMessage(HttpStatusCode.Ambiguous);
                }
                else
                {
                    _fileManager.Save(fileName, fileBytes);

                    //Calculate CRC
                    string CRC = getHash(fileName);

                    //Notify New File            
                    _hubWrapper.NotifyNewFile(fileName, CRC);

                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
            }
            catch (HttpResponseException)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        private string getHash(string fileName)
        {
            string CRC;
            using (var md5 = MD5.Create())
            {
                using (var stream = _fileManager.GetStream(fileName))
                {
                    CRC = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
            }

            return CRC;
        }

        private string getTempFileName(string fileName)
        {
            string tempPart = "_partial";
            var dotIndex = fileName.LastIndexOf('.');
            if (dotIndex >= 0)
                return fileName.Insert(dotIndex, tempPart);
            else
                return fileName + tempPart;
        }

        [HttpDelete]
        public HttpResponseMessage Delete(string filename, string extension)
        {
            try
            {
                if (String.IsNullOrEmpty(filename))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                _fileManager.Delete(filename + "." + extension);
                _hubWrapper.NotifyDeleteFile(filename + "." + extension);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (IOException excp)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("Problems while deleting file. " + excp.Message) };
            }
            catch (Exception excp)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(excp.Message) };
            }
        }

        private async Task<byte[]> GetFileBytesFromRequest(MultipartMemoryStreamProvider multiContents)
        {
            byte[] fileBytes;
            if (multiContents.Contents.Count > 1)
            {
                //The file as byte array
                fileBytes = await multiContents.Contents[1].ReadAsByteArrayAsync();
                if (fileBytes.Length <= 0)
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }
            else
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            return fileBytes;
        }

        private async Task<string> GetFileNameFromRequest(MultipartMemoryStreamProvider multiContents)
        {
            string fileName;
            if (multiContents.Contents.Count > 0)
            {
                //Filename as string content
                fileName = await multiContents.Contents[0].ReadAsStringAsync();
                if (String.IsNullOrEmpty(fileName))
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }
            else
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            return fileName;
        }

        [HttpGet]
        public HttpResponseMessage Exists(string fileName)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (String.IsNullOrEmpty(fileName))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }
            else
            {
                response.Content = new StringContent(_fileManager.Exists(fileName) ? "true" : "false");
                return response;
            }
        }

        [HttpGet]
        public HttpResponseMessage Download(string fileName)
        {
            FileStream sourceStream = _fileManager.GetStream(fileName);

            HttpResponseMessage fullResponse = Request.CreateResponse(HttpStatusCode.OK);
            fullResponse.Content = new StreamContent(sourceStream);

            fullResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return fullResponse;
        }
    }
}
