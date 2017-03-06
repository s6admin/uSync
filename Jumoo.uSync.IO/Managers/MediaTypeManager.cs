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
    public class MediaTypeManager : BaseSyncIOManager<IMediaType>, ISyncIOManager
    {
        public Guid Key => Guid.Parse("AA8B9C7D-9346-4F25-B76D-E2AEA2F7F6CF");
        public string Name => "MediaTypeManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }

        private readonly IContentTypeService contentTypeService;
        public Type ItemType => typeof(IMediaType);


        public MediaTypeManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.MediaType;
            containerType = UmbracoObjectTypes.MediaTypeContainer;
            contentTypeService = serviceContext.ContentTypeService;
        }

        public override SyncAttempt<IMediaType> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.MediaTypeSerializer.DeSerialize(node, force);

            return SyncAttempt<IMediaType>.Fail(file, ChangeType.ImportFail);
        }


        public override void ImportItemAgain(string file, IMediaType item)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            uSyncContext.MediaTypeSerializer.DesearlizeSecondPass(item, node);
        }

        public IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions)
        {
            if (actions.Any(x => x.ItemType == typeof(IMediaType)))
            {
                return CleanEmptyContainers(folder, -1);
            }
            return null;
        }

        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var item = contentTypeService.GetMediaType(key);

                if (item != null)
                {
                    contentTypeService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(IMediaType), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(IMediaType), ChangeType.Delete, "Not found");
                   
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var item = contentTypeService.GetMediaType(key);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(IMediaType), "Item not set");

            try
            {
                var attempt = uSyncContext.MediaTypeSerializer.Serialize(item);
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
            var update = uSyncContext.MediaTypeSerializer.IsUpdate(node);

            var action = uSyncActionHelper<IMediaType>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.MediaTypeSerializer).GetChanges(node);

            return action;
        }

        protected override bool RemoveContainer(int id)
        {
            var attempt = contentTypeService.DeleteMediaTypeContainer(id);
            return attempt.Success;
        }

    }
}
