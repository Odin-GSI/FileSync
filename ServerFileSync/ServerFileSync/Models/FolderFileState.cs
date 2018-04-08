using kahua.kdk.property;

namespace ServerFileSync.Models
{
    public class FolderFileState : Propertied
    {
        static class Names
        {
            public const string FolderFileState = "FolderFileState";
            public const string FileName = "FileName";
            public const string Hash = "Hash";
            public const string CurrentStatus = "CurrentStatus";
        }

        #region Constructors
        public FolderFileState()
           : base(Names.FolderFileState)
        {

        }

        public FolderFileState(PropertyCollection properties)
            : base(properties)
        {

        }
        #endregion Constructors

        #region FileName
        public string FileName() { return Properties.String(Names.FileName); }
        public FolderFileState FileName(string filename) { Properties.String(Names.FileName, filename);return this; }
        #endregion FileName

        #region Hash
        public string Hash() { return Properties.String(Names.Hash); }
        public FolderFileState Hash(string hash) { Properties.String(Names.Hash, hash);return this; }
        #endregion Hash

        #region CurrentStatus Enum
        public FileStatusType CurrentStatus() { return Properties.Enum<FileStatusType>(Names.CurrentStatus); }
        public FolderFileState CurrentStatus(FileStatusType fileStatusType) { Properties.Enum(Names.CurrentStatus, fileStatusType);return this; }
        #endregion CurrentStatus Enum
    }
}
