using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Data;
using Sitecore.Publishing.Service.Client.Http;
using Sitecore.Publishing.Service.Data;

namespace Sitecore.PublishingService.Foundation.Extensions.Model
{
    public class ChangedItem
    {
        public string Language { get; set; }
        public int Version { get; set; }
        public ID ItemId { get; set; }
        public ItemPath ItemPath { get; set; }
        public string Path { get; set; }
        public ManifestOperationResultType ItemOperationResultType { get; set; }
    }
}