using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Archiving;
using Sitecore.DependencyInjection;
using Sitecore.Framework.Conditions;
using Sitecore.Globalization;
using Sitecore.Publishing.Service.Client.Http;
using Sitecore.Publishing.Service.Client.Http.Manifest;
using Sitecore.Publishing.Service.Pipelines.BulkPublishingEnd;
using Sitecore.Publishing.Service.SitecoreAbstractions;
using Sitecore.PublishingService.Foundation.Extensions.Model;
using Sitecore.PublishingService.Foundation.Extensions.Model.Wrappers;

namespace Sitecore.PublishingService.Foundation.Extensions.Pipelines.BulkPublishingEnd
{
    /// <summary>
    /// This pipeline processor runs in the BulkPublishingEnd pipeline from Sitecore Publishing Service.
    /// The intention is to flatten, simplified what items were published.
    /// Deleted / archived items also loses a lot of context. This processor will inject as much information back as possible.
    /// </summary>
    public class RetrieveChangedItems
    {
        private readonly IPublishingLog _publishingLog;
        private readonly IDatabaseFactory _databaseFactory;
        private readonly IArchiveManagerWrapper _archiveManagerWrapper;

        [ExcludeFromCodeCoverage]
        public RetrieveChangedItems() : this(new PublishingLogWrapper(), new DatabaseFactoryWrapper(new PublishingLogWrapper()), ServiceLocator.ServiceProvider.GetService<IArchiveManagerWrapper>()) {}

        [ExcludeFromCodeCoverage]
        public RetrieveChangedItems(IPublishingLog publishingLog, IDatabaseFactory databaseFactory, IArchiveManagerWrapper archiveManagerWrapper)
        {
            Condition.Requires(publishingLog, nameof(publishingLog)).IsNotNull();
            Condition.Requires(databaseFactory, nameof(databaseFactory)).IsNotNull();
            Condition.Requires(archiveManagerWrapper, nameof(archiveManagerWrapper)).IsNotNull();
            _publishingLog = publishingLog;
            _databaseFactory = databaseFactory;
            _archiveManagerWrapper = archiveManagerWrapper;
        }
        public void Process(PublishEndResultBatchArgs args)
        {
            if (args.TotalResultCount.Equals(0)) return;

            _publishingLog.Debug("Processing Published Items and Transforming into a list of ChangedItem");
            ProcessChangedItems(args);
        }

        /// <summary>
        /// Processing all the published items.
        /// </summary>
        /// <param name="args"><see cref="PublishEndResultBatchArgs"/></param>
        public void ProcessChangedItems(PublishEndResultBatchArgs args)
        {
            var changedItems = new List<ChangedItem>();
            var sourceDatabase = _databaseFactory.GetDatabase(args.JobData.SourceDatabaseName);

            // Process deleted items first
            var itemResults = args.Batch.ToList();
            var deletedResults = itemResults.Where(x => x.Type == ManifestOperationResultType.Deleted);
            var createdModifiedResults = itemResults.Where(x => x.Type != ManifestOperationResultType.Deleted);
            var deletedResultsList = deletedResults.ToList();
            if (deletedResultsList.Any())
            {
                changedItems.AddRange(deletedResultsList.Select(deletedResult => ProcessDeletedItem(sourceDatabase, deletedResult)));
            }

            var createdModifiedResultsList = createdModifiedResults.ToList();
            if (createdModifiedResultsList.Any())
            {
                changedItems.AddRange(createdModifiedResultsList.Select(itemResult => ProcessChangedItem(sourceDatabase, itemResult)).Where(changedItem => changedItem != null));
            }

            if (changedItems.Count > 0)
            {
                // Insert specific things that you would like to do
                // Examples: pass the changedItems to another custom pipeline, raise more events, or custom remote events with the changedItems as part of EventData
            }
        }

        /// <summary>
        /// Processes items or their versions that were deleted from the target database. There are 3 possible scenarios that items or versions are unpublished. Publishing Restrictions, Delete, Archive.
        /// </summary>
        /// <param name="sourceDatabase"><see cref="Database"/></param>
        /// <param name="itemResult">A list of item results which indicates items or their version(s) were removed from the publishing target. <see cref="ManifestOperationResult"/></param>
        /// <returns><see cref="ChangedItem"/></returns>
        public ChangedItem ProcessDeletedItem(IDatabase sourceDatabase, ManifestOperationResult<ItemResult> itemResult)
        {
            var itemId = ID.Parse(itemResult.EntityId);

            // Try to retrieve straight as an item. If an item is found from the source database, it will most likely have something to do with Publishing Restrictions imposed to the item
            var item = sourceDatabase.Database.GetItem(itemId);
            if (item == null)
            {
                ArchiveEntry archiveEntry;

                // Try to retrieve from Recycle Bin
                var recyclebinItem = _archiveManagerWrapper.GetEntries("recyclebin", sourceDatabase.Database, itemId).FirstOrDefault(ent => ent.ItemId == itemId);

                if (recyclebinItem != null)
                {
                    _publishingLog.Debug(string.Format("Item {0} found in Recycle Bin", itemId));
                    archiveEntry = recyclebinItem;
                }
                else
                {
                    // Try to retrieve from Archive
                    var archiveItem = _archiveManagerWrapper.GetEntries("archive", sourceDatabase.Database, itemId).FirstOrDefault(ent => ent.ItemId == itemId);
                    if (archiveItem != null)
                    {
                        _publishingLog.Debug(string.Format("Item {0} found in Archive", itemId));
                        archiveEntry = archiveItem;
                    }
                    else
                    {
                        return null;
                    }
                }

                var changedItem = new ChangedItem
                {
                    ItemId = itemId, ItemOperationResultType = itemResult.Type, Path = archiveEntry.OriginalLocation, ResultChangeType = ResultChangeType.Deleted
                };

                return changedItem;
            }
            else
            {
                _publishingLog.Debug(string.Format("Item {0} found in source databbase {1}", itemId, sourceDatabase.Name));
                var changedItem = new ChangedItem
                {
                    ItemId = itemId,
                    ItemOperationResultType = itemResult.Type,
                    ItemPath = item.Paths,
                    Path = item.Paths.FullPath,
                    FieldChanges = itemResult.Metadata.FieldChanges
                };

                return changedItem;
            }
        }
        /// <summary>
        /// Processes items that were created and modified during publishing.
        /// </summary>
        /// <param name="sourceDatabase">This should be the 'master' database.<see cref="Database"/></param>
        /// <param name="itemResult">A list of item results which indicates items or their version(s) were removed from the publishing target. <see cref="ManifestOperationResult"/></param>
        /// <returns><see cref="ChangedItem"/></returns>
        public ChangedItem ProcessChangedItem(IDatabase sourceDatabase, ManifestOperationResult<ItemResult> itemResult)
        {
            var itemId = ID.Parse(itemResult.EntityId);
            var item = sourceDatabase.Database.GetItem(itemId, Language.Parse(itemResult.Metadata.VarianceChanges[0].Item1), Version.Parse(itemResult.Metadata.VarianceChanges[0].Item2));
            
            if (item == null) return null;
            var changedItem = new ChangedItem
            {
                ItemId = itemId,
                ItemOperationResultType = itemResult.Type,
                ItemPath = item.Paths,
                Path = item.Paths.FullPath,
                Language = itemResult.Metadata.VarianceChanges[0].Item1,
                Version = itemResult.Metadata.VarianceChanges[0].Item2,
                ResultChangeType = itemResult.Metadata.VarianceChanges[0].Item3,
                FieldChanges = itemResult.Metadata.FieldChanges
            };

            _publishingLog.Debug(string.Format("Item {0} found in source database {1}", itemId, sourceDatabase.Name));
            return changedItem;
        }
    }
}