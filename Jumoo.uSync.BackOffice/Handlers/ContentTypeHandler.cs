
namespace Jumoo.uSync.BackOffice.Handlers
{
    using System;
    using System.Linq;
    using System.Xml.Linq;
    using System.IO;

    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Core.Logging;

    using Jumoo.uSync.Core.Extensions;

    using Jumoo.uSync.Core;
    using Jumoo.uSync.BackOffice.Helpers;
    using System.Collections.Generic;
    using Umbraco.Core.Models.EntityBase;
    using Umbraco.Core.Events;
    using Core.Interfaces;

    public class ContentTypeHandler : uSyncBaseHandler<IContentType>, ISyncHandler, ISyncPostImportHandler
    {
        // sets our running order in usync. 
        public int Priority { get { return uSyncConstants.Priority.ContentTypes; } }
        public string Name { get { return "uSync: ContentTypeHandler"; } }
        public string SyncFolder { get { return Constants.Packaging.DocumentTypeNodeName; } }

        private readonly ISyncIOManager _contentTypeIO; 

        public ContentTypeHandler() 
        {
            RequiresPostProcessing = true;
        }

        public void RegisterEvents()
        {
            ContentTypeService.SavedContentType += ContentTypeService_SavedContentType;
            ContentTypeService.DeletedContentType += ContentTypeService_DeletedContentType;
            ContentTypeService.MovedContentType += ContentTypeService_MovedContentType;
        }

        private void ContentTypeService_DeletedContentType(IContentTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IContentType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<ContentTypeHandler>("Delete: Removing uSync files for Item: {0}", () => item.Name);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, GetItemPath(item), "def");
                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Key, item.Alias, typeof(IContentType));
            }
        }

        private void ContentTypeService_SavedContentType(IContentTypeService sender, Umbraco.Core.Events.SaveEventArgs<IContentType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                SaveContentItem(item);
            }
        }

        private void ContentTypeService_MovedContentType(IContentTypeService sender, MoveEventArgs<IContentType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.MoveInfoCollection)
            {
                SaveContentItem(item.Entity);
            }
        }
    

        private void SaveContentItem(IContentType item)
        {
            LogHelper.Info<ContentTypeHandler>("Save: Saving uSync files for Item: {0}", () => item.Name);
            var action = _ioManager.ExportItem(item.Key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
            if (action.Success)
            {
                NameChecker.ManageOrphanFiles(Constants.Packaging.DocumentTypeNodeName, item.Key, action.FileName);
            }
        }

        public IEnumerable<uSyncAction> ProcessPostImport(string filepath, IEnumerable<uSyncAction> actions)
        {
            return _ioManager.PostImport(filepath, actions);
        }
    }
}
