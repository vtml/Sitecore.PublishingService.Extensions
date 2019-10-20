using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Sitecore.Data;
using Sitecore.Data.Archiving;

namespace Sitecore.PublishingService.Foundation.Extensions.Model.Wrappers
{
    /// <summary>
    /// This is a wrapper for ArchiveManager so that any implementation of this manager wrapper can be unit testable.
    /// </summary>
    public interface IArchiveManagerWrapper
    {
        /// <summary>
        /// Getting an entry from either Recycle Bin or Archive
        /// </summary>
        /// <param name="archiveName">The name of the archive to retrieve. This should be either 'recyclebin' or 'archive'. TODO: make this an enum.</param>
        /// <param name="database">The database where the Archive is stored. Usually 'master'. <see cref="Database"/></param>
        /// <param name="itemId">The original Item ID before it was archived. <see cref="ID"/></param>
        /// <returns></returns>
        IEnumerable<ArchiveEntry> GetEntries(string archiveName, Database database, ID itemId);
    }

    ///<inheritdoc cref="IArchiveManagerWrapper"/>
    public class ArchiveManagerWrapper : IArchiveManagerWrapper
    {
        ///<inheritdoc cref="IArchiveManagerWrapper"/>
        [ExcludeFromCodeCoverage]
        public IEnumerable<ArchiveEntry> GetEntries(string archiveName, Database database, ID itemId)
        {
            var archive = ArchiveManager.GetArchive(archiveName, database);
            var archiveId = archive.GetArchivalId(itemId);
            return archive.GetEntries(ID.Parse(archiveId));
        }
    }
}