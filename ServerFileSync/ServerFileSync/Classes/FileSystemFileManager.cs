using ServerFileSync.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        public void Delete(string fileName)
        {
            if (File.Exists(_filePath + "\\" + fileName))
                File.Delete(_filePath + "\\" + fileName);
        }

        public void DeleteTemp(string fileName, Guid tempGuid)
        {
            string fileNameTemp = getTempFileName(fileName, tempGuid);
            if (File.Exists(_filePath + "\\" + fileNameTemp))
                File.Delete(_filePath + "\\" + fileNameTemp);
        }

        public bool Exists(string fileName)
        {
            return File.Exists(_filePath + "\\" + fileName);
        }

        public bool ExistsTemp(string fileName, Guid tempGuid)
        {
            return File.Exists(_filePath + "\\" + getTempFileName(fileName,tempGuid));
        }

        public FileStream GetStream(string fileName)
        {
            return File.Open(_filePath + "\\" + fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public Guid Save(string fileName, byte[] file, bool asTemp)
        {
            if (!asTemp)
            {
                File.WriteAllBytes(_filePath + "\\" + fileName, file);
                return new Guid(new String('0',32));
            }
            else
            {
                Guid tempGuid = Guid.NewGuid();
                File.WriteAllBytes(_filePath + "\\" + getTempFileName(fileName,tempGuid), file);
                return tempGuid;
            }
        }

        public void ConfirmSave(string fileName, Guid tempGuid)
        {
            Move(getTempFileName(fileName,tempGuid),fileName);
        }

        public void Move(string sourceName, string destinyName)
        {
            if (this.Exists(sourceName))
            {
                this.Save(destinyName, File.ReadAllBytes(_filePath + "\\" + sourceName),false);
                this.Delete(sourceName);
            }
        }

        public string GetHash(string fileName)
        {
            byte[] b = System.IO.File.ReadAllBytes(_filePath + "\\" + fileName);
            
            return calculateCRC(b);
        }

        private string calculateCRC(byte[] b)
        {
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(b)).Replace("-", "").ToLowerInvariant();
            }
        }

        public bool SameHash(string fileName, byte[] fileContent)
        {
            try
            {
                return GetHash(fileName).Equals(calculateCRC(fileContent));
            }
            catch
            {
                return false;
            }
        }

        private string getTempFileName(string fileName, Guid tempGuid)
        {
            return fileName + "_Temp_" + tempGuid.ToString();
        }
    }
}