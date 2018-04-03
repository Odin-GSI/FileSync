using Client2FileSync.Classes;
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
        FolderSynchronizer folderSynchronizer;
        public event PropertyChangedEventHandler PropertyChanged;
        private string _signalRStatus = "";

        public MainViewModel()
        {
            folderSynchronizer = new FolderSynchronizer(ConfigurationManager.AppSettings["WepApiURL"].ToString());
            folderSynchronizer.StartSignalR(ConfigurationManager.AppSettings["SignalRHost"].ToString(), ConfigurationManager.AppSettings["SignalRHub"].ToString());

            folderSynchronizer.OnSignalRConnectionStateChanged += FolderSynchronizer_OnSignalRConnectionStateChanged;

            string syncClientFolder = ConfigurationManager.AppSettings["SyncFolder"].ToString();
            folderSynchronizer.StartWatcher(syncClientFolder, new FileSystemFileManager(syncClientFolder));
        }
        
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

        public void raisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
