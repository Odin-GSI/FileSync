using FolderSynchronizer.Enums;
using FolderSynchronizer.FolderStateModel;
using FolderSynchronizer.Interfaces;
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
        private string _signalRHost;
        private string _signalRHub;
        FileSystemWatcher _watcher; //Watcher for Copy and Modify
        FileSystemWatcher _deleteWatcher; //Watcher for Delete
        private Thread signalRThread;
        private IFileManager _fileManager; //Is initiated in StartWatcher
        private IServerManager _serverManager;
        private string _signalRDownloadedFile = ""; //Needed to check last file downloaded and ignore New File Watcher event
        private DateTime _signalRDownloadedFileTime = new DateTime();
        private string _signalRDeletedFile = ""; //Needed to check last file deleted by SignalR and ignore Delete File Watcher event
        private DateTime _signalRDeletedFileTime = new DateTime();
        public delegate void FileSyncNotification(string fileName, SyncNotification syncNotification,string optionalMsg);
        public delegate Task ProceedOnConflict(string fileName, SyncConflictType conflictType, string conflictID);
        public delegate void SignalRConnectionStateChanged(string connectionStatus);
        public event FileSyncNotification OnSyncNotification;
        public event ProceedOnConflict OnConflictConfirmationRequiredToProceed;
        private Dictionary<string, SyncConflict> _conflicts = new Dictionary<string, SyncConflict>();
        private List<SyncConflictType> _userHandledConflicts = new List<SyncConflictType>();
        //private List<string> _autoSync = new List<string>();
        #endregion Class vars

        #region Constructors
        // serviceEndPoint: http://localhost/ServerFileSync/api/FileTransfer/        
        public Synchronizer(string serviceEndPoint)
        {
            if (serviceEndPoint.Last() != '/')
                serviceEndPoint += '/';

            _serverManager = new WebApiManager(serviceEndPoint);

            //Bydefault conflicts to be passed to the user
            //This should be made configurable somewhere
            SetEventsThatRequireUserConfirmation(new List<SyncConflictType>() {
                SyncConflictType.UploadedLocalFileNewerVersionRemote,
                SyncConflictType.NewerVersionRemoteAndLocalDeleted,
                SyncConflictType.NewerVersionRemoteAndLocalVersionChanged,
                SyncConflictType.RemoteFileDeletedAndLocalIsNewer
            });
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
                SignalRProxy.On<string, string>("DeleteFileNotification", (fileName, CRC) => OnDeleteFileNotified(fileName, CRC));

                SignalRConnection.Start();

                while (SignalRThreadActive)
                {
                    System.Threading.Thread.Sleep(10);
                }
            })
            { IsBackground = true };
            signalRThread.Start();
        }
        public async Task StartWatcherAsync(string localFolderPath, string remoteFolderPath, IFileManager fileManager)
        {
            //The local folder to sync
            _localSyncFolder = localFolderPath;
            _remoteSyncFolder = remoteFolderPath;

            //The File Api to save/read files
            _fileManager = fileManager;

            //Add the RemoteSyncFolder to the ServerApi
            _serverManager.SetRemoteSyncFolder(_remoteSyncFolder);

            //Check the FolderStatus
            _folderSyncState = new FolderStatusManager(_localSyncFolder, _remoteSyncFolder, _fileManager);
            await startUpSyncFolderAsync();

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

            //_autoSync.Clear();
        }
        public void Shutdown()
        {
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
            SignalRConnection.Stop();
            signalRThread.Abort();
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
        private void OnDeleteFileNotified(string fileName, string CRC)
        {
            //vars to prevent processing when FileWatcher triggers this Delete
            _signalRDeletedFile = fileName;
            _signalRDeletedFileTime = DateTime.Now;

            operationDeleteFileAsync(fileName, CRC);
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

            if (e.Name.Equals(_folderSyncState.GetStatusFileFolderName) || e.Name.Equals(_folderSyncState.GetStatusFileName))
                return;

            //if (_autoSync.Contains(e.Name))
            //{
            //    return;
            //}

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

            if (e.Name.Equals(_folderSyncState.GetStatusFileFolderName) || e.Name.Equals(_folderSyncState.GetStatusFileName))
                return;

            //if (_autoSync.Contains(e.Name))
            //{
            //    return;
            //}

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

            if (e.Name.Equals(_folderSyncState.GetStatusFileFolderName) || e.Name.Equals(_folderSyncState.GetStatusFileName))
                return;

            //if (_autoSync.Contains(e.Name))
            //{
            //    return;
            //}

            operationCallDeleteFileAsync(e.Name);
        }
        #endregion SystemFileWatcher

        #region SyncOperations

        #region ServerOperations
        private async Task operationDownloadFileAsync(string filename, string CRC, bool ignoreCRCCheck = false)
        {
            bool newFile = true;

            if (_fileManager.Exists(filename))
            {
                //If File exists and has the same Hash no download is needed
                if (!ignoreCRCCheck && !String.IsNullOrEmpty(CRC) && CRC.Equals(_fileManager.GetHash(filename)))
                    return;

                //if(!ignoreSyncStatus && _folderSyncState.LocalFile(filename).CurrentStatus()!=FileStatusType.Synced)
                //{
                //    //Server has file updated but local file is not Synced
                //    userConfirmationCall(new SyncConflict() {Filename=filename, CRC=CRC, ConflictType=SyncConflictType.NewerVersionRemoteAndLocalVersionChanged });
                //    return;
                //}

                newFile = false;
            }

            byte[] fileContent = new byte[0];

            try
            {
                fileContent = await _serverManager.DownloadFileAsync(filename);
            }
            catch (HttpRequestException e)
            {
                userNotification(filename, newFile ? SyncNotification.NewDownloadFail : SyncNotification.UpdateDownloadFail);
                return;
            }
            try
            {
                _fileManager.TryToSaveFile(filename, fileContent);
            }
            catch
            {
                userNotification(filename, newFile ? SyncNotification.ErrorWritingFile : SyncNotification.ErrorOverwritingFile);
                return;
            }
            _folderSyncState.NewFileLocalAndServer(filename, CRC);
            userNotification(filename, newFile ? SyncNotification.SuccessfulNewDownload : SyncNotification.SuccessfulUpdateDownload);
        }
        private async Task operationDeleteFileAsync(string filename, string CRC, bool ignoreCRCCheck = false)
        {
            if (_fileManager.Exists(filename))
            {
                if (!ignoreCRCCheck && !_fileManager.GetHash(filename).Equals(CRC)) //Local file is different that Server File deleted
                {
                    userConfirmationCall(new SyncConflict() { Filename = filename, CRC = CRC, ConflictType = SyncConflictType.RemoteFileDeletedAndLocalIsNewer });
                    return;
                }

                try
                {
                    _fileManager.TryToDeleteFile(filename);
                }
                catch
                {
                    userNotification(filename, SyncNotification.LocalDeleteFail);
                    return;
                }
                _folderSyncState.DeleteFileLocalAndServer(filename);
                userNotification(filename, SyncNotification.SuccessfulLocalDelete);
            }
        }
        #endregion ServerOperations

        #region LocalOperations
        private async Task operationUploadFileAsync(string filename, bool overwrite = false)
        {
            if (!_fileManager.Exists(filename))
                return;

            //Get File Content
            byte[] fileContent = new byte[10];
            string CRC = "";
            try
            {
                fileContent = _fileManager.TryToGetContent(filename);
                CRC = _fileManager.GetHash(filename);
                _folderSyncState.UpdateLocalFileStatus(filename, CRC, FileStatusType.Uploading);

            }
            catch (IOException ex)
            {
                userNotification(filename, SyncNotification.ReadLocalFileToUploadFail, ex.Message);
                return;
            }

            //Auto Sync on StartUp
            if (overwrite)
            {
                await _serverManager.UploadOverwriteAsync(filename, fileContent);
                _folderSyncState.NewFileLocalAndServer(filename, CRC);
                userNotification(filename, SyncNotification.AutoSync, "Overwritten on Server");
                return;
            }
            
            //Upload File as Temp and get Guid
            string[] uploadResponse = new string[2];
            try
            {
                uploadResponse = await _serverManager.UploadFileAsync(filename,fileContent, _folderSyncState.GetExpectedServerFileHash(filename));
            }
            catch (HttpRequestException ex)
            {
                userNotification(filename, SyncNotification.UploadFail);
                return;
            }
            string tempGuid = uploadResponse[1];

            //React on Upload Response StatusCode
            switch (uploadResponse[0])
            {
                case "OK": //New File now ComfirmUpload automatically in Server
                    _folderSyncState.NewFileLocalAndServer(filename, CRC);
                    userNotification(filename, SyncNotification.SuccessfulNewUpload);
                    break;
                case "Accepted": //File in Server was previous version, ComfirmUpload was called automatically in Server
                    _folderSyncState.NewFileLocalAndServer(filename, CRC);
                    userNotification(filename, SyncNotification.SuccessfulUpdateUpload);
                    break;
                case "MultipleChoices": //Ambiguous - File in Server is different from expected, the file changed. User must decide
                    userConfirmationCall(new SyncConflict() { Filename = filename, CRC = CRC, TempGuid = tempGuid, ConflictType = SyncConflictType.UploadedLocalFileNewerVersionRemote });
                    break;
                case "NotModified":
                    _folderSyncState.NewFileLocalAndServer(filename, CRC);
                    userNotification(filename, SyncNotification.GeneralInfo, "File Exists on Server");
                    break;
                case "BadRequest":
                    userNotification(filename, SyncNotification.UploadFail, "400");
                    break;
                case "InternalServerError":
                default:
                    userNotification(filename, SyncNotification.UploadFail, "500");
                    break;
            }
        }
        private async Task operationCallDeleteFileAsync(string filename, bool checkHash = true)
        {
            _folderSyncState.DeleteFileInLocalStatus(filename);

            if (await _serverManager.FileExistsAsync(filename))
            {
                HttpStatusCode deleteResponseCode = HttpStatusCode.NoContent;

                try
                {
                    string hash = "";
                    if (_folderSyncState.RemoteFile(filename) != null)
                        hash = _folderSyncState.RemoteFile(filename).Hash();

                    deleteResponseCode = await _serverManager.DeleteFileAsync(filename, checkHash ? hash : null);
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
                    userConfirmationCall(new SyncConflict() { Filename = filename, ConflictType = SyncConflictType.NewerVersionRemoteAndLocalDeleted });
                else
                    userNotification(filename, SyncNotification.ServerDeleteFail, deleteResponseCode.ToString());
            }
            else
            {
                userNotification(filename, SyncNotification.GeneralInfo, "File doesn't exist on Server");
                _folderSyncState.DeleteFileInServerStatus(filename);
            }
        }
        #endregion LocalOperations

        #endregion SyncOperations

        #region UserNotificationAndConflicts

        private void userNotification(string fileName, SyncNotification syncType, string optionalMsg ="")
        {
            OnSyncNotification?.Invoke(fileName, syncType, optionalMsg);
        }

        private void userConfirmationCall(SyncConflict conflict)
        {
            userNotification(conflict.Filename, SyncNotification.UserCallOnConflict, conflict.ConflictType.ToString());
            _conflicts.Add(conflict.ConflictID, conflict);

            if (_userHandledConflicts.Contains(conflict.ConflictType))
                OnConflictConfirmationRequiredToProceed?.Invoke(conflict.Filename, conflict.ConflictType, conflict.ConflictID);
            else
                ProcessConflictAsync(conflict.ConflictID, byDefaultSyncEventAction(conflict.ConflictType));
        }

        public void SetEventsThatRequireUserConfirmation(List<SyncConflictType> handledConflicts)
        {
            SyncConflictType[] list = new SyncConflictType[10];
            handledConflicts.CopyTo(list);

            _userHandledConflicts = list.ToList();
        }

        private OnConflictAction byDefaultSyncEventAction(SyncConflictType syncConflict)
        {
            //This should be configurable somewhere
            switch (syncConflict)
            {
                case SyncConflictType.GeneralConflict:
                    break;
                case SyncConflictType.NewerVersionRemoteAndLocalVersionChanged:
                    break;
                case SyncConflictType.NewerVersionRemoteAndLocalDeleted:
                    break;
                case SyncConflictType.UploadedLocalFileNewerVersionRemote:
                    break;
                case SyncConflictType.RemoteFileDeletedAndLocalIsNewer:
                    break;
                default:
                    return OnConflictAction.SyncFromRemote;
            }
            return OnConflictAction.SyncFromRemote;
        }

        public async Task ProcessConflictAsync(string conflictID, OnConflictAction action)
        {
            SyncConflict conflict = _conflicts[conflictID];

            switch (conflict.ConflictType)
            {
                case SyncConflictType.NewerVersionRemoteAndLocalVersionChanged:
                    switch (action)
                    {
                        case OnConflictAction.SyncFromRemote:
                            operationDownloadFileAsync(conflict.Filename, conflict.CRC, true);
                            break;
                        case OnConflictAction.SyncFromLocal:
                            operationUploadFileAsync(conflict.Filename, true);
                            break;
                        default:
                            break;
                    }
                    break;
                case SyncConflictType.NewerVersionRemoteAndLocalDeleted:
                    switch (action)
                    {
                        case OnConflictAction.SyncFromLocal:
                            operationCallDeleteFileAsync(conflict.Filename,false);
                            break;
                        case OnConflictAction.SyncFromRemote:
                            operationDownloadFileAsync(conflict.Filename, conflict.CRC, true);
                            break;
                        default:
                            break;
                    }
                    break;
                case SyncConflictType.UploadedLocalFileNewerVersionRemote:
                    switch (action)
                    {
                        case OnConflictAction.SyncFromLocal: //Confirm Upload
                            if (await _serverManager.ConfirmUploadAsync(conflict.Filename, conflict.TempGuid))
                            {
                                _folderSyncState.NewFileLocalAndServer(conflict.Filename, conflict.CRC);
                                userNotification(conflict.Filename, SyncNotification.SuccessfulUpdateUpload);
                            }
                            else
                                userNotification(conflict.Filename, SyncNotification.UpdateUploadFail);
                            break;
                        case OnConflictAction.SyncFromRemote: //Delete temp remotely & Download file
                            if (await _serverManager.DeleteTempAsync(conflict.Filename, conflict.TempGuid))
                                userNotification(conflict.Filename, SyncNotification.GeneralInfo, "Upload Aborted");
                            else
                                userNotification(conflict.Filename, SyncNotification.GeneralFail, "Failed to delete temporary file");

                            operationDownloadFileAsync(conflict.Filename, conflict.CRC, true);
                            break;
                        default:
                            break;
                    }
                    break;
                case SyncConflictType.RemoteFileDeletedAndLocalIsNewer:
                    switch (action)
                    {
                        case OnConflictAction.SyncFromRemote:
                            operationDeleteFileAsync(conflict.Filename, conflict.CRC, true);
                            break;
                        case OnConflictAction.SyncFromLocal:
                            operationUploadFileAsync(conflict.Filename, true);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }

            _conflicts.Remove(conflict.ConflictID);
        }
        #endregion UserNotificationAndConflicts

        #region SyncFolderStatus
        private async Task startUpSyncFolderAsync()
        {
            List<FolderFileState> serverCurrentList = await _serverManager.GetServerFolderStatusAsync();
            List<FolderFileState> localCurrentList = _folderSyncState.ReadFolder();

            //Sync Operations
            List<FolderFileState> toUpload = getDifferences(localCurrentList, _folderSyncState.FolderState.LocalFiles().ToList(),true);
            List<FolderFileState> toDownload = getDifferences(serverCurrentList, _folderSyncState.FolderState.RemoteFiles().ToList(),true);
            List<FolderFileState> toDelete = getDifferences(_folderSyncState.FolderState.RemoteFiles().ToList(), serverCurrentList,false); //Local delete based on server info
            List<FolderFileState> toCallDelete = getDifferences(_folderSyncState.FolderState.LocalFiles().ToList(), localCurrentList,false); //Call delete based on local info

            //Analyze Conflicts in Sync Operations
            List<FolderFileState> conflicted = await SameFilenameDiffHashInToUploadAndToDownloadAsync(toUpload, toDownload);
            toUpload.RemoveAll(x => conflicted.Any(y => x.FileName() == y.FileName()));
            toDownload.RemoveAll(x => conflicted.Any(y => x.FileName() == y.FileName()));
            toDelete.RemoveAll(x => conflicted.Any(y => x.FileName() == y.FileName()));
            toCallDelete.RemoveAll(x => conflicted.Any(y => x.FileName() == y.FileName()));

            //Other cross-conflicts:
            //SameFileToUploadAndToDelete
            //SameFileToDownloadAndToCallDelete

            //Execute Sync Operations
            foreach (var f in toDelete)
                operationDeleteFileAsync(f.FileName(), f.Hash());
            
            foreach (var f in toDownload)
                operationDownloadFileAsync(f.FileName(),f.Hash());

            foreach (var f in toUpload)
                operationUploadFileAsync(f.FileName());

            foreach (var f in toCallDelete)
                operationCallDeleteFileAsync(f.FileName());

        }

        private async Task<List<FolderFileState>> SameFilenameDiffHashInToUploadAndToDownloadAsync(List<FolderFileState> toUpload, List<FolderFileState> toDownload)
        {
            //Keep the toDownload info
            List<FolderFileState> conflicted = toDownload.Where(x => toUpload.Any(y => (x.FileName() == y.FileName()) && (x.Hash() != y.Hash()))).ToList();

            //_autoSync.AddRange(conflicted.Select(f => f.FileName()));

            foreach (var c in conflicted)
                userConfirmationCall(new SyncConflict() {Filename = c.FileName(), CRC = c.Hash(), ConflictType = SyncConflictType.NewerVersionRemoteAndLocalVersionChanged });

            return conflicted;
        }

        private List<FolderFileState> getDifferences(List<FolderFileState> filesIn, List<FolderFileState> filesNotIn, bool compareHash)
        {
            if(compareHash)
                return filesIn.Where(x => !filesNotIn.Any(f => (x.FileName() == f.FileName()) && (x.Hash() == f.Hash()))).ToList();
            else
                return filesIn.Where(x => !filesNotIn.Any(f => (x.FileName() == f.FileName()) )).ToList();
        }
        #endregion SyncFolderStatus
    }
}
