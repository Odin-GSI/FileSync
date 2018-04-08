using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebClientFileSync.Models;

namespace WebClientFileSync.Controllers
{
    public class FilesController : Controller
    {
        private string _serverSyncFolder = @"C:\SyncFolders\ServerFolder";
        private string _webApiURLtoLoad = ConfigurationManager.AppSettings["SignalRHubURL"].ToString() + "/api/FileTransfer/Upload";
        private string _webApiURLtoDelete = ConfigurationManager.AppSettings["SignalRHubURL"].ToString() + "/api/FileTransfer/Delete";
        private string _webApiURLtoConfirmUpload = ConfigurationManager.AppSettings["SignalRHubURL"].ToString() + "/api/FileTransfer/ConfirmUpload";

        [HttpGet]
        public ActionResult List()
        {
            DirectoryInfo salesFTPDirectory = null;
            FileInfo[] files = null;

            string salesFTPPath = _serverSyncFolder;
            salesFTPDirectory = new DirectoryInfo(salesFTPPath);
            files = salesFTPDirectory.GetFiles();
            var fileNames = files.OrderBy(f => f.Name)
                              .Select(f => f.Name)
                              .ToArray();
            ViewBag.Message = TempData["Message"];
            return View(new ListModel() { filenames = fileNames });
        }

        [HttpPost]
        public ActionResult Upload()
        {
            if (Request.Files[0].ContentLength > 0)
            {
                //Should be only one
                var httpFile = Request.Files[0];
                byte[] fileBytes = new byte[httpFile.ContentLength];
                httpFile.InputStream.Read(fileBytes, 0, httpFile.ContentLength);

                string fileName = httpFile.FileName.Split('\\').Last();

                //Upload to WebApi
                if(sendFile(fileBytes, fileName))
                    TempData["Message"] = "Upload successful.";
                else
                    TempData["Message"] = "Upload unsuccessful.";
            }
            else
                TempData["Message"] = "No file selected.";

            return RedirectToAction("List");
        }

        [HttpGet]
        public ActionResult Delete(string fileName)
        {
            using (var client = new HttpClient())
            {
                var response = client.DeleteAsync(_webApiURLtoDelete+"?filename="+fileName).Result;

                if (response.StatusCode == HttpStatusCode.OK)
                    TempData["Message"] = "File deleted.";
                else
                    TempData["Message"] = "There was a problem in the server.";
            }

            return RedirectToAction("List");
        }

        private bool sendFile(byte[] file,string fileName)
        {
            HttpContent fileContent = new ByteArrayContent(file);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = client.PostAsync(_webApiURLtoLoad, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { new StringContent("NewFile"),"previousHash"},
                    { fileContent, "file", fileName }
                }).Result;

                if (response.IsSuccessStatusCode)
                    return true;

                if (response.StatusCode == HttpStatusCode.Ambiguous)
                {
                    string tempGuid = response.Content.ReadAsStringAsync().Result;
                    var overwriteResponse = client.PostAsync(_webApiURLtoConfirmUpload + "?fileName=" + fileName, new StringContent(tempGuid));
                    return overwriteResponse.Result.IsSuccessStatusCode;
                }
                else
                return false;
            }
        }
    }
}