using kahua.kdk.property;
using System;
using System.Collections.Generic;

namespace Client2FileSync.Classes
{
    public class FolderState : PropertiedCollection<FolderFileState>
    {
        #region Constructors
        public FolderState(string name)
        : base(name, createFileState)
        {


        }

        public FolderState(PropertyCollection properties)
            : base(properties, createFileState)
        {


        }
        #endregion Constructors

        static FolderFileState createFileState(PropertyCollection properties) { return new FolderFileState(properties); }

        //****************************************************************** Revise use of List with Propertied
        public List<FolderFileState> RemoteFiles { get; set; }

        public List<FolderFileState> LocalFiles { get; set; }
    }
}
