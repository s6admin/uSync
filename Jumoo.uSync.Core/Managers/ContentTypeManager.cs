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
using Jumoo.uSync.Core.Interfaces;

namespace Jumoo.uSync.Core.IO
{
    public class ContentTypeManager : BaseSyncIOManager<IContentType>, ISyncIOManager
    {
        public Guid Key => Guid.Parse("588C2A64-4A76-4493-91B6-8CB8DCF21EC6");
        public string Name => "ContentTypeManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }
        public Type ItemType => typeof(IContentType);

        private readonly IContentTypeService contentTypeService;

        public ContentTypeManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.DocumentType;
            containerType = UmbracoObjectTypes.DocumentTypeContainer;
            contentTypeService = serviceContext.ContentTypeService;
        }

        public override SyncAttempt<IContentType> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.ContentTypeSerializer.DeSerialize(node, force);

            return SyncAttempt<IContentType>.Fail(file, ChangeType.ImportFail);
        }


        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var item = contentTypeService.GetContentType(key);

                if (item != null)
                {
                    contentTypeService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(IContentType), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(IContentType), ChangeType.Delete, "Not found");
                   
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var item = contentTypeService.GetContentType(key);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(IContentType), "Item not set");

            try
            {
                var attempt = uSyncContext.ContentTypeSerializer.Serialize(item);
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
                logger.Warn<ContentTypeManager>("Error saving content type {0}", () => ex.ToString());
                return uSyncAction.Fail(item.Name, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.ContentSerializer.IsUpdate(node);

            var action = uSyncActionHelper<IContentType>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.ContentTypeSerializer).GetChanges(node);

            return action;
        }

        public override IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions)
        {
            if (actions.Any() && actions.Any(x => x.ItemType == typeof(IContentType)))
            {
                return CleanEmptyContainers(folder, -1);
            }
            return null;
        }

        protected override bool RemoveContainer(int id)
        {
            var attempt = contentTypeService.DeleteContentTypeContainer(id);
            return attempt.Success;
        }

    }
}
