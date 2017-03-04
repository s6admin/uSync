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
    public class LanguageManager : BaseSyncManager<ILanguage>, ISyncManager
    {
        public Guid Key => Guid.Parse("3734EC5C-FA0D-4B37-9D72-CCEEB0038B86");
        public string Name => "LanguageManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }

        private readonly ILocalizationService localizationService;
        public Type ItemType => typeof(ILanguage);

        public LanguageManager(
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

        public override SyncAttempt<ILanguage> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.LanguageSerializer.DeSerialize(node, force);

            return SyncAttempt<ILanguage>.Fail(file, ChangeType.ImportFail);
        }


        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var entity = entityService.GetByKey(key);
                var item = localizationService.GetLanguageById(entity.Id);

                if (item != null)
                {
                    localizationService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(ILanguage), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(ILanguage), ChangeType.Delete, "Not found");
                   
        }

        public override IEnumerable<uSyncAction> Export(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            foreach(var item in localizationService.GetAllLanguages())
            {
                if (item != null)
                    actions.Add(ExportLanguage(item, folder));
            }


            return actions;
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var entity = entityService.GetByKey(key);
            var item = localizationService.GetLanguageById(entity.Id);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(ILanguage), "Item not set");

            return ExportLanguage(item, folder);
        }

        public uSyncAction ExportLanguage(ILanguage item, string folder)
        { 
            try
            {
                var attempt = uSyncContext.LanguageSerializer.Serialize(item);
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
                logger.Warn<LanguageManager>("Error saving Language type {0}", () => ex.ToString());
                return uSyncAction.Fail(item.CultureName, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.LanguageSerializer.IsUpdate(node);

            var action = uSyncActionHelper<ILanguage>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.LanguageSerializer).GetChanges(node);

            return action;
        }
    }
}
