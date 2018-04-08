using kahua.kdk.collection;
using kahua.kdk.property;
using kahua.kdk.resourcebuilder;
using System.Linq;

namespace FolderSynchronizer.FolderStateModel
{
    public class FolderState : Propertied, IBuild
    {
        static class Names
        {
            public const string FolderState = "FolderState";
            public const string RemotePath = "RemotePath";
            public const string LocalPath = "LocalPath";
            public const string RemoteFiles = "RemoteFiles";
            public const string LocalFiles = "LocalFiles";
        }

        #region Constructors
        public FolderState() : base(Names.FolderState) {}

        public FolderState(PropertyCollection properties) : base(properties) {}
        #endregion Constructors

        #region LocalPath
        public string LocalPath() { return Properties.String(Names.LocalPath); }
        public FolderState LocalPath(string localPath) { Properties.String(Names.LocalPath, localPath); return this; }
        #endregion LocalPath

        #region RemotePath
        public string RemotePath() { return Properties.String(Names.RemotePath); }
        public FolderState RemotePath(string remotePath) { Properties.String(Names.RemotePath, remotePath); return this; }
        #endregion RemotePath

        #region Files Lists
        private FolderFileStateCollection _remoteFiles;
        private FolderFileStateCollection _localFiles;

        public IOrdered<FolderFileState> RemoteFiles() { return _remoteFiles ?? OrderedHelper<FolderFileState>.Empty; }
        public IOrdered<FolderFileState> LocalFiles() { return _localFiles ?? OrderedHelper<FolderFileState>.Empty; }

        public FolderState RemoteFile(FolderFileState map)
        {
            createRemoteFiles();
            _remoteFiles.Add(map);
            return this;
        }

        public FolderState LocalFile(FolderFileState map)
        {
            createLocalFiles();
            _localFiles.Add(map);
            return this;
        }

        public FolderState RemoveRemoteFile(FolderFileState bindingSource)
        {
            _remoteFiles.Remove(bindingSource);
            return this;
        }

        public FolderState RemoveLocalFile(FolderFileState bindingSource)
        {
            _localFiles.Remove(bindingSource);
            return this;
        }

        private void createRemoteFiles()
        {
            if (_remoteFiles == null)
            {
                _remoteFiles = new FolderFileStateCollection(Properties.New(Names.RemoteFiles));
            }
        }
        private void createLocalFiles()
        {
            if (_localFiles == null)
            {
                _localFiles = new FolderFileStateCollection(Properties.New(Names.LocalFiles));
            }
        }
        #endregion Files Lists

        #region IBuild
        protected override void onPropertiesSet()
        {
            base.onPropertiesSet();

            var remotefiles = Properties.Properties(Names.RemoteFiles);
            if (remotefiles != null)
            {
                _remoteFiles = new FolderFileStateCollection(remotefiles);
            }

            var localfiles = Properties.Properties(Names.LocalFiles);
            if (localfiles != null)
            {
                _localFiles = new FolderFileStateCollection(localfiles);
            }
        }

        void IBuild.Prepare()
        {
            createRemoteFiles();
            createLocalFiles();
        }
        #endregion IBuild
    }
}
