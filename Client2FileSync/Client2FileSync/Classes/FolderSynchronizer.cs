using Client2FileSync.Enums;
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client2FileSync.Classes
{
    class FolderSynchronizer
    {
        #region Class vars
        private string _webApiRUL = "";
        private string _syncFolder;
        private string _webApiURLtoUpload;
        private string _webApiURLtoConfirmSave;
        private string _webApiURLtoDelete;
        private string _wepApiURLtoDeleteTemp;
        private string _wepApiURLtoDownload;
        private string _wepApiURLExists;
        private string _signalRHost;
        private string _signalRHub;
        FileSystemWatcher _watcher; //Watcher for Copy and Modify
        FileSystemWatcher _deleteWatcher; //Watcher for Delete
        private Thread signalRThread;
        private IFileManager _fileManager; //Is initiated in StartWatcher
        private string _downloadedFile = ""; //Needed to check last file downloaded and ignore New File Watcher event
        private DateTime _downloadedFileTime = new DateTime();
        public delegate bool ProceedOnConflict(SyncNotification syncEvent);
        public delegate void SignalRConnectionStateChanged(string connectionStatus);
        #endregion Class vars

        #region Constructors
        // serviceEndPoint: http://localhost/ServerFileSync/api/FileTransfer/        
        public FolderSynchronizer(string serviceEndPoint)
        {
            _webApiRUL = serviceEndPoint;
            _webApiURLtoUpload = serviceEndPoint+"Upload";
            _webApiURLtoConfirmSave = serviceEndPoint + "ConfirmSave";
            _webApiURLtoDelete = serviceEndPoint+"Delete";
            _wepApiURLtoDeleteTemp = serviceEndPoint + "DeleteTemp";
            _wepApiURLtoDownload = serviceEndPoint + "Download";
            _wepApiURLExists = serviceEndPoint + "Exists";
        }
        #endregion Constructors

        #region ToolsStarters
        // signalRHost: http://localhost/ServerFileSync/
        // hub: FileSyncHub
        public void StartSignalR(string hostURL, string hubName)
        {
            _signalRHost = hostURL;
            _signalRHub = hubName;

            SignalRThreadActive = true;
            signalRThread = new System.Threading.Thread(() =>
            {
                SignalRConnection = new HubConnection(_signalRHost);
                SignalRConnection.Error += Connection_Error;
                SignalRConnection.StateChanged += Connection_StateChanged;
                SignalRProxy = SignalRConnection.CreateHubProxy(_signalRHub);

                SignalRProxy.On<string, string>("NewFileNotification", (fileName, CRC) => OnNewFileNotified(fileName, CRC));
                SignalRProxy.On<string>("DeleteFileNotification", (fileName) => OnDeleteFileNotified(fileName));

                SignalRConnection.Start();

                while (SignalRThreadActive)
                {
                    System.Threading.Thread.Sleep(10);
                }
            })
            { IsBackground = true };
            signalRThread.Start();
        }
        public void StartWatcher(string folderPath, IFileManager fileManager)
        {
            //If there was another folder being watched, drop it
            if (_watcher != null)
            {
                _watcher.Created -= new FileSystemEventHandler(onWatcherCreated);
                _watcher.Changed -= new FileSystemEventHandler(onWatcherChanged);
                _watcher.Dispose();
                _watcher = null;

                _deleteWatcher.Deleted -= new FileSystemEventHandler(onWatcherDeleted);
                _deleteWatcher.Dispose();
                _deleteWatcher = null;
            }

            //The local folder to sync
            _syncFolder = folderPath;

            //The File Api to save/read files
            _fileManager = fileManager;

            //Watcher for Copy and Modify actions
            _watcher = new FileSystemWatcher();
            _watcher.Path = _syncFolder;
            _watcher.NotifyFilter = NotifyFilters.LastWrite; //Need another watcher for Delete because this filter invalidates Delete
            _watcher.Filter = "*.*";
            _watcher.Changed += new FileSystemEventHandler(onWatcherChanged);
            _watcher.Created += new FileSystemEventHandler(onWatcherCreated);
            _watcher.EnableRaisingEvents = true;

            //Watcher for Delete action
            _deleteWatcher = new FileSystemWatcher();
            _deleteWatcher.Path = _syncFolder;
            _deleteWatcher.Filter = "*.*";
            _deleteWatcher.Deleted += new FileSystemEventHandler(onWatcherDeleted);
            _deleteWatcher.EnableRaisingEvents = true;

            //Check the FolderStatus
            startUpSyncFolder();
        }

        private void startUpSyncFolder()
        {
            //******************************************************* TO DO
        }
        #endregion ToolsStarters

        #region SignalR
        public bool SignalRThreadActive { get; set; }
        public IHubProxy SignalRProxy { get; set; }
        public HubConnection SignalRConnection { get; set; }
        public event SignalRConnectionStateChanged OnSignalRConnectionStateChanged;
        private void Connection_StateChanged(StateChange obj)
        {
            OnSignalRConnectionStateChanged?.Invoke(obj.NewState.ToString());
        }
        private void Connection_Error(Exception obj)
        {
            OnSignalRConnectionStateChanged?.Invoke("Error: " + obj.ToString());
        }
        private void OnDeleteFileNotified(string fileName)
        {
            //A delegate could be raised here to read user input to proceed or not

            if (_fileManager.Exists(fileName))
                if (tryToDeleteFile(fileName))
                    userNotification(fileName,SyncNotification.SuccessfulLocalDelete);
                else
                    userNotification(fileName,SyncNotification.LocalDeleteFail);
        }
        private void OnNewFileNotified(string fileName, string CRC)
        {
            bool newFile = true;

            if (_fileManager.Exists(fileName))
            {
                //If File exists and has the same Hash no download is needed
                if (CRC.Equals(_fileManager.GetHash(fileName)))
                    return;

                newFile = false;
            }

            byte[] fileContent = new byte[0];

            try
            {
                fileContent = downloadFileAsync(fileName).Result;
            }
            catch (HttpRequestException e)
            {
                userNotification(fileName, newFile ? SyncNotification.NewDownloadFail : SyncNotification.UpdateDownloadFail);
                return;
            }
            if (tryToSaveFile(fileName, fileContent))
                userNotification(fileName, newFile ? SyncNotification.SuccessfulNewDownload : SyncNotification.SuccessfulUpdateDownload);
            else
                userNotification(fileName, newFile ? SyncNotification.SuccessfulNewDownloadLocalFileSaveFail : SyncNotification.SuccessfulUpdateDownloadLocalFileSaveFail);
        }
        #endregion SignalR

        #region SystemFileWatcher
        Dictionary<string, DateTime> changedFiles = new Dictionary<string, DateTime>();
        private void onWatcherChanged(object source, FileSystemEventArgs e)
        {
            if (changedFiles.ContainsKey(e.Name) && (DateTime.Now.Subtract(changedFiles[e.Name]).TotalSeconds < 3))
                return;

            //Task.WaitAll(sendFile(e.Name));
            
            if (changedFiles.ContainsKey(e.Name))
                changedFiles[e.Name] = DateTime.Now;
            else
                changedFiles.Add(e.Name, DateTime.Now);
        }

        private void onWatcherCreated(object source, FileSystemEventArgs e)
        {
            //Check if it was the downloaded File
            if (_downloadedFile != "")
                if (_downloadedFile == e.Name && DateTime.Now.Subtract(_downloadedFileTime).TotalSeconds < 5)
                {
                    _downloadedFile = "";
                    return;
                }

            //Get File Content
            byte[] fileContent = new byte[10];
            try
            {
                fileContent = tryToGetContent(e.Name);
            }
            catch(IOException ex)
            {
                userNotification(e.Name, SyncNotification.ReadLocalFileToUploadFail,ex.Message);
                return;
            }

            //Upload File as Temp and get Guid
            string tempGuid = "";
            HttpStatusCode uploadResponse = HttpStatusCode.Unused;
            try
            {
                uploadResponse = uploadFile(e.Name, fileContent,out tempGuid);
            }
            catch(HttpRequestException ex)
            {
                userNotification(e.Name, SyncNotification.UploadFail);
                return;
            }

            //React on result
            switch (uploadResponse)
            {
                case HttpStatusCode.OK:
                    // New File, confirm save
                    if (confirmSaveAsync(e.Name, tempGuid).Result)
                        userNotification(e.Name, SyncNotification.SuccessfulNewUpload);
                    else
                        userNotification(e.Name, SyncNotification.ConfirmUploadFail);
                    break;
                case HttpStatusCode.Ambiguous:
                    // File Exists on Server - Do you want to overwrite?
                    //************************************************************************* TO DO
                    break;
                case HttpStatusCode.NotModified:
                    // File Exists on Server and has the same Hash - No file save took place
                    // No need to notify the user (??)
                    break;
                case HttpStatusCode.BadRequest:
                    userNotification(e.Name, SyncNotification.UploadFail, "400");
                    break;
                case HttpStatusCode.InternalServerError:
                default:
                    userNotification(e.Name,SyncNotification.UploadFail,"500");
                    break;
            }
        }

        private void onWatcherDeleted(object source, FileSystemEventArgs e)
        {
            //if (fileExistsOnServer(e.Name, "").Result)
            //    if (System.Windows.MessageBox.Show("File " + e.Name + " exists in server. Do you want to delete it?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            //        Task.WaitAll(deleteFileOnServer(e.Name));
        }
        #endregion SystemFileWatcher

        #region WebApiActions
        private async Task<bool> confirmSaveAsync(string fileName, string tempGuid)
        {
            using (var client = new HttpClient())
            {
                var overwriteResponse = await client.PostAsync(_webApiURLtoConfirmSave + "?fileName=" + fileName, new StringContent(tempGuid));
                return overwriteResponse.IsSuccessStatusCode;
            }
        }
        private HttpStatusCode uploadFile(string fileName, byte[] fileContent, out string tempGuid)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = client.PostAsync(_webApiURLtoUpload, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { new ByteArrayContent(fileContent), "file", fileName }
                }).Result;
                tempGuid = response.Content.ReadAsStringAsync().Result;
                return response.StatusCode;
            }
        }
        private async Task<byte[]> downloadFileAsync(string fileName)
        {
            _downloadedFile = fileName;
            _downloadedFileTime = DateTime.Now;

            using (var client = new HttpClient())
            {
                var responseStream = await client.GetStreamAsync(_wepApiURLtoDownload + "?fileName=" + fileName);
                MemoryStream ms = new MemoryStream();
                responseStream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        #endregion WebApiActions

        #region FileActions
        private byte[] tryToGetContent(string fileName, int tries = 0)
        {
            FileStream fs = new FileStream("blank",FileMode.Create);
            try
            {
                fs = _fileManager.GetStream(fileName);
            }
            catch(IOException e)
            {
                if (tries > 50)
                    throw new IOException(e.Message);
                else
                    tryToGetContent(fileName, ++tries);
            }
            MemoryStream ms = new MemoryStream();
            fs.CopyTo(ms);
            return ms.ToArray();
        }
        private bool tryToSaveFile(string fileName, byte[] fileContent,int tries = 0)
        {
            if (tries > 49)
                return false;

            try
            {
                _fileManager.Save(fileName, fileContent, false);
                return true;
            }
            catch (IOException e)
            {
                return tryToSaveFile(fileName, fileContent, ++tries);
            }
        }
        private bool tryToDeleteFile(string fileName, int tries = 0)
        {
            if (tries > 49)
                return false;

            try
            {
                _fileManager.Delete(fileName);
                return true;
            }
            catch(IOException e)
            {
                return tryToDeleteFile(fileName, ++tries);
            }
        }
        #endregion FileActions

        private void userNotification(string fileName, SyncNotification syncType, string optionalMsg ="")
        {

        }
    }
}
