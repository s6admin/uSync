

namespace Jumoo.uSync.BackOffice.Handlers
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;

    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Core.Logging;

    using Jumoo.uSync.Core;
    using Jumoo.uSync.BackOffice.Helpers;
    using System.Collections.Generic;
    using Core.Extensions;
    using Umbraco.Core.Models.EntityBase;
    public class MediaTypeHandler : uSyncBaseHandler<IMediaType>, ISyncHandler, ISyncPostImportHandler
    {
        public string Name { get { return "uSync: MediaTypeHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.MediaTypes; } }
        public string SyncFolder { get { return "MediaType"; } }

        public MediaTypeHandler()
        {
            RequiresPostProcessing = true;
        }


        public void RegisterEvents()
        {
            ContentTypeService.SavedMediaType += ContentTypeService_SavedMediaType;
            ContentTypeService.MovedMediaType += ContentTypeService_MovedMediaType;
            ContentTypeService.DeletedMediaType += ContentTypeService_DeletedMediaType;
        }


        private void ContentTypeService_DeletedMediaType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IMediaType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<MediaTypeHandler>("Delete: Deleting uSync File for item: {0}", () => item.Name);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, GetItemPath(item), "def");
                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Key, item.Alias, typeof(IMediaType));
            }
        }

        private void ContentTypeService_SavedMediaType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<IMediaType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                SaveMediaType(item);
            }
        }

        private void ContentTypeService_MovedMediaType(IContentTypeService sender, Umbraco.Core.Events.MoveEventArgs<IMediaType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach(var item in e.MoveInfoCollection)
            {
                SaveMediaType(item.Entity);
            }
        }

        private void SaveMediaType(IMediaType item)
        {
            LogHelper.Info<MediaTypeHandler>("Save: Saving uSync file for item: {0}", () => item.Name);
            var action = _ioManager.ExportItem(item.Key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
            if (action.Success)
            {
                NameChecker.ManageOrphanFiles(SyncFolder, item.Key, action.FileName);
            }
        }

        public IEnumerable<uSyncAction> ProcessPostImport(string filepath, IEnumerable<uSyncAction> actions)
        {
            return _ioManager.PostImport(filepath, actions);
        }
    }
}
