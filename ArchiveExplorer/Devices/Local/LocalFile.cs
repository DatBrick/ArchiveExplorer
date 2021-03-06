﻿using System.IO;

namespace Archive.Local
{
    public class LocalFile : BasicFile
    {
        protected FileInfo info_;

        public override string Name => info_.Name;
        public override IDevice Device { get; }
        public override IDirectory Parent => new LocalDirectory(info_.Directory, Device);

        public override long Length
        {
            get
            {
                info_.Refresh();

                return info_.Length;
            }
        }

        public override FileLocation Location => FileLocation.Local;
        public override bool Compressed => false;

        public LocalFile(FileInfo info, IDevice device)
        {
            info_ = info;
            Device = device;
        }

        public override Stream Open(FileMode mode, FileAccess access, FileShare share)
        {
            return info_.Open(mode, access, share);
        }

        #if false
        public override void CopyTo(IFile dest)
        {
            if (dest is LocalFile local)
            {
                info_.CopyTo(local.info_.FullName, true);
            }
            else
            {
                base.CopyTo(dest);
            }
        }
        #endif
    }
}