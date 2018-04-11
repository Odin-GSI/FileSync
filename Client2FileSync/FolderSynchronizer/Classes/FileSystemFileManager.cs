using FolderSynchronizer.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace FolderSynchronizer.Classes
{
    public class FileSystemFileManager : IFileManager
    {
        private string _folderPath;
        private string _tempFolderPath;
        public string FilePath { get => _folderPath; set => _folderPath = value; }

        public FileSystemFileManager(string folderPath, bool useTempFolder = true)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            _folderPath = folderPath;

            if (useTempFolder)
            {
                _tempFolderPath = _folderPath + "\\Temp";
                if (!Directory.Exists(_tempFolderPath))
                    Directory.CreateDirectory(_tempFolderPath);
            }
        }

        public void Delete(string fileName)
        {
            if (File.Exists(_folderPath + "\\" + fileName))
                File.Delete(_folderPath + "\\" + fileName);
        }

        public void DeleteTemp(string fileName, Guid tempGuid)
        {
            string fileNameTemp = getTempFileName(fileName, tempGuid);
            if (File.Exists(_tempFolderPath + "\\" + fileNameTemp))
                File.Delete(_tempFolderPath + "\\" + fileNameTemp);
        }

        public bool Exists(string fileName)
        {
            return File.Exists(_folderPath + "\\" + fileName);
        }

        public bool ExistsTemp(string fileName, Guid tempGuid)
        {
            return File.Exists(_tempFolderPath + "\\" + getTempFileName(fileName, tempGuid));
        }

        public FileStream GetStream(string fileName)
        {
            return File.Open(_folderPath + "\\" + fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public string GetContent(string fileName)
        {
            return File.ReadAllText(_folderPath + "\\" + fileName);
        }

        public Guid Save(string fileName, byte[] file, bool asTemp)
        {
            if (!asTemp)
            {
                File.WriteAllBytes(_folderPath + "\\" + fileName, file);
                return new Guid(new String('0', 32));
            }
            else
            {
                Guid tempGuid = Guid.NewGuid();
                File.WriteAllBytes(_tempFolderPath + "\\" + getTempFileName(fileName, tempGuid), file);
                return tempGuid;
            }
        }

        public void ConfirmSave(string fileName, Guid tempGuid)
        {
            Move(_tempFolderPath + "\\" + getTempFileName(fileName, tempGuid), _folderPath + "\\" + fileName);
        }

        public void Move(string sourceName, string destinyName)
        {
            if (File.Exists(sourceName))
            {
                File.WriteAllBytes(destinyName, File.ReadAllBytes(sourceName));
                File.Delete(sourceName);
            }
        }

        public string GetHash(string fileName)
        {
            byte[] b = System.IO.File.ReadAllBytes(_folderPath + "\\" + fileName);

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

        public IEnumerable<string> GetFilenames()
        {
            return Directory.GetFiles(_folderPath).Select(Path.GetFileName);
        }

        public void SaveFolderState(string folderStateSaveFilePath, string folderStateDefinition)
        {
            tryToSaveFolderState(folderStateSaveFilePath, folderStateDefinition);
        }

        private void tryToSaveFolderState(string folderStateSaveFilePath, string folderStateDefinition, int tries = 0)
        {
            try
            {
                File.WriteAllText(_folderPath + folderStateSaveFilePath, folderStateDefinition);
            }
            catch (IOException e)
            {
                if (tries > 50)
                    throw new IOException(e.Message);
                else
                    tryToSaveFolderState(folderStateSaveFilePath, folderStateDefinition, ++tries);
            }
        }

        public byte[] TryToGetContent(string fileName, int tries = 0)
        {
            try
            {
                using (FileStream fs = GetStream(fileName))
                {
                    MemoryStream ms = new MemoryStream();
                    fs.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (IOException e)
            {
                if (tries > 50)
                    throw new IOException(e.Message);
                else
                    return TryToGetContent(fileName, ++tries);
            }
        }
        public void TryToSaveFile(string fileName, byte[] fileContent, int tries = 0)
        {
            try
            {
                File.WriteAllBytes(_folderPath + "\\" + fileName, fileContent);
            }
            catch (IOException e)
            {
                if (tries > 50)
                    throw new IOException(e.Message);
                else
                    TryToSaveFile(fileName, fileContent, ++tries);
            }
        }
        public void TryToDeleteFile(string fileName, int tries = 0)
        {
            try
            {
                Delete(fileName);
            }
            catch (IOException e)
            {
                if (tries > 50)
                    throw new IOException(e.Message);
                else
                    TryToDeleteFile(fileName, ++tries);
            }
        }
    }
}