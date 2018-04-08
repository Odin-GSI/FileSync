using FolderSynchronizer.Enums;
using FolderSynchronizer.FolderStateModel;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
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

namespace FolderSynchronizer.Classes
{
    public class Synchronizer
    {
        #region Class vars
        private FolderStatusManager _folderSyncState;
        private string _localSyncFolder;
        private string _remoteSyncFolder;
        private string _webApiRUL = "";
        private string _webApiURLtoUpload;
        private string _webApiURLtoConfirmUpload;
        private string _webApiURLtoDelete;
        private string _wepApiURLtoDeleteTemp;
        private string _wepApiURLtoDownload;
        private string _wepApiURLExists;
        private string _webApiRULGetFolderStatus;
        private string _signalRHost;
        private string _signalRHub;
        FileSystemWatcher _watcher; //Watcher for Copy and Modify
        FileSystemWatcher _deleteWatcher; //Watcher for Delete
        private Thread signalRThread;
        private IFileManager _fileManager; //Is initiated in StartWatcher
        private string _signalRDownloadedFile = ""; //Needed to check last file downloaded and ignore New File Watcher event
        private DateTime _signalRDownloadedFileTime = new DateTime();
        private string _signalRDeletedFile = ""; //Needed to check last file deleted by SignalR and ignore Delete File Watcher event
        private DateTime _signalRDeletedFileTime = new DateTime();
        public delegate void FileSyncNotification(string fileName, SyncNotification syncNotification,string optionalMsg);
        public delegate void ProceedOnConflict(string fileName, SyncConflict syncEvent, string conflictID);
        public delegate void SignalRConnectionStateChanged(string connectionStatus);
        #endregion Class vars

        #region Constructors
        // serviceEndPoint: http://localhost/ServerFileSync/api/FileTransfer/        
        public Synchronizer(string serviceEndPoint)
        {
            if (serviceEndPoint.Last() != '/')
                serviceEndPoint += '/';

            _webApiRUL = serviceEndPoint;
            _webApiURLtoUpload = serviceEndPoint+"Upload";
            _webApiURLtoConfirmUpload = serviceEndPoint + "ConfirmUpload";
            _webApiURLtoDelete = serviceEndPoint+"Delete";
            _wepApiURLtoDeleteTemp = serviceEndPoint + "DeleteTemp";
            _wepApiURLtoDownload = serviceEndPoint + "Download";
            _wepApiURLExists = serviceEndPoint + "Exists";
            _webApiRULGetFolderStatus = serviceEndPoint + "GetFolderStatus";
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
        public void StartWatcher(string localFolderPath, string remoteFolderPath, IFileManager fileManager)
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
            _localSyncFolder = localFolderPath;
            _remoteSyncFolder = remoteFolderPath;

            //The File Api to save/read files
            _fileManager = fileManager;

            //Watcher for Copy and Modify actions
            _watcher = new FileSystemWatcher();
            _watcher.Path = _localSyncFolder;
            _watcher.NotifyFilter = NotifyFilters.LastWrite; //Need another watcher for Delete because this filter invalidates Delete
            _watcher.Filter = "*.*";
            _watcher.Changed += new FileSystemEventHandler(onWatcherChanged);
            _watcher.Created += new FileSystemEventHandler(onWatcherCreated);
            _watcher.EnableRaisingEvents = true;

            //Watcher for Delete action
            _deleteWatcher = new FileSystemWatcher();
            _deleteWatcher.Path = _localSyncFolder;
            _deleteWatcher.Filter = "*.*";
            _deleteWatcher.Deleted += new FileSystemEventHandler(onWatcherDeleted);
            _deleteWatcher.EnableRaisingEvents = true;

            //Check the FolderStatus
            startUpSyncFolderAsync();
        }
        #endregion ToolsStarters

        #region SignalR
        public bool SignalRThreadActive { get; set; }
        protected IHubProxy SignalRProxy { get; set; }
        protected HubConnection SignalRConnection { get; set; }
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
            //vars to prevent processing when FileWatcher triggers this Delete
            _signalRDeletedFile = fileName;
            _signalRDeletedFileTime = DateTime.Now;

            operationDeleteFile(fileName);
        }
        private void OnNewFileNotified(string fileName, string CRC)
        {
            //vars to prevent processing when FileWatcher triggers this copy/modify event
            _signalRDownloadedFile = fileName;
            _signalRDownloadedFileTime = DateTime.Now;

            operationDownloadFileAsync(fileName, CRC);
        }
        #endregion SignalR
        
        #region SystemFileWatcher
        Dictionary<string, DateTime> changedFiles = new Dictionary<string, DateTime>();
        private void onWatcherChanged(object source, FileSystemEventArgs e)
        {
            if (changedFiles.ContainsKey(e.Name) && (DateTime.Now.Subtract(changedFiles[e.Name]).TotalSeconds < 3))
                return;

            //Check if it was the downloaded File. OnDownload Watcher triggers Changed
            if (_signalRDownloadedFile != "")
                if (_signalRDownloadedFile == e.Name && DateTime.Now.Subtract(_signalRDownloadedFileTime).TotalSeconds < 5)
                {
                    _signalRDownloadedFile = "";
                    return;
                }

            operationUploadFileAsync(e.Name);

            if (changedFiles.ContainsKey(e.Name))
                changedFiles[e.Name] = DateTime.Now;
            else
                changedFiles.Add(e.Name, DateTime.Now);
        }

        private void onWatcherCreated(object source, FileSystemEventArgs e)
        {
            //Check if it was the downloaded File
            if (_signalRDownloadedFile != "")
                if (_signalRDownloadedFile == e.Name && DateTime.Now.Subtract(_signalRDownloadedFileTime).TotalSeconds < 5)
                {
                    _signalRDownloadedFile = "";
                    return;
                }

            operationUploadFileAsync(e.Name);
        }

        private void onWatcherDeleted(object source, FileSystemEventArgs e)
        {
            //Check if it was the deleted File from SignalR
            if (_signalRDeletedFile != "")
                if (_signalRDeletedFile == e.Name && DateTime.Now.Subtract(_signalRDeletedFileTime).TotalSeconds < 5)
                {
                    _signalRDeletedFile = "";
                    return;
                }

            operationCallDeleteFileAsync(e.Name);
        }
        #endregion SystemFileWatcher

        #region SyncOperations

        #region ServerOperations
        private async Task operationDownloadFileAsync(string filename, string CRC)
        {
            bool newFile = true;

            if (_fileManager.Exists(filename))
            {
                //If File exists and has the same Hash no download is needed
                if (CRC.Equals(_fileManager.GetHash(filename)))
                    return;

                newFile = false;
            }

            byte[] fileContent = new byte[0];

            try
            {
                fileContent = await downloadFileAsync(filename);
            }
            catch (HttpRequestException e)
            {
                userNotification(filename, newFile ? SyncNotification.NewDownloadFail : SyncNotification.UpdateDownloadFail);
                return;
            }
            if (tryToSaveFile(filename, fileContent))
            {
                _folderSyncState.NewFileLocalAndServer(filename, CRC);
                userNotification(filename, newFile ? SyncNotification.SuccessfulNewDownload : SyncNotification.SuccessfulUpdateDownload);
            }
            else
                userNotification(filename, newFile ? SyncNotification.SuccessfulNewDownloadLocalFileSaveFail : SyncNotification.SuccessfulUpdateDownloadLocalFileSaveFail);
        }
        private void operationDeleteFile(string filename)
        {
            if (_fileManager.Exists(filename))
                if (tryToDeleteFile(filename))
                {
                    _folderSyncState.DeleteFileLocalAndServer(filename);
                    userNotification(filename, SyncNotification.SuccessfulLocalDelete);
                }
                else
                    userNotification(filename, SyncNotification.LocalDeleteFail);
        }
        #endregion ServerOperations

        #region LocalOperations
        private async Task operationUploadFileAsync(string filename)
        {
            //Get File Content
            byte[] fileContent = new byte[10];
            string CRC = "";
            try
            {
                fileContent = tryToGetContent(filename);
                CRC = _fileManager.GetHash(filename);
                _folderSyncState.UpdateLocalFileStatus(filename, CRC, FileStatusType.Uploading);

            }
            catch (IOException ex)
            {
                userNotification(filename, SyncNotification.ReadLocalFileToUploadFail, ex.Message);
                return;
            }
            
            //Upload File as Temp and get Guid
            string[] uploadResponse = new string[2];
            try
            {
                uploadResponse = await uploadFileAsync(filename, fileContent);
            }
            catch (HttpRequestException ex)
            {
                userNotification(filename, SyncNotification.UploadFail);
                return;
            }
            string tempGuid = uploadResponse[1];

            //React on result
            switch (uploadResponse[0])
            {
                case "OK": //New File now ComfirmUpload automatically in Server
                    //bool confirmOk = await confirmSaveAsync(filename, tempGuid);
                    //if (confirmOk)
                    //{
                        _folderSyncState.NewFileLocalAndServer(filename, CRC);
                        userNotification(filename, SyncNotification.SuccessfulNewUpload);
                    //}
                    //else
                    //    userNotification(filename, SyncNotification.ConfirmUploadFail);
                    break;
                case "Accepted": //File in Server was previous version, ComfirmUpload was called automatically in Server
                    _folderSyncState.NewFileLocalAndServer(filename, CRC);
                    userNotification(filename, SyncNotification.SuccessfulUpdateUpload);
                    break;
                case "MultipleChoices": //Ambiguous - File in Server is different from expected, the file changed. User must decide
                    if (userConfirmationCall(filename, SyncConflict.NewLocalFileOtherVersionOnServer))
                    {
                        if (await confirmSaveAsync(filename, tempGuid))
                        {
                            _folderSyncState.NewFileLocalAndServer(filename, CRC);
                            userNotification(filename, SyncNotification.SuccessfulUpdateUpload);
                        }
                        else
                            userNotification(filename, SyncNotification.UpdateUploadFail);
                    }
                    else
                    {
                        if (await deleteTempAsync(filename, tempGuid))
                        {
                            _folderSyncState.UpdateLocalFileStatus(filename, CRC, FileStatusType.Ignore);
                            userNotification(filename, SyncNotification.GeneralInfo, "Upload Aborted");
                        }
                        else
                            userNotification(filename, SyncNotification.GeneralFail, "Upload Aborting Failed");
                    }
                    break;
                case "NotModified":
                    {
                        _folderSyncState.UpdateLocalFileStatus(filename, CRC, FileStatusType.Synced);
                        userNotification(filename, SyncNotification.GeneralInfo, "File Exists on Server");
                        break;
                    }
                case "BadRequest":
                    userNotification(filename, SyncNotification.UploadFail, "400");
                    break;
                case "InternalServerError":
                default:
                    userNotification(filename, SyncNotification.UploadFail, "500");
                    break;
            }
        }
        private async Task operationCallDeleteFileAsync(string filename)
        {
            _folderSyncState.DeleteFileInLocalStatus(filename);

            if (await fileExistsOnServer(filename))
            {
                HttpStatusCode deleteResponseCode = HttpStatusCode.NoContent;

                try
                {
                    deleteResponseCode = await deleteFileOnServer(filename,_folderSyncState.RemoteFile(filename).Hash());
                }
                catch (Exception ex)
                {
                    userNotification(filename, SyncNotification.ServerDeleteFail, ex.Message);
                    return;
                }
                if (deleteResponseCode == HttpStatusCode.OK)
                {
                    _folderSyncState.DeleteFileInServerStatus(filename);
                    userNotification(filename, SyncNotification.SuccessfulServerDelete);
                }
                else
                if (deleteResponseCode == HttpStatusCode.Ambiguous) //File in Server is modified. User must decide.
                {
                    userConfirmationCall(filename, SyncConflict.NewerVersionOnServerAndLocalDeleted);
                }
                else
                    userNotification(filename, SyncNotification.ServerDeleteFail, deleteResponseCode.ToString());
            }
            else
                userNotification(filename, SyncNotification.GeneralInfo, "File doesn't exist on Server");
        }
        #endregion LocalOperations

        #endregion SyncOperations

        #region WebApiActions
        private async Task<HttpStatusCode> deleteFileOnServer(string fileName, string hash)
        {
            using (var client = new HttpClient())
            {
                var response = await client.DeleteAsync(_webApiURLtoDelete + "?filename=" + fileName+"&previousHash="+hash);
                return response.StatusCode;
            }
        }
        private async Task<bool> fileExistsOnServer(string fileName)
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
        private async Task<bool> confirmSaveAsync(string fileName, string tempGuid)
        {
            using (var client = new HttpClient())
            {
                var overwriteResponse = await client.PostAsync(_webApiURLtoConfirmUpload + "?fileName=" + fileName, new StringContent(tempGuid));
                return overwriteResponse.IsSuccessStatusCode;
            }
        }
        private async Task<bool> deleteTempAsync(string fileName, string tempGuid)
        {
            using (var client = new HttpClient())
            {
                var deleteTempResponse = await client.PutAsync(_wepApiURLtoDeleteTemp + "?fileName=" + fileName, new StringContent(tempGuid));
                return deleteTempResponse.IsSuccessStatusCode;
            }
        }
        private async Task<string[]> uploadFileAsync(string fileName, byte[] fileContent)
        {
            string[] result = new string[2];

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(_webApiURLtoUpload, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { new StringContent(_folderSyncState.GetExpectedServerFileHash(fileName)),"previousHash"},
                    { new ByteArrayContent(fileContent), "file", fileName }
                });
                result[1] = await response.Content.ReadAsStringAsync();
                result[0] = response.StatusCode.ToString();
            }

            return result;
        }
        private async Task<byte[]> downloadFileAsync(string fileName)
        {
            using (var client = new HttpClient())
            {
                var responseStream = await client.GetStreamAsync(_wepApiURLtoDownload + "?fileName=" + fileName);
                MemoryStream ms = new MemoryStream();
                responseStream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        private async Task<List<FolderFileState>> getServerFolderStatusAsync()
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
        #endregion WebApiActions

        #region FileActions
        private byte[] tryToGetContent(string fileName, int tries = 0)
        {
            try
            {
                using (FileStream fs = _fileManager.GetStream(fileName))
                {
                    MemoryStream ms = new MemoryStream();
                    fs.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (IOException e)
            {
                if (tries > 50)
                    throw new IOException(e.Message);
                else
                    return tryToGetContent(fileName, ++tries);
            }
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

        #region UserNotificationAndConflicts
        public event FileSyncNotification OnSyncNotification;
        private void userNotification(string fileName, SyncNotification syncType, string optionalMsg ="")
        {
            OnSyncNotification?.Invoke(fileName, syncType, optionalMsg);
        }

        public event ProceedOnConflict OnConflictConfirmationRequiredToProceed;
        private bool userConfirmationCall(string fileName, SyncConflict syncConflict)
        {
            if (isSyncEventBeingHandledByUser(syncConflict))
                return (OnConflictConfirmationRequiredToProceed?.Invoke(fileName, syncConflict)).Value;
            else
                return byDefaultSyncEventAction(syncConflict);
        }

        private bool isSyncEventBeingHandledByUser(SyncConflict syncConflict)
        {
            //Check list created on SetEventsThatRequireUserConfirmation(new List< SyncConflict> {....., ......, ......}); 
            return true;
        }

        private bool byDefaultSyncEventAction(SyncConflict syncConflict)
        {
            //Could be configurable also?
            return true;
        }
        #endregion UserNotificationAndConflicts

        #region SyncFolderStatus
        private async Task startUpSyncFolderAsync()
        {
            _folderSyncState = new FolderStatusManager(_localSyncFolder,_remoteSyncFolder,_fileManager);

            return;

            List<FolderFileState> serverCurrentList = await getServerFolderStatusAsync();
            List<FolderFileState> localCurrentList = _folderSyncState.ReadFolder();

            //Sync Operations
            List<FolderFileState> toUpload = getDifferences(localCurrentList, _folderSyncState.FolderState.LocalFiles().ToList());
            List<FolderFileState> toDownload = getDifferences(serverCurrentList, _folderSyncState.FolderState.RemoteFiles().ToList());
            List<FolderFileState> toDelete = getDifferences(_folderSyncState.FolderState.RemoteFiles().ToList(), serverCurrentList); //Local delete based on server info
            List<FolderFileState> toCallDelete = getDifferences(_folderSyncState.FolderState.LocalFiles().ToList(), localCurrentList); //Call delete based on local info

            //Analyze Conflicts between Sync Operations
            // ************************************************************************** TO DO

            //Execute Sync Operations
            foreach (var f in toUpload)
                operationUploadFileAsync(f.FileName());

            foreach (var f in toDownload)
                operationDownloadFileAsync(f.FileName(),f.Hash());

            foreach (var f in toDelete)
                operationDeleteFile(f.FileName());

            foreach (var f in toCallDelete)
                operationCallDeleteFileAsync(f.FileName());
        }

        private List<FolderFileState> getDifferences(List<FolderFileState> filesIn, List<FolderFileState> filesNotIn)
        {
            return filesIn.Where(x => !filesNotIn.Any(f => (x.FileName() == f.FileName()) && (x.Hash() == f.Hash()))).ToList();
        }
        #endregion SyncFolderStatus
    }
}
