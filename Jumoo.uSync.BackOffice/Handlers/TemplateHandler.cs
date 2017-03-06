namespace Jumoo.uSync.BackOffice.Handlers
{
    using System;
    using System.Xml.Linq;

    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Core.Logging;

    using Jumoo.uSync.Core;
    using Jumoo.uSync.BackOffice.Helpers;
    using System.Collections.Generic;
    using System.IO;
    using Core.Extensions;

    public class TemplateHandler : uSyncBaseHandler<ITemplate>, ISyncHandler
    {
        public string Name { get { return "uSync: TemplateHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.Templates; } }
        public string SyncFolder { get { return Constants.Packaging.TemplateNodeName; } }
        public void RegisterEvents()
        {
            FileService.SavedTemplate += FileService_SavedTemplate;
            FileService.DeletedTemplate += FileService_DeletedTemplate;
        }

        private void FileService_DeletedTemplate(IFileService sender, Umbraco.Core.Events.DeleteEventArgs<ITemplate> e)
        {
            if (uSyncEvents.Paused)
                return; 

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<TemplateHandler>("Delete: Deleting uSync File for item: {0}", () => item.Name);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, GetItemPath(item));
                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Alias, typeof(ITemplate));
                
            }
        }

        private void FileService_SavedTemplate(IFileService sender, Umbraco.Core.Events.SaveEventArgs<ITemplate> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                LogHelper.Info<TemplateHandler>("Save: Saving uSync file for item: {0}", () => item.Name);
                var action = _ioManager.ExportItem(item.Key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);

                if (action.Success)
                {
                    NameChecker.ManageOrphanFiles(SyncFolder, item.Key, action.FileName);

                    // becuase we delete by name, we should check the action log, and remove any entries with
                    // this alias.
                    uSyncBackOfficeContext.Instance.Tracker.RemoveActions(item.Alias, typeof(ITemplate));
                }

            }
        }
    }
}