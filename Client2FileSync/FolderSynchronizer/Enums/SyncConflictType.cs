using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderSynchronizer.Enums
{
    public enum SyncConflictType
    {
        //Conflicts
        GeneralConflict = 0,
        
        DownloadingNewerVersionOnServerAndLocalVersionChanged = 1,
        NewerVersionOnServerAndLocalDeleted = 2,
        NewLocalFileOtherVersionOnServer = 3,
        FileDeletedOnServerIsNewerVersion = 4,
        FileDeletedOnServerAndLocalIsNewer = 5,
        NewerVersionOnServerDeletedAndLocalChanged = 6,

        //Cross conflicts on startup
        KeepServerOnDiffFilesServerAndLocal = 50,

        LocalFileLocked = 99
    }
}
