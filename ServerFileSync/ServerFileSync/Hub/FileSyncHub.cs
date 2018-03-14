using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ServerFileSync
{
    public class FileSyncHub : Hub<IFileSyncClient>
    {
        
    }

    public interface IFileSyncClient
    {
        void NewFileNotification(string filename, string CRC);
    }
}