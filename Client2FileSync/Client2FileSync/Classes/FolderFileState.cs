using Client2FileSync.Enums;
using kahua.kdk.property;

namespace Client2FileSync.Classes
{
    public class FolderFileState : Propertied
    {
        static class Names
        {
            public const string FolderFileState = "FolderFileState";
            public const string Filename = "Filename";
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

        public string Filename() { return Properties.String(Names.Filename); }
        public FolderFileState Filename(string filename) { Properties.String(Names.Filename, filename);return this; }

        public string Hash() { return Properties.String(Names.Hash); }
        public FolderFileState Hash(string hash) { Properties.String(Names.Hash, hash);return this; }

        //**************************************************** How to use enum in Propertied?
        public FileStatusType CurrentStatus { get; set; }
    }
}
