using FolderSynchronizer.FolderStateModel;
using FolderSynchronizer.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FolderSynchronizer.Classes
{
    public class WebApiManager: IServerManager
    {
        private string _webApiRUL = "";
        private string _webApiURLtoUpload;
        private string _webApiURLtoConfirmUpload;
        private string _webApiURLtoDelete;
        private string _wepApiURLtoDeleteTemp;
        private string _wepApiURLtoDownload;
        private string _wepApiURLExists;
        private string _webApiRULGetFolderStatus;
        private string _webApiURLtoUploadOverwrite;

        public WebApiManager(string serviceEndPoint)
        {
            _webApiRUL = serviceEndPoint;
            _webApiURLtoUpload = serviceEndPoint + "Upload";
            _webApiURLtoConfirmUpload = serviceEndPoint + "ConfirmUpload";
            _webApiURLtoDelete = serviceEndPoint + "Delete";
            _wepApiURLtoDeleteTemp = serviceEndPoint + "DeleteTemp";
            _wepApiURLtoDownload = serviceEndPoint + "Download";
            _wepApiURLExists = serviceEndPoint + "Exists";
            _webApiRULGetFolderStatus = serviceEndPoint + "GetFolderStatus";
            _webApiURLtoUploadOverwrite = serviceEndPoint + "UploadOverwrite";
        }

        public async Task<HttpStatusCode> DeleteFileAsync(string fileName, string hash)
        {
            using (var client = new HttpClient())
            {
                var response = await client.DeleteAsync(_webApiURLtoDelete + "?filename=" + fileName + "&previousHash=" + hash);
                return response.StatusCode;
            }
        }
        public async Task<bool> FileExistsAsync(string fileName)
        {
            bool final;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(_wepApiURLExists + "?fileName=" + fileName);
                var result = await response.Content.ReadAsStringAsync();
                final = JsonConvert.DeserializeObject<bool>(result);
            }

            return final;
        }
        public async Task<bool> ConfirmUploadAsync(string fileName, string tempGuid)
        {
            using (var client = new HttpClient())
            {
                var overwriteResponse = await client.PostAsync(_webApiURLtoConfirmUpload + "?fileName=" + fileName, new StringContent(tempGuid));
                return overwriteResponse.IsSuccessStatusCode;
            }
        }
        public async Task<bool> DeleteTempAsync(string fileName, string tempGuid)
        {
            using (var client = new HttpClient())
            {
                var deleteTempResponse = await client.PutAsync(_wepApiURLtoDeleteTemp + "?fileName=" + fileName, new StringContent(tempGuid));
                return deleteTempResponse.IsSuccessStatusCode;
            }
        }
        public async Task<string[]> UploadFileAsync(string fileName, byte[] fileContent, string previousHash)
        {
            string[] result = new string[2];

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(_webApiURLtoUpload, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { new StringContent(previousHash),"previousHash"},
                    { new ByteArrayContent(fileContent), "file", fileName }
                });
                result[1] = await response.Content.ReadAsStringAsync();
                result[0] = response.StatusCode.ToString();
            }

            return result;
        }
        public async Task<bool> UploadOverwriteAsync(string fileName, byte[] fileContent)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(_webApiURLtoUploadOverwrite, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { new StringContent("Overwrite"),"previousHash"},
                    { new ByteArrayContent(fileContent), "file", fileName }
                });
                return response.IsSuccessStatusCode;
            }
        }
        public async Task<byte[]> DownloadFileAsync(string fileName)
        {
            using (var client = new HttpClient())
            {
                var responseStream = await client.GetStreamAsync(_wepApiURLtoDownload + "?fileName=" + fileName);
                MemoryStream ms = new MemoryStream();
                responseStream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        public async Task<List<FolderFileState>> GetServerFolderStatusAsync()
        {
            FolderState remoteFolderState = new FolderState();
            string folderDefinition = "";

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(_webApiRULGetFolderStatus);
                folderDefinition = await response.Content.ReadAsStringAsync();
            }

            remoteFolderState.Definition = folderDefinition;

            return remoteFolderState.RemoteFiles().ToList();
        }
    }
}
