using FolderSynchronizer.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderSynchronizer.Classes
{
    class SyncConflict
    {
        string _id = "";
        public SyncConflict()
        {
            _id = "SC" + Guid.NewGuid();
        }

        public string ConflictID { get { return _id; } }

        public string Filename { get; set; }

        public string CRC { get; set; }

        public string TempGuid { get; set; }

        public SyncConflictType ConflictType { get; set; }
    }
}
