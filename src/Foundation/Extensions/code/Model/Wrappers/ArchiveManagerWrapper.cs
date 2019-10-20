using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Sitecore.Data;
using Sitecore.Data.Archiving;

namespace Sitecore.PublishingService.Foundation.Extensions.Model.Wrappers
{
    public interface IArchiveManagerWrapper
    {
        IEnumerable<ArchiveEntry> GetEntries(string archiveName, Database database, ID itemId);
    }

    public class ArchiveManagerWrapper : IArchiveManagerWrapper
    {
        [ExcludeFromCodeCoverage]
        public IEnumerable<ArchiveEntry> GetEntries(string archiveName, Database database, ID itemId)
        {
            var archive = ArchiveManager.GetArchive(archiveName, database);
            var archiveId = archive.GetArchivalId(itemId);
            return archive.GetEntries(ID.Parse(archiveId));
        }
    }
}