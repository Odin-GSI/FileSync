using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClientWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _syncFolder = ConfigurationManager.AppSettings["SyncFolder"].ToString();
        private string _webApiURLtoLoad = ConfigurationManager.AppSettings["WepApiURLtoLoad"].ToString();
        private string _webApiURLtoConfirmSave = ConfigurationManager.AppSettings["WepApiURLtoConfirmSave"].ToString();
        private string _webApiURLtoDelete = ConfigurationManager.AppSettings["WepApiURLtoDelete"].ToString();
        private string _wepApiURLtoDeleteTemp = ConfigurationManager.AppSettings["WepApiURLtoDeleteTemp"].ToString();
        private string _wepApiURLtoDownload = ConfigurationManager.AppSettings["WepApiURLtoDownload"].ToString();
        private string _wepApiURLExistsFile = ConfigurationManager.AppSettings["WepApiURLExistsFile"].ToString();
        private string _singalRHost = ConfigurationManager.AppSettings["SignalRHost"].ToString();
        FileSystemWatcher _watcher;

        public System.Threading.Thread Thread { get; set; }
        public IHubProxy Proxy { get; set; }
        public HubConnection Connection { get; set; }
        public bool Active { get; set; }

        private async void ActionWindowLoaded(object sender, RoutedEventArgs e)
        {
            Active = true;
            Thread = new System.Threading.Thread(() =>
            {
                Connection = new HubConnection(_singalRHost);
                Connection.Error += Connection_Error;
                Connection.StateChanged += Connection_StateChanged;
                Proxy = Connection.CreateHubProxy("FileSyncHub");

                Proxy.On<string, string>("NewFileNotification", (fileName, CRC) => OnNewFileNotified(fileName, CRC));
                Proxy.On<string>("DeleteFileNotification", (fileName) => OnDeleteFileNotified(fileName));

                Connection.Start();

                while (Active)
                {
                    System.Threading.Thread.Sleep(10);
                }
            })
            { IsBackground = true };
            Thread.Start();
        }

        private void OnDeleteFileNotified(string fileName)
        {
            if(alreadyHaveFile(fileName,""))
            if (System.Windows.MessageBox.Show("File "+fileName+" was deleted in the server. Do you want to delete it locally?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var fullPath = _syncFolder + "\\" + fileName;
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
        }

        private void OnNewFileNotified(string fileName, string CRC)
        {
            //Check if I have the File
            if (!alreadyHaveFile(fileName, CRC))
                //Download the New File
                getFile(fileName);
        }

        private void Connection_StateChanged(StateChange obj)
        {
            Dispatcher.Invoke(() => infoLabel.Content = "Status: " + obj.NewState);
        }

        private void Connection_Error(Exception obj)
        {
            Dispatcher.Invoke(() => infoLabel.Content = "Connection error: " + obj.ToString());
        }

        public MainWindow()
        {
            InitializeComponent();
            startWatcher();
        }

        private void startWatcher()
        {
            if(_watcher!= null)
            {
                _watcher.Deleted -= new FileSystemEventHandler(onWatcherDeleted);
                _watcher.Created -= new FileSystemEventHandler(onWatcherCreated);
                _watcher.Dispose();
                _watcher = null;
            }
            _watcher = new FileSystemWatcher();
            _watcher.Path = _syncFolder;
            CurrentFolder.Content = _syncFolder;
            //_watcher.NotifyFilter = NotifyFilters.LastWrite;
            _watcher.Filter = "*.*";
            //_watcher.Changed += new FileSystemEventHandler(OnWatcherChanged);
            _watcher.Deleted += new FileSystemEventHandler(onWatcherDeleted);
            _watcher.Created += new FileSystemEventHandler(onWatcherCreated);
            _watcher.EnableRaisingEvents = true;
        }

        private void btnSendTest_Click(object sender, RoutedEventArgs e)
        {
            var filename = "test.txt";

            sendFile(filename);
        }

        private bool alreadyHaveFile(string fileName, string CRC)
        {
            //Include CRC check ************************************************************************** TO DO
            return File.Exists(_syncFolder + "\\" + fileName);
        }

        string _downloadedFile = "";
        private async void getFile(string fileName)
        {
            _downloadedFile = fileName;

            using (var client = new HttpClient())
            {
                var responseStream = await client.GetStreamAsync(_wepApiURLtoDownload + "?fileName=" + fileName);
                using (var fileWritter = File.Create(_syncFolder + "\\" + fileName))
                {
                    //responseStream.Seek(0, SeekOrigin.Begin);
                    responseStream.CopyTo(fileWritter);
                }
            }
        }
        private async Task sendFile(string fileName)
        {
            byte[] byteFile = tryToReadFile(_syncFolder + "\\" + fileName);
            HttpContent fileContent = new ByteArrayContent(byteFile);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(_webApiURLtoLoad, new MultipartFormDataContent()
                {
                    { new StringContent(fileName),"fileName"},
                    { fileContent, "file", fileName }
                });
                string tempGuid = response.Content.ReadAsStringAsync().Result;
                string msgBoxText = "";
                if (response.StatusCode == System.Net.HttpStatusCode.Ambiguous)
                    msgBoxText = "File "+fileName+" already exists in server. Do you want to overwrite it?";
                else
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    msgBoxText = "File " + fileName + " does not exists in server. Do you want to add it?";
                
                if (System.Windows.MessageBox.Show(msgBoxText, "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var overwriteResponse = await client.PostAsync(_webApiURLtoConfirmSave + "?fileName=" + fileName, new StringContent(tempGuid));
                }
                else
                {
                    var deletePartialResponse = await client.PutAsync(_wepApiURLtoDeleteTemp + "?fileName=" + fileName, new StringContent(tempGuid));
                }
            }
        }

        private async Task<bool> fileExistsOnServer(string fileName, string CRC)
        {
            bool final;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(_wepApiURLExistsFile + "?fileName=" + fileName);
                var result = await response.Content.ReadAsStringAsync();
                final = JsonConvert.DeserializeObject<bool>(result);
            }

            return final;
        }

        private byte[] tryToReadFile(string file, int tries = 0)
        {
            if (tries == 100)
                throw new Exception("Can't access file " + file);

            byte[] b;
            try
            {
                b = System.IO.File.ReadAllBytes(file);
            }
            catch (IOException)
            {
                Thread.Sleep(500);
                return tryToReadFile(file, ++tries);
            }

            return b;
        }

        //Not being called ATM
        private void onWatcherChanged(object source, FileSystemEventArgs e)
        {
            if (!fileExistsOnServer(e.Name, "").Result)
                Task.WaitAll(sendFile(e.Name));
        }

        private void onWatcherCreated(object source, FileSystemEventArgs e)
        {
            if (_downloadedFile!="")
                if (_downloadedFile == e.Name)
                {
                    _downloadedFile = "";
                    return;
                }
            //if (!fileExistsOnServer(e.Name, "").Result)
            Task.WaitAll(sendFile(e.Name));
        }

        private void onWatcherDeleted(object source, FileSystemEventArgs e)
        {
            if(fileExistsOnServer(e.Name,"").Result)
                if (System.Windows.MessageBox.Show("File "+e.Name+" exists in server. Do you want to delete it?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    Task.WaitAll(deleteFileOnServer(e.Name));
        }

        private async Task deleteFileOnServer(string fileName)
        {
            var lastDotIndex = fileName.LastIndexOf('.');
            var nameLength = lastDotIndex >= 0 ? lastDotIndex : fileName.Length;
            var extensionLength = fileName.Length - 1 - nameLength;
            var name = fileName.Substring(0, nameLength);

            string extension;
            if (lastDotIndex != - 1)
                extension = fileName.Substring(lastDotIndex + 1, extensionLength);
            else
                extension = "";
            using (var client = new HttpClient())
            {
                var response = await client.DeleteAsync(_webApiURLtoDelete + "?filename=" + name + "&extension=" + extension);
            }
        }
        private string calculateMD5(string file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                var result = dialog.ShowDialog();
                if(result == System.Windows.Forms.DialogResult.OK)
                {
                    _syncFolder = dialog.SelectedPath;
                    startWatcher();
                }
            }
        }
    }
}