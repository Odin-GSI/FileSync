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
        private string _wepApiURLtoDownload = ConfigurationManager.AppSettings["WepApiURLtoDownload"].ToString();
        private string _wepApiURLExistsFile = ConfigurationManager.AppSettings["WepApiURLExistsFile"].ToString();
        FileSystemWatcher _watcher;

        public System.Threading.Thread Thread { get; set; }
        //public string Host = "http://localhost:53349/";
        public string Host = "http://localhost/ServerFileSync/";
        public IHubProxy Proxy { get; set; }
        public HubConnection Connection { get; set; }
        public bool Active { get; set; }

        private async void ActionWindowLoaded(object sender, RoutedEventArgs e)
        {
            Active = true;
            Thread = new System.Threading.Thread(() =>
            {
                Connection = new HubConnection(Host);
                Connection.Error += Connection_Error;
                Connection.StateChanged += Connection_StateChanged;
                Proxy = Connection.CreateHubProxy("FileSyncHub");

                Proxy.On<string, string>("NewFileNotification", (fileName, CRC) => OnNewFileNotified(fileName,CRC));

                Connection.Start();

                while (Active)
                {
                    System.Threading.Thread.Sleep(10);
                }
            })
            { IsBackground = true };
            Thread.Start();
        }

        private void OnNewFileNotified(string fileName, string CRC)
        {
            //Check if I have the File
            if(!alreadyHaveFile(fileName, CRC))
                //Download the New File
                getFile(fileName);
        }

        private void Connection_StateChanged(StateChange obj)
        {
            Dispatcher.Invoke(() => infoLabel.Content = "Status: " + obj.NewState);
            //MessageBox.Show("State: " + obj.NewState);
        }

        private void Connection_Error(Exception obj)
        {
            Dispatcher.Invoke(() => infoLabel.Content = "Connection error: " + obj.ToString());
            //MessageBox.Show("Connection error: " + obj.ToString());
        }

        public MainWindow()
        {
            InitializeComponent();

            _watcher = new FileSystemWatcher();
            _watcher.Path = _syncFolder;
            _watcher.NotifyFilter = NotifyFilters.LastWrite;
            _watcher.Filter = "*.*";
            _watcher.Changed += new FileSystemEventHandler(OnWatcherChanged);
            _watcher.EnableRaisingEvents = true;
        }

        private void btnSendTest_Click(object sender, RoutedEventArgs e)
        {
            var filename = "test.txt";

            sendFile(filename);
        }

        private bool alreadyHaveFile(string fileName,string CRC)
        {
            //Include CRC check ************************************************************************** TO DO
            return File.Exists(_syncFolder + "\\" + fileName);
        }

        private async void getFile(string fileName)
        {
            using (var client = new HttpClient())
            {
                var responseStream = await client.GetStreamAsync(_wepApiURLtoDownload +"?fileName="+ fileName);
                using (var fileWritter = File.Create(_syncFolder+"\\"+fileName))
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
            }
        }

        private async Task<bool> fileExists(string fileName, string CRC)
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
                throw new Exception("Can't access file "+file);

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

        private void OnWatcherChanged(object source, FileSystemEventArgs e)
        {
            if (!fileExists(e.Name, "").Result)
                Task.WaitAll(sendFile(e.Name));
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
    }
}