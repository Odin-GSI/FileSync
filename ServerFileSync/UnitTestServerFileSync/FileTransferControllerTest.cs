using System;
using System.IO;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using ServerFileSync;
using ServerFileSync.Controllers;
using ServerFileSync.Interfaces;

namespace UnitTestServerFileSync
{
    [TestClass]
    public class FileTransferControllerTest
    {
        #region Class vars
        FileTransferController fileController;
        Mock<IFileManager> mockFileManager;
        Mock<IFileNotifier> mockHubWrapper;
        string root;
        #endregion Class vars

        #region StartUpTearDown
        [TestInitialize]
        public void Startup()
        {
            mockFileManager = new Mock<IFileManager>();
            //mockFileManager.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
            var mockFileManagerOject = mockFileManager.Object;


            mockHubWrapper = new Mock<IFileNotifier>();
            var mockHubWrapperObject = mockHubWrapper.Object;

            root = @"C:\SyncFolders\ServerFolder";
            fileController = new FileTransferController(mockFileManagerOject, mockHubWrapperObject, root);
        }

        //[TestCleanup]
        //public void TearDown()
        //{
        //    mockFileManager = null;
        //    mockHubWrapper = null;
        //    root = null;
        //    fileController = null;
        //}
        #endregion StartUpTearDown

        #region ConfirmSave
        [TestMethod]
        public void ConfirmSave_ParamsOK_CallsFileConfirmSave()
        {
            //ARRANGE
            string filename = "myFile";
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            Guid tempGuid = Guid.NewGuid();
            fileController.Request.Content = new StringContent(tempGuid.ToString());
            mockFileManager.Setup(x => x.ExistsTemp(filename, tempGuid)).Returns(true);

            //ACT
            var result = fileController.ConfirmSave(filename);

            //ASSERT
            mockFileManager.Verify(x => x.ConfirmSave(filename, tempGuid), Times.Once);
        }
        #endregion ConfirmSave

        #region Upload
        [TestMethod]
        public void Upload_NoFileName_ReturnsBadRequest()
        {
            //ARRANGE
            byte[] fileBytes = new byte[] { 1, 2, 3 };
            HttpContent fileContent = new ByteArrayContent(fileBytes);

            fileController.Request = new System.Net.Http.HttpRequestMessage();
            fileController.Request.Content = new MultipartFormDataContent()
                                                        {
                                                            //Intentionally missing FileName
                                                            //{ new StringContent(fileName),fileName},
                                                            { fileContent, "file", "MyFile.txt" }
                                                        };


            //ACT
            var result = fileController.Upload().Result;

            //ASSERT
            mockFileManager.Verify(x => x.Save(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.Never);
            mockHubWrapper.Verify(x => x.NotifyNewFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.AreEqual(result.StatusCode, System.Net.HttpStatusCode.BadRequest);
        }

        [TestMethod]
        public void Upload_NoFile_ReturnsBadRequest()
        {
            //ARRANGE            
            var fileName = "MyFile.txt";

            fileController.Request = new System.Net.Http.HttpRequestMessage();
            fileController.Request.Content = new MultipartFormDataContent()
                                                        {
                                                            { new StringContent(fileName),fileName},
                                                            //Intentionally missing File
                                                            //{ fileContent, "file", fileName }
                                                        };

            //ACT
            var result = fileController.Upload().Result;

            //ASSERT
            mockFileManager.Verify(x => x.Save(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<bool>()), Times.Never);
            mockHubWrapper.Verify(x => x.NotifyNewFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            Assert.AreEqual(result.StatusCode, System.Net.HttpStatusCode.BadRequest);
        }
        
        [TestMethod]        
        public void Upload_ParamsOK_SaveNotifyAndReturnsOK()
        {
            //ARRANGE
            byte[] fileBytes = new byte[] { 1, 2, 3 };
            HttpContent fileContent = new ByteArrayContent(fileBytes);
            var fileName = "MyFile.txt";

            fileController.Request = new System.Net.Http.HttpRequestMessage();
            fileController.Request.Content = new MultipartFormDataContent()
                                                        {
                                                            { new StringContent(fileName),fileName},
                                                            { fileContent, "file", fileName }
                                                        };

            //ACT
            var result = fileController.Upload().Result;

            //ASSERT
            mockFileManager.Verify(x => x.Save(fileName, fileBytes, true), Times.Once);
            mockHubWrapper.Verify(x => x.NotifyNewFile(fileName, It.IsAny<string>()), Times.Once);
            Assert.AreEqual(result.StatusCode, System.Net.HttpStatusCode.OK);
        }
        #endregion Upload

        #region Exists
        [TestMethod]
        public void Exists_EmptyParam_ReturnsBadRequest()
        {
            //ARRANGE

            //ACT
            var result = fileController.Exists("");

            //ASSERT
            mockFileManager.Verify(x => x.Exists(It.IsAny<string>()), Times.Never);
            Assert.AreEqual(result.StatusCode, System.Net.HttpStatusCode.BadRequest);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void Exists_ParamsOK_ReturnsFileManagerExistsMethodOutput(bool existsMethodOutput)
        {
            //ARRANGE
            mockFileManager.Setup(x => x.Exists(It.IsAny<string>())).Returns(existsMethodOutput);

            var fileName = "MyFile.txt";

            //ACT
            var result = fileController.Exists(fileName);

            //ASSERT
            mockFileManager.Verify(x => x.Exists(fileName), Times.Once);
            Assert.AreEqual(result.StatusCode, System.Net.HttpStatusCode.OK);
            Assert.AreEqual(existsMethodOutput, JsonConvert.DeserializeObject<bool>(result.Content.ReadAsStringAsync().Result));
        }
        #endregion Exists

        #region Delete
        [TestMethod]
        public void Delete_EmptyParam_ReturnsBadRequest()
        {
            //ARRANGE
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            
            //ACT
            var result = fileController.Delete(null);

            //ASSERT
            //mockFileManager.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
            Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        }

        public void Delete_EmptyParam_DeleteMethodNotCalled()
        {
            //ARRANGE
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            
            //ACT
            var result = fileController.Delete(null);

            //ASSERT
            mockFileManager.Verify(x => x.Delete(It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void Delete_ParamsOK_ReturnsOK()
        {
            //ARRANGE
            string filename = "myFile";
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            
            //ACT
            var result = fileController.Delete(filename);

            //ASSERT
            Assert.AreEqual(System.Net.HttpStatusCode.OK, result.StatusCode);
        }

        [TestMethod]
        public void Delete_ParamsOK_DeletesFile()
        {
            //ARRANGE
            string filename = "myFile";
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            
            //ACT
            var result = fileController.Delete(filename);

            //ASSERT
            mockFileManager.Verify(x => x.Delete(filename), Times.Once);
        }

        [TestMethod]
        public void Delete_ParamsOK_NotifiesDeletion()
        {
            //ARRANGE
            string filename = "myFile";
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            
            //ACT
            var result = fileController.Delete(filename);

            //ASSERT
            mockHubWrapper.Verify(x => x.NotifyDeleteFile(filename), Times.Once);
        }

        [TestMethod]
        public void Delete_OnIOException_ReturnsInternalServerError()
        {
            //ARRANGE
            string filename = "myFile";
            string excpMsg = "IO Exception message";
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            mockFileManager.Setup(x => x.Delete(It.IsAny<string>())).Throws(new IOException(excpMsg));
            
            //ACT
            var result = fileController.Delete(filename);

            //ASSERT
            Assert.AreEqual(System.Net.HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.IsTrue(result.Content.ReadAsStringAsync().Result.Contains(excpMsg));
        }

        [TestMethod]
        public void Delete_Exception_ReturnsInternalServerError()
        {
            //ARRANGE
            string filename = "myFile";
            string excpMsg = "General Exception message";
            fileController.Request = new System.Net.Http.HttpRequestMessage();
            mockFileManager.Setup(x => x.Delete(It.IsAny<string>())).Throws(new Exception(excpMsg));
            
            //ACT
            var result = fileController.Delete(filename);

            //ASSERT
            Assert.AreEqual(System.Net.HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.IsTrue(result.Content.ReadAsStringAsync().Result.Contains(excpMsg));
        }
        #endregion Delete
    }
}
