using FolderSynchronizer.Classes;
using FolderSynchronizer.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Client2FileSync
{
    class MainViewModel : INotifyPropertyChanged
    {
        Synchronizer folderSynchronizer;
        
        public MainViewModel()
        {
            Start();
        }

        public void Start()
        {
            folderSynchronizer = new Synchronizer(ConfigurationManager.AppSettings["WepApiURL"].ToString());
            //folderSynchronizer = new Synchronizer("http://localhost:52051/api/FileTransfer/");

            folderSynchronizer.OnSignalRConnectionStateChanged += FolderSynchronizer_OnSignalRConnectionStateChanged;
            folderSynchronizer.OnConflictConfirmationRequiredToProceed += FolderSynchronizer_OnConflictConfirmationRequiredToProceed;
            folderSynchronizer.OnSyncNotification += FolderSynchronizer_OnSyncNotification;

            string syncClientFolder = ConfigurationManager.AppSettings["localSyncFolder"].ToString();
            string syncServerFolder = ConfigurationManager.AppSettings["remoteSyncFolder"].ToString();
            folderSynchronizer.StartWatcher(syncClientFolder, syncServerFolder, new FileSystemFileManager(syncClientFolder));

            //Call SignalR after Watcher Sync - Allow Client StartUp Sync
            folderSynchronizer.StartSignalR(ConfigurationManager.AppSettings["SignalRHost"].ToString(), ConfigurationManager.AppSettings["SignalRHub"].ToString());
        }

        private bool FolderSynchronizer_OnConflictConfirmationRequiredToProceed(string filename, SyncConflict syncEvent)
        {
            //Could have a list of pre-defined responses that won't be asked to the user
            return System.Windows.MessageBox.Show(getUserMsgForSyncEvent(syncEvent,filename), "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        private string getUserMsgForSyncEvent(SyncConflict syncConflict, string filename)
        {
            //Make a Switch and return a user-friendly msg
            return syncConflict.ToString();
        }

        #region SignalRConnectionState
        private string _signalRStatus = "";
        private void FolderSynchronizer_OnSignalRConnectionStateChanged(string connectionStatus)
        {
            SignalRConnectionStatus = connectionStatus;
        }
        public string SignalRConnectionStatus
        {
            get
            {
                return _signalRStatus;
            }
            set
            {
                _signalRStatus = value;
                raisePropertyChanged("SignalRConnectionStatus");
            }
        }
        #endregion SignalRConnectionState

        #region SyncNotification
        private string _syncNotif = "";
        private void FolderSynchronizer_OnSyncNotification(string fileName, SyncNotification syncNotification, string optionalMsg)
        {
            SyncNotification = "File " + fileName + " " + syncNotification.ToString() + ". " + optionalMsg;
        }
        public string SyncNotification
        {
            get { return _syncNotif; }
            set { _syncNotif = value; raisePropertyChanged("SyncNotification"); }
        }
        #endregion SyncNotification

        #region INotify
        public event PropertyChangedEventHandler PropertyChanged;
        public void raisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion INotify
    }
}
