﻿using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

            var sourceDatabase = _databaseFactory.GetDatabase(args.JobData.SourceDatabaseName);
            var targetDatabase = _databaseFactory.GetDatabase(args.TargetInfo.TargetDatabaseName);
            var changedItems = new List<ChangedItem>();

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
                PublishChangedItemsRemoteEvent(targetDatabase, changedItems);
            }
        }

        public static ChangedItem ProcessDeletedItem(IDatabase sourceDatabase, ManifestOperationResult<ItemResult> itemResult)
        {
            var itemId = ID.Parse(itemResult.EntityId);
            
            var archive = ArchiveManager.GetArchive("archive", sourceDatabase.Database);
            var archivalId = archive.GetArchivalId(itemId);
            var archiveItem = archive.GetEntries(ID.Parse(archivalId)).Where(ent => ent.ItemId == itemId).OrderByDescending(x => x.ArchiveDate).FirstOrDefault();

            var recyclebin = ArchiveManager.GetArchive("recyclebin", sourceDatabase.Database);
            var recyclebinId = recyclebin.GetArchivalId(itemId);
            var recyclebinItem = recyclebin.GetEntries(ID.Parse(recyclebinId)).Where(ent => ent.ItemId == itemId).OrderByDescending(x => x.ArchiveDate).FirstOrDefault(); ;

            if (archiveItem == null && recyclebinItem == null) return null;
            ArchiveEntry archiveEntry;
            if (archiveItem != null && recyclebinItem != null)
            {
                archiveEntry = archiveItem.ArchiveDate > recyclebinItem.ArchiveDate ? archiveItem : recyclebinItem;
            }
            else if (archiveItem != null)
            {
                archiveEntry = archiveItem;
            }
            else
            {
                archiveEntry = recyclebinItem;
            }

            var changedItem = new ChangedItem
            {
                ItemId = itemId, ItemOperationResultType = itemResult.Type, Path = archiveEntry.OriginalLocation
            };

            return changedItem;
        }
        public static ChangedItem ProcessChangedItem(IDatabase targetDatabase, ManifestOperationResult<ItemResult> itemResult)
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
                Version = itemResult.Metadata.VarianceChanges[0].Item2
            };

            return changedItem;
        }

        public void PublishChangedItemsRemoteEvent(IDatabase targetDatabase, IEnumerable<ChangedItem> changedItems)
        {

        }
    }
}