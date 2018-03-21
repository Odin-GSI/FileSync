using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerFileSync.Interfaces
{
    public interface IFileManager
    {
        void Save(string uri, byte[] file);

        FileStream GetStream(string uri);

        bool Exists(string uri);

        void Delete(string uri);

        void Move(string sourceName, string destinyName);
    }
}
