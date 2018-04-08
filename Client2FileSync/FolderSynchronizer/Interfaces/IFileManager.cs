using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FolderSynchronizer.Classes
{
    public interface IFileManager
    {
        /// <summary>
        /// Saves a byte[] content in the specified uri. If asTemp is true, sets a temporary filename with a generated Guid and returns the Guid.
        /// </summary>
        /// <param name="uri">Location to save the file</param>
        /// <param name="file">Content of the file to be saved</param>
        /// <param name="asTemp">Flag to set if the name will be a temporary name, created with a generated Guid</param>
        /// <returns>If asTemp is set to true, returns the Guid used for the temporary filename</returns>
        Guid Save(string uri, byte[] file, bool asTemp = true);

        FileStream GetStream(string uri);

        string GetContent(string fileName);

        bool Exists(string uri);

        bool ExistsTemp(string uri, Guid temp);

        void Delete(string uri);

        void DeleteTemp(string uri, Guid temp);

        void Move(string sourceName, string destinyName);

        void ConfirmSave(string uri, Guid temp);

        string GetHash(string fileName);

        bool SameHash(string fileName, byte[] fileContent);

        IEnumerable<string> GetFilenames();
    }
}
