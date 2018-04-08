using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderSynchronizer.Enums
{
    public enum SyncConflict
    {
        //Conflicts
        GeneralConflict = 0,
        
        NewerVersionOnServerAndLocalVersionChanged = 1,
        NewerVersionOnServerAndLocalDeleted = 2,
        NewLocalFileOtherVersionOnServer = 3,
        FileDeletedOnServerIsNewerVersion = 4,
        FileDeletedOnServerndLocalIsNewer = 5,
        NewerVersionOnServerDeletedAndLocalChanged = 6,

        LocalFileLocked = 99
    }
}
