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
        
        NewerVersionRemoteAndLocalVersionChanged = 1,
        NewerVersionRemoteAndLocalDeleted = 2,
        UploadedLocalFileNewerVersionRemote = 3,
        RemoteFileDeletedAndLocalIsNewer = 4
    }
}
