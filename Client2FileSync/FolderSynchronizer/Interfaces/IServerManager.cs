﻿using FolderSynchronizer.FolderStateModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FolderSynchronizer.Interfaces
{
    public interface IServerManager
    {
        Task<HttpStatusCode> DeleteFileAsync(string fileName, string hash);
        Task<bool> FileExistsAsync(string fileName);
        Task<bool> ConfirmUploadAsync(string fileName, string tempGuid);
        Task<bool> DeleteTempAsync(string fileName, string tempGuid);
        Task<string[]> UploadFileAsync(string fileName, byte[] fileContent, string previousHash);
        Task<byte[]> DownloadFileAsync(string fileName);
        Task<List<FolderFileState>> GetServerFolderStatusAsync();
        Task<bool> UploadOverwriteAsync(string fileName, byte[] fileContent);
        void SetRemoteSyncFolder(string remoteFolder);
    }
}
