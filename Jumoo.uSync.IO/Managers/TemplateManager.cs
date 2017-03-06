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
    public class TemplateManager : BaseSyncIOManager<ITemplate>, ISyncIOManager
    {
        public Guid Key => Guid.Parse("B20217B2-69BE-4EC8-94F1-780901249AC6");
        public string Name => "TemplateManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }

        private readonly IFileService fileService;
        public Type ItemType => typeof(ITemplate);


        public TemplateManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.Template;
            containerType = UmbracoObjectTypes.Unknown;
            fileService = serviceContext.FileService;

            requiresPostProcessing = true;
        }

        public override SyncAttempt<ITemplate> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.TemplateSerializer.DeSerialize(node, force);

            return SyncAttempt<ITemplate>.Fail(file, ChangeType.ImportFail);
        }

        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var item = fileService.GetTemplate(key);

                if (item != null)
                {
                    fileService.DeleteTemplate(item.Alias);
                    return uSyncAction.SetAction(true, name, typeof(ITemplate), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(ITemplate), ChangeType.Delete, "Not found");
                   
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var item = fileService.GetTemplate(key);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(ITemplate), "Item not set");

            try
            {
                var attempt = uSyncContext.TemplateSerializer.Serialize(item);
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
                logger.Warn<TemplateManager>("Error saving data type {0}", () => ex.ToString());
                return uSyncAction.Fail(item.Name, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.TemplateSerializer.IsUpdate(node);

            var action = uSyncActionHelper<ITemplate>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.TemplateSerializer).GetChanges(node);

            return action;
        }

    }
}
