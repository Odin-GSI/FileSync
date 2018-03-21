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
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            this._filePath = folderPath;
        }

        public string FilePath { get => _filePath; set => _filePath = value; }

        public void Delete(string uri)
        {
            if (File.Exists(_filePath + "\\" + uri))
                File.Delete(_filePath + "\\" + uri);
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

        public void Move(string sourceName, string destinyName)
        {
            if (this.Exists(sourceName))
            {
                this.Save(destinyName, File.ReadAllBytes(_filePath + "\\" + sourceName));
                this.Delete(sourceName);
            }
        }
    }
}