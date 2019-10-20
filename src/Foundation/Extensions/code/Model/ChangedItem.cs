using System.Collections.Generic;
using Sitecore.Data;
using Sitecore.Publishing.Service.Client.Http;

namespace Sitecore.PublishingService.Foundation.Extensions.Model
{
    /// <summary>
    /// This is a flatten view of the items that were published.
    /// </summary>
    public class ChangedItem
    {
        public string Language { get; set; }
        public int Version { get; set; }
        public ID ItemId { get; set; }
        public ItemPath ItemPath { get; set; }
        public string Path { get; set; }
        public ManifestOperationResultType ItemOperationResultType { get; set; }
        public ResultChangeType ResultChangeType { get; set; }
        public IEnumerable<FieldResult> FieldChanges { get; set; }
    }
}