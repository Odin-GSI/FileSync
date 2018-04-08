using kahua.kdk.property;

namespace ServerFileSync.Models
{
    public class FolderFileStateCollection : PropertiedCollection<FolderFileState>
    {
        #region Constructors
        public FolderFileStateCollection(string name) : base(name, createFileState) { }

        public FolderFileStateCollection(PropertyCollection properties) : base(properties, createFileState) { }
        #endregion Constructors

        static FolderFileState createFileState(PropertyCollection properties) { return new FolderFileState(properties); }
    }
}
