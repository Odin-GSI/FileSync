using ServerFileSync.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace ServerFileSync
{
    public class FileSystemFileManager : IFileManager
    {
        private string _filePath;

        public FileSystemFileManager(string folderPath)
        {
            this._filePath = folderPath;
        }

        public bool Exists(string fileName)
        {
            return File.Exists(_filePath + "\\" + fileName);
        }

        public FileStream GetStream(string fileName)
        {
            return File.Open(_filePath + "\\" + fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Save(string fileName, byte[] file)
        {
            File.WriteAllBytes(_filePath + "\\" + fileName, file);
        }
    }
}