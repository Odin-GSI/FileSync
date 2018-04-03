namespace Client2FileSync.Enums
{
    public enum SyncNotification
    {
        GeneralSuccess = 0,

        //Success
        SuccessfulNewDownload = 1,
        SuccessfulNewUpload = 2,
        SuccessfulUpdateDownload = 3,
        SuccessfulUpdateUpload = 4,
        SuccessfulLocalDelete = 5,
        SuccessfulServerDelete = 6,

        //Fail
        ConfirmUploadFail = 50,
        NewDownloadFail = 51,
        UploadFail = 52,
        UpdateDownloadFail = 53,
        UpdateUploadFail = 54,
        LocalDeleteFail = 55,
        ServerDeleteFail = 56,
        SuccessfulNewDownloadLocalFileSaveFail = 57,
        SuccessfulUpdateDownloadLocalFileSaveFail = 58,
        ReadLocalFileToUploadFail = 59,

        GeneralInfo = 98,
        GeneralFail = 99,

        //Conflicts
        GeneralConflict = 100,
        NewerVersionOnServerAndLocalVersionChanged = 101,
        LocalFileLocked = 102
    }
}
