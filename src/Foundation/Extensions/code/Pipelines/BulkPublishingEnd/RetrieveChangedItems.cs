using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Sitecore.Data;
using Sitecore.Data.Archiving;
using Sitecore.Framework.Conditions;
using Sitecore.Publishing.Service.Client.Http;
using Sitecore.Publishing.Service.Client.Http.Manifest;
using Sitecore.Publishing.Service.Pipelines.BulkPublishingEnd;
using Sitecore.Publishing.Service.SitecoreAbstractions;
using Sitecore.PublishingService.Foundation.Extensions.Model;

namespace Sitecore.PublishingService.Foundation.Extensions.Pipelines.BulkPublishingEnd
{
    public class RetrieveChangedItems
    {
        private readonly IPublishingLog _publishingLog;
        private readonly IDatabaseFactory _databaseFactory;

        [ExcludeFromCodeCoverage]
        public RetrieveChangedItems() : this(new PublishingLogWrapper(), new DatabaseFactoryWrapper(new PublishingLogWrapper())) {}

        [ExcludeFromCodeCoverage]
        public RetrieveChangedItems(IPublishingLog publishingLog, IDatabaseFactory databaseFactory)
        {
            Condition.Requires(publishingLog, nameof(publishingLog)).IsNotNull();
            Condition.Requires(databaseFactory, nameof(databaseFactory)).IsNotNull();
            _publishingLog = publishingLog;
            _databaseFactory = databaseFactory;
        }
        public void Process(PublishEndResultBatchArgs args)
        {
            if (args.TotalResultCount.Equals(0)) return;

            _publishingLog.Debug("Processing Published Items and Transforming into a list of ChangedItem");
            Task.WaitAll(ProcessChangedItems(args));
        }

        public virtual async Task ProcessChangedItems(PublishEndResultBatchArgs args)
        {
            var changedItems = new List<ChangedItem>();
            var sourceDatabase = _databaseFactory.GetDatabase(args.JobData.SourceDatabaseName);
            var targetDatabase = _databaseFactory.GetDatabase(args.TargetInfo.TargetDatabaseName);

            foreach (var itemResult in args.Batch.ToList())
            {
                ChangedItem changedItem;
                // Item Deleted
                if (itemResult.Type == ManifestOperationResultType.Deleted && itemResult.Metadata.VarianceChanges.Length.Equals(0))
                {
                    changedItem = ProcessDeletedItem(sourceDatabase, itemResult);
                }
                else
                {
                    changedItem = ProcessChangedItem(targetDatabase, itemResult);
                }
                if (changedItem != null)
                    changedItems.Add(changedItem);
            }

            if (changedItems.Count > 0)
            {
                // Insert specific things that you would like to do
                // Examples: pass the changedItems to another custom pipeline, raise more events, or custom remote events with the changedItems as part of EventData
            }
        }

        public ChangedItem ProcessDeletedItem(IDatabase sourceDatabase, ManifestOperationResult<ItemResult> itemResult)
        {
            var itemId = ID.Parse(itemResult.EntityId);

            // Try to retrieve straight as an item. If an item is found from the source database, it will most likely have something to do with Publishing Restrictions imposed to the item
            var item = sourceDatabase.GetItem(itemId);
            if (item == null)
            {
                ArchiveEntry archiveEntry;

                // Try to retrieve from Recycle Bin
                var recyclebin = ArchiveManager.GetArchive("recyclebin", sourceDatabase.Database);
                var recyclebinId = recyclebin.GetArchivalId(itemId);
                var recyclebinItem = recyclebin.GetEntries(ID.Parse(recyclebinId)).Where(ent => ent.ItemId == itemId)
                    .OrderByDescending(x => x.ArchiveDate).FirstOrDefault();

                if (recyclebinItem != null)
                {
                    _publishingLog.Debug(string.Format("Item {0} found in Recycle Bin", itemId));
                    archiveEntry = recyclebinItem;
                }
                else
                {
                    // Try to retrieve from Archive
                    var archive = ArchiveManager.GetArchive("archive", sourceDatabase.Database);
                    var archivalId = archive.GetArchivalId(itemId);
                    var archiveItem = archive.GetEntries(ID.Parse(archivalId)).Where(ent => ent.ItemId == itemId)
                        .OrderByDescending(x => x.ArchiveDate).FirstOrDefault();

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
                    ItemId = itemId, ItemOperationResultType = itemResult.Type, Path = archiveEntry.OriginalLocation
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
        public ChangedItem ProcessChangedItem(IDatabase targetDatabase, ManifestOperationResult<ItemResult> itemResult)
        {
            var itemId = ID.Parse(itemResult.EntityId);
            var item = targetDatabase.GetItem(itemId);
            
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

            _publishingLog.Debug(string.Format("Item {0} found in target database {1}", itemId, targetDatabase.Name));
            return changedItem;
        }
    }
}