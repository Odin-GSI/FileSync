using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ServerFileSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestServerFileSync
{
    [TestClass]
    public class FileSystemFileManagerTest
    {
        FileSystemFileManager fileManager;
        string folderPath = @"C:\SyncFolders\ServerFolderForTesting";

        [TestInitialize]
        public void Startup()
        {
            fileManager = new FileSystemFileManager(folderPath);
        }

        [TestCleanup]
        public void TearDown()
        {
            cleanAllFilesInFolderPath();
        }

        private void cleanAllFilesInFolderPath()
        {
            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void Save_OnCreation_FordelPathCreated()
        {
            if (Directory.Exists(folderPath))
                 Directory.Delete(folderPath);
            fileManager = new FileSystemFileManager(folderPath);
            Thread.Sleep(50);
            Assert.IsTrue(Directory.Exists(folderPath));
        }

        [TestMethod]
        public void Save_ParamOK_FileCreated()
        {
            var fileName = "test.txt";
            var file = new byte[] { 1, 2, 3 };
            fileManager.Save(fileName, file);
            Assert.IsTrue(File.Exists(folderPath + "\\" + fileName));
        }

        [TestMethod]
        public void Delete_ParamOK_FileDeleted()
        {
            var fileName = "test.txt";
            File.Create(folderPath + "\\" + fileName).Close();
            fileManager.Delete(fileName);
            Assert.IsFalse(File.Exists(folderPath + "\\" + fileName));
        }

        [TestMethod]
        public void Exists_FileExists_ReturnsTrue()
        {
            var fileName = "test.txt";
            File.Create(folderPath + "\\" + fileName).Close();
            Assert.IsTrue(fileManager.Exists(fileName));
        }

        [TestMethod]
        public void Exists_FileDoesNotExist_ReturnsFalse()
        {
            var fileName = "test.txt";
            Assert.IsFalse(fileManager.Exists(fileName));
        }

        [DataTestMethod]
        [DataRow("test.txt", new byte[] { 1, 2, 3 })]
        [DataRow("test.txt1", new byte[] { 3, 4, 5 })]
        [DataRow("test.txt2", new byte[] { 8, 11, 15 })]
        public void GetStream_ParamOK_FileDeleted(string fileName, byte[] content)
        {
            var fs = File.Create(folderPath + "\\" + fileName);
            fs.Write(content, 0, content.Length);
            fs.Close();
            var result = fileManager.GetStream(fileName);
            var contentToCompare = new byte[content.Length];
            result.Read(contentToCompare, 0, (int)result.Length);
            result.Close();
            var expectedContent = File.ReadAllBytes(folderPath + "\\" + fileName);
            Assert.AreEqual(expectedContent[0], contentToCompare[0]);
            Assert.AreEqual(expectedContent[1], contentToCompare[1]);
            Assert.AreEqual(expectedContent[2], contentToCompare[2]);
        }
    }

}
