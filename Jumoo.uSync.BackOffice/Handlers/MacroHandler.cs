

namespace Jumoo.uSync.BackOffice.Handlers
{
    using System;
    using System.IO;
    using System.Xml.Linq;

    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Core.Logging;

    using Jumoo.uSync.Core;
    using Jumoo.uSync.BackOffice.Helpers;
    using System.Collections.Generic;
    using Core.Extensions;

    public class MacroHandler : uSyncBaseHandler<IMacro>, ISyncHandler
    {
        public string Name { get { return "uSync: MacroHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.Macros; } }
        public string SyncFolder { get { return Constants.Packaging.MacroNodeName; } }

        public void RegisterEvents()
        {
            MacroService.Saved += MacroService_Saved;
            MacroService.Deleted += MacroService_Deleted;
        }

        private void MacroService_Deleted(IMacroService sender, Umbraco.Core.Events.DeleteEventArgs<IMacro> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<MacroHandler>("Delete: Deleting uSync File for item: {0}", () => item.Name);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, item.Alias.ToSafeAlias());

                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Alias, typeof(IMacro));
            }
        }

        private void MacroService_Saved(IMacroService sender, Umbraco.Core.Events.SaveEventArgs<IMacro> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                LogHelper.Info<MacroHandler>("Save: Saving uSync file for item: {0}", () => item.Name);
                var action = _ioManager.ExportItem(item.Key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
                if (action.Success)
                {
                    // Name checker currently only works on guidkeys. 
                    // The key changes on macros, so we fake it with the id. 
                    // this isn't imported or exported, but we use it check for orphans, when renames happen
                    // it will work as long as the before and after both came from the same instance. 
                    // 
                    // when keys are less volitile in macros we will revert. 
                    NameChecker.ManageOrphanFiles(SyncFolder, item.Id, action.FileName);
                    // uSyncBackOfficeContext.Instance.Tracker.RemoveActions(item.Alias, typeof(IMacro));
                }
            }
        }
    }
}
