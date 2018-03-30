﻿using ServerFileSync.Interfaces;
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
        private string _root;
        private IFileManager _fileManager;
        private IFileNotifier _hubWrapper;
        #endregion Class vars

        #region Constructors
        /// <summary>
        /// Public Constructor
        /// </summary>
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
        #endregion Constructors

        #region Public methods

        /// <summary>
        /// Creates a file based on the temporary previously uploaded one
        /// Requires the temporary Guid as String in the HttpRequest Content
        /// Generates SignalR NotifyNewFile on success
        /// </summary>
        /// <param name="fileName">Original name of the file</param>
        /// <returns></returns>
        [HttpPost]
        public HttpResponseMessage ConfirmSave(string fileName)
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
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

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
        /// Uploads a file and saves it with a temporary name with a generated Guid
        /// If the uploaded file exists with the same Hash, nothing is done
        /// </summary>
        /// <returns>The generated Guid as a String Content in the HttpResponse</returns>
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
                byte[] fileBytes = await getFileBytesFromRequest(multiContents);
                
                Guid tempGuid = _fileManager.Save(fileName, fileBytes);

                HttpResponseMessage myResponse = new HttpResponseMessage();
                myResponse.Content = new StringContent(tempGuid.ToString());
                myResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

                //Check original file existance at the end of the temporal copy
                if (_fileManager.Exists(fileName))
                {
                    //If the existing File has the same Hash, nothing needs to be done
                    if (_fileManager.SameHash(fileName, fileBytes))
                    {
                        _fileManager.DeleteTemp(fileName, tempGuid);
                        return new HttpResponseMessage(HttpStatusCode.NotModified);
                    }

                    myResponse.StatusCode = HttpStatusCode.Ambiguous;
                }
                else
                {
                    myResponse.StatusCode = HttpStatusCode.OK;
                }

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
        public HttpResponseMessage Delete(string filename)
        {
            try
            {
                if (String.IsNullOrEmpty(filename))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                _fileManager.Delete(filename);
                _hubWrapper.NotifyDeleteFile(filename);
                return new HttpResponseMessage(HttpStatusCode.OK);
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
        /// <returns>true if exists, false if it doesn't</returns>
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

        #endregion Public methods

        #region Private methods
        private async Task<byte[]> getFileBytesFromRequest(MultipartMemoryStreamProvider multiContents)
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
