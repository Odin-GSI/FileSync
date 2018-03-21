using Microsoft.AspNet.SignalR;
using ServerFileSync.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ServerFileSync
{
    public class FileSyncHubWrapper:IFileNotifier
    {
        //Singleton
       // private readonly static Lazy<FileSyncHubWrapper> _instance = new Lazy<FileSyncHubWrapper>(
       //() => new FileSyncHubWrapper(GlobalHost.ConnectionManager.GetHubContext<FileSyncHub>()));

        private static IHubContext _context;
        private static FileSyncHubWrapper _singleton;

        /// <summary>
        /// For testing porpouse only. Should not be used on real situations.
        /// </summary>
        public FileSyncHubWrapper()
        {

        }

        private FileSyncHubWrapper(IHubContext context)
        {
            _context = context;
        }

        public static FileSyncHubWrapper Instance
        {
            get
            {
                if (_singleton != null)
                    return _singleton;
                else
                {
                    _singleton = new FileSyncHubWrapper(GlobalHost.ConnectionManager.GetHubContext<FileSyncHub>());
                    return _singleton;
                }
            }
            
        }

        // Send a Delete File Signal
        public void NotifyDeleteFile(string fileName)
        {
            _context.Clients.All.DeleteFileNotification(fileName);
        }

        // Send a New File Signal
        public void NotifyNewFile(string fileName, string CRC)
        {
            _context.Clients.All.NewFileNotification(fileName,CRC);
        }
    }
}