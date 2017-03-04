using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Jumoo.uSync.Core;
using Jumoo.uSync.Core.Extensions;

using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using System.IO;

namespace Jumoo.uSync.IO.Managers
{
    public class DictionaryManager : BaseSyncManager<IDictionaryItem>, ISyncManager
    {
        public Guid Key => Guid.Parse("3E09254D-EB51-4C02-9A72-4C7D4C0480FC");
        public string Name => "DictionaryManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }

        private readonly ILocalizationService localizationService;
        public Type ItemType => typeof(IDictionaryItem);


        public DictionaryManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.Unknown;
            containerType = UmbracoObjectTypes.Unknown;
            localizationService = serviceContext.LocalizationService;
        }

        public override SyncAttempt<IDictionaryItem> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.DictionarySerializer.DeSerialize(node, force);

            return SyncAttempt<IDictionaryItem>.Fail(file, ChangeType.ImportFail);
        }


        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var item = localizationService.GetDictionaryItemById(key);

                if (item != null)
                {
                    localizationService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(IDictionaryItem), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(IDictionaryItem), ChangeType.Delete, "Not found");
        }

        public override IEnumerable<uSyncAction> Export(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            foreach(var item in localizationService.GetRootDictionaryItems())
            {
                if (item != null)
                    ExportDictionary(item, folder);
            }

            return actions;
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var item = localizationService.GetDictionaryItemById(key);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(IDictionaryItem), "Item not set");

            return ExportDictionary(item, folder);
        }

        public uSyncAction ExportDictionary(IDictionaryItem item, string folder)
        { 
            try
            {
                var attempt = uSyncContext.DictionarySerializer.Serialize(item);
                var filename = string.Empty;
                if (attempt.Success)
                {
                    filename = this.SavePath(folder, item);
                    SaveNode(attempt.Item, filename);
                }
                return uSyncActionHelper<XElement>.SetAction(attempt, filename);
            }
            catch(Exception ex)
            {
                logger.Warn<DataTypeManager>("Error saving dictionary item {0}", () => ex.ToString());
                return uSyncAction.Fail(item.ItemKey, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.DictionarySerializer.IsUpdate(node);

            var action = uSyncActionHelper<IDictionaryItem>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.DictionarySerializer).GetChanges(node);

            return action;
        }
    }
}
