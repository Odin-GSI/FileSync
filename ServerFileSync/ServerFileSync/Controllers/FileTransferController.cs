using ServerFileSync.Interfaces;
using ServerFileSync.Models;
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
        #region Class vars
        private string _syncFolder;
        private IFileManager _fileManager;
        private IFileNotifier _hubWrapper;
        #endregion Class vars

        #region Constructors
        /// <summary>
        /// Public Constructor
        /// </summary>
        public FileTransferController()
        {
            _syncFolder = ConfigurationManager.AppSettings["SyncFolder"].ToString();
            _fileManager = new FileSystemFileManager(_syncFolder);
            _hubWrapper = FileSyncHubWrapper.Instance;
        }

        /// <summary>
        /// Constructor for Unit Testing
        /// </summary>
        /// <param name="fileManager"></param>
        /// <param name="hubWrapper"></param>
        /// <param name="syncFolder"></param>
        public FileTransferController(IFileManager fileManager, IFileNotifier hubWrapper, string syncFolder)
        {
            _fileManager = fileManager;
            _hubWrapper = hubWrapper;
            _syncFolder = syncFolder;
        }
        #endregion Constructors

        #region Public methods

        /// <summary>
        /// Creates a file based on the temporary previously uploaded one
        /// Requires the temporary Guid as String in the HttpRequest Content
        /// Generates SignalR NotifyNewFile on success
        /// </summary>
        /// <param name="fileName">Original name of the file</param>
        /// <returns>HttpResponseMessage</returns>
        [HttpPost]
        public HttpResponseMessage ConfirmUpload(string fileName)
        {
            Guid tempGuid;
            try
            {
                string tempGuidstring = Request.Content.ReadAsStringAsync().Result;
                tempGuid = new Guid(tempGuidstring);
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (!_fileManager.ExistsTemp(fileName,tempGuid))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            try
            {
                _fileManager.ConfirmSave(fileName,tempGuid);
                string CRC = _fileManager.GetHash(fileName);
                _hubWrapper.NotifyNewFile(fileName, CRC);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Deletes the temporary file asociated with the provided fileName
        /// Requires the temporary Guid as String in the HttpRequest Content
        /// </summary>
        /// <param name="fileName">Original name of the file</param>
        /// <returns></returns>
        [HttpPut]
        public HttpResponseMessage DeleteTemp(string fileName)
        {
            Guid tempGuid;
            try
            {
                string tempGuidstring = Request.Content.ReadAsStringAsync().Result;
                tempGuid = new Guid(tempGuidstring);
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            
            if (!_fileManager.ExistsTemp(fileName, tempGuid))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            try
            {
                _fileManager.DeleteTemp(fileName,tempGuid);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Uploads a file
        /// If the file to overwrite is different than the expected, keeps the uploaded file with a temporary name generated with a Guid. Need to complete the operation calling ConfirmUpload or DeleteTemp
        /// The generated Guid is returned as a String Content in the HttpResponse
        /// Expected ordered Request Content:
        /// Filename as string
        /// Previous file hash as string
        /// File content as byte array
        /// </summary>
        /// <returns>
        /// HttpResponseMessage
        /// The generated Guid as String Content in the HttpResponse
        /// HttpResponse HttpStatusCode:
        /// OK: No file existed with that name, Upload confirmed. Generates SignalR NotifyNewFile
        /// Accepted: The file to overwrite was the expected one, Upload confirmed. Generates SignalR NotifyNewFile
        /// NotModified: A file with same name and hash existed, no need to Upload
        /// Ambiguous: A file with same name exists, but different hash than the expected and the uploaded one. File was kept with a temporary name. Guid returned. Need to call ConfirmUpload to overwrite, or DeleteTemp to cancel
        /// </returns>
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
                string fileName = await getStringDataFromRequest(multiContents,0);
                string previousHash = await getStringDataFromRequest(multiContents, 1);
                byte[] fileBytes = await getFileBytesFromRequest(multiContents);
                
                HttpResponseMessage myResponse = new HttpResponseMessage();

                //Check original file existance at the end of the temporal copy
                if (_fileManager.Exists(fileName))
                {
                    //If the existing File has the same Hash, nothing needs to be done, the updated file was already uploaded
                    if (_fileManager.SameHash(fileName, fileBytes))
                        return new HttpResponseMessage(HttpStatusCode.NotModified);

                    string CRC = _fileManager.GetHash(fileName);

                    //If the existing File has the client expected Hash, automatically ConfirmUpload
                    if (CRC.Equals(previousHash))
                    {
                        _fileManager.Save(fileName, fileBytes, false);
                        _hubWrapper.NotifyNewFile(fileName, _fileManager.GetHash(fileName)); //Send the updated Hash
                        myResponse.StatusCode = HttpStatusCode.Accepted;
                        return myResponse;
                    }
                }
                else //File is New, automatically ConfirmUpload
                {
                    _fileManager.Save(fileName, fileBytes, false);
                    _hubWrapper.NotifyNewFile(fileName, _fileManager.GetHash(fileName));
                    myResponse.StatusCode = HttpStatusCode.OK;
                    return myResponse;
                }

                //File exists on Server, but Hash is different than expected. User must decide and call ConfirmUpload or DeleteTemp. Temp Guid returned.
                myResponse.StatusCode = HttpStatusCode.Ambiguous;
                Guid tempGuid = _fileManager.Save(fileName, fileBytes);
                myResponse.Content = new StringContent(tempGuid.ToString());
                myResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

                return myResponse;
            }
            catch (HttpResponseException)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
            catch (Exception e)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Deletes a file
        /// Generates SignalR NotifyDeleteFile on success
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        [HttpDelete]
        public HttpResponseMessage Delete(string filename, string previousHash)
        {
            try
            {
                if (String.IsNullOrEmpty(filename))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);

                if (String.IsNullOrEmpty(previousHash)||(_fileManager.GetHash(filename).Equals(previousHash)))
                {
                    string hash = _fileManager.GetHash(filename);
                    _fileManager.Delete(filename);
                    _hubWrapper.NotifyDeleteFile(filename, hash);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                //File intended to Delete was modified. Must confirm Delete calling it with previousHash empty.
                return new HttpResponseMessage(HttpStatusCode.Ambiguous);
            }
            catch (IOException excp)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("There was a problem deleting the file. " + excp.Message) };
            }
            catch (Exception excp)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(excp.Message) };
            }
        }

        /// <summary>
        /// Checks the existance of a file
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>
        /// String Content: true if name exists, false if it doesn't
        /// HttpStatusCode:
        /// Ok: if file exists with given name and hash,
        /// Ambiguous: if file exists with given name but different hash.
        /// </returns>
        [HttpGet]
        public HttpResponseMessage Exists(string fileName/*, string hash*/)
        {
            if (String.IsNullOrEmpty(fileName))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            bool exists = _fileManager.Exists(fileName);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent( exists? "true" : "false");

            /*
            //if file exists but has different hash
            if (exists && !_fileManager.GetHash(fileName).Equals(hash))
                response.StatusCode = HttpStatusCode.Ambiguous;
            */

            return response;
        }

        /// <summary>
        /// Download the requested file
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Stream with the file content as Stream Content in the HttpResponse</returns>
        [HttpGet]
        public HttpResponseMessage Download(string fileName)
        {
            FileStream sourceStream = _fileManager.GetStream(fileName);

            HttpResponseMessage fullResponse = Request.CreateResponse(HttpStatusCode.OK);
            fullResponse.Content = new StreamContent(sourceStream);

            fullResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return fullResponse;
        }

        [HttpGet]
        public HttpResponseMessage GetFolderStatus()
        {
            List<FolderFileState> remoteFiles = new List<FolderFileState>();
            var files = _fileManager.GetFilenames();

            foreach (string file in files)
                remoteFiles.Add(new FolderFileState()
                        .FileName(file)
                        .Hash(_fileManager.GetHash(file))
                        .CurrentStatus(FileStatusType.Synced));

            FolderState folderState = new FolderState()
                .RemotePath(_syncFolder);

            foreach (var f in remoteFiles)
                folderState.RemoteFile(f);

            string s = folderState.Definition;

            var resp = new HttpResponseMessage { Content = new StringContent(s, System.Text.Encoding.UTF8, "application/xml") };
            return resp;
        }

        #endregion Public methods

        #region Private methods
        private async Task<byte[]> getFileBytesFromRequest(MultipartMemoryStreamProvider multiContents)
        {
            byte[] fileBytes;
            if (multiContents.Contents.Count > 2)
            {
                //The file as byte array
                fileBytes = await multiContents.Contents[2].ReadAsByteArrayAsync();
                if (fileBytes.Length <= 0)
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }
            else
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            return fileBytes;
        }

        private async Task<string> getStringDataFromRequest(MultipartMemoryStreamProvider multiContents, int position)
        {
            string fileName;
            if (multiContents.Contents.Count > position)
            {
                //Filename as string content
                fileName = await multiContents.Contents[position].ReadAsStringAsync();
                if (String.IsNullOrEmpty(fileName))
                    throw new HttpResponseException(HttpStatusCode.BadRequest);
            }
            else
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            return fileName;
        }
        #endregion Private methods
    }
}
