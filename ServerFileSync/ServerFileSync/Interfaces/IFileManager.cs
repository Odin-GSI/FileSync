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
        /// <summary>
        /// Saves a byte[] content in the specified uri. If indicated, creates a Temp Name with a generated Guid and returns the Guid.
        /// </summary>
        /// <param name="uri">Location to save the file</param>
        /// <param name="file">Content of the file to be saved</param>
        /// <param name="asTemp">Flag to set if the name will be a temporal name, created with a generated Guid</param>
        /// <returns>If asTemp is set to true, returns the Guid used for the temporal name</returns>
        Guid Save(string uri, byte[] file, bool asTemp = true);

        FileStream GetStream(string uri);

        bool Exists(string uri);

        bool ExistsTemp(string uri, Guid temp);

        void Delete(string uri);

        void DeleteTemp(string uri, Guid temp);

        void Move(string sourceName, string destinyName);

        void ConfirmSave(string uri, Guid temp);

        string GetHash(string fileName);
    }
}
