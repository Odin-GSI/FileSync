﻿using FolderSynchronizer.Enums;
using FolderSynchronizer.Interfaces;
using FolderSynchronizer.FolderStateModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace FolderSynchronizer.Classes
{
    class FolderStatusManager
    {
        private string _statusFileFolderName = "FolderState";
        private string _statusFileName = ".folderState";
        private string _folderStateSaveFilePath;
        private FolderState _folderState;
        private bool _createdFromFile = false;
        private IFileManager _fileManager;

        public FolderStatusManager(string localFolderPath, string remoteFolderName, IFileManager fileManager)
        {
            _folderStateSaveFilePath = "\\"+_statusFileFolderName+ "\\"+_statusFileName;
            _folderState = new FolderState();
            _fileManager = fileManager;
            
            if (_fileManager.Exists(_folderStateSaveFilePath))
            {
                _folderState.Definition = _fileManager.GetContent(_folderStateSaveFilePath);
                _createdFromFile = true;
                return;
            }
            
            if (_fileManager.GetFilenames().Count() > 0)
                throw new InvalidDataException("New Sync Folder must be empty");

            _folderState
                .LocalPath(localFolderPath)
                .RemotePath(remoteFolderName);

            //SaveFolderState(_folderState.LocalPath());
            SaveStatus();
        }

        public string GetStatusFileFolderName { get { return _statusFileFolderName; } }
        public string GetStatusFileName { get { return _statusFileName; } }

        public bool WasCreatedFromFile { get { return _createdFromFile; } }

        public List<FolderFileState> ReadFolder()
        {
            List<FolderFileState> result = new List<FolderFileState>();
            var files = _fileManager.GetFilenames();

            foreach(string file in files)
                if("\\"+file!=_folderStateSaveFilePath)
                    result.Add(new FolderFileState()
                            .FileName(file)
                            .Hash(_fileManager.GetHash(file))
                            .CurrentStatus(FileStatusType.Synced));

            return result;
        }

        public FolderState FolderState { get { return _folderState; } }
        
        public FolderFileState RemoteFile(string filename) { return _folderState.RemoteFiles()?.FirstOrDefault(x => x.FileName() == filename); }
        public FolderFileState LocalFile(string filename) { return _folderState.LocalFiles()?.FirstOrDefault(x => x.FileName() == filename); }

        public void UpdateServerFileStatus(string fileName, string CRC, FileStatusType status)
        {
            Console.WriteLine("FolderStatus - UpdateServerFileStatus");
            DeleteFileInServerStatus(fileName);

            _folderState.RemoteFile(new FolderFileState()
                                        .FileName(fileName)
                                        .Hash(CRC)
                                        .CurrentStatus(status));
            Console.WriteLine("FolderStatus - UpdateServerFileStatus - before SaveStatus");
            SaveStatus();
            Console.WriteLine("FolderStatus - UpdateServerFileStatus - after SaveStatus");
        }
        public void UpdateLocalFileStatus(string fileName, string CRC, FileStatusType status)
        {
            Console.WriteLine("FolderStatus - UpdateLocalFileStatus");
            DeleteFileInLocalStatus(fileName);

            _folderState.LocalFile(new FolderFileState()
                                        .FileName(fileName)
                                        .Hash(CRC)
                                        .CurrentStatus(status));
            Console.WriteLine("FolderStatus - UpdateLocalFileStatus - before SaveStatus");
            SaveStatus();
            Console.WriteLine("FolderStatus - UpdateLocalFileStatus - after SaveStatus");
        }
        public void DeleteFileInServerStatus(string fileName)
        {
            FolderFileState fileState = RemoteFile(fileName);

            if (fileState != null)
                _folderState.RemoveRemoteFile(fileState);

            SaveStatus();
        }
        public void DeleteFileInLocalStatus(string fileName)
        {
            FolderFileState fileState = LocalFile(fileName);

            if (fileState != null)
                _folderState.RemoveLocalFile(fileState);

            SaveStatus();
        }

        public void SaveStatus()
        {
            _fileManager.SaveFolderState(_folderStateSaveFilePath, _folderState.Definition);
            //File.WriteAllText(_folderState.LocalPath() + _folderStateSaveFilePath, _folderState.Definition);
        }

        public void DeleteFileLocalAndServer(string fileName)
        {
            DeleteFileInServerStatus(fileName);
            DeleteFileInLocalStatus(fileName);
        }

        public void NewFileLocalAndServer(string fileName,string CRC)
        {
            Console.WriteLine("FolderStatus - before NewFileLocalAndServer");
            UpdateLocalFileStatus(fileName, CRC, FileStatusType.Synced);
            UpdateServerFileStatus(fileName, CRC, FileStatusType.Synced);
            Console.WriteLine("FolderStatus - after NewFileLocalAndServer");
        }

        public string GetExpectedServerFileHash(string fileName)
        {
            string s = RemoteFile(fileName)?.Hash();
            if (s is null)
                s = "NewFile";
            return s;
        }
    }
}
