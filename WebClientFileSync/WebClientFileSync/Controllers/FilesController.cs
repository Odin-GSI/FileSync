using System;
using System.Collections.Generic;
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
        private string _webApiURLtoLoad = "http://localhost/ServerFileSync/api/FileTransfer/Upload";

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
                if(sendFile(fileBytes, fileName).Result)
                    TempData["Message"] = "Upload successful.";
                else
                    TempData["Message"] = "Upload unsuccessful.";
            }
            else
                TempData["Message"] = "No file selected.";

            return RedirectToAction("List");
        }

        private async Task<bool> sendFile(byte[] file,string fileName)
        {
            HttpContent fileContent = new ByteArrayContent(file);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(_webApiURLtoLoad, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { fileContent, "file", fileName }
                });

                return response.IsSuccessStatusCode;
            }
        }
    }
}