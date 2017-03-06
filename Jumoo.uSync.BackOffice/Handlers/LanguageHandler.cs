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

    public class LanguageHandler : uSyncBaseHandler<ILanguage>, ISyncHandler
    {
        public string Name { get { return "uSync: LanguageHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.Languages; } }
        public string SyncFolder { get { return Constants.Packaging.LanguagesNodeName; } }


        public void RegisterEvents()
        {
            LocalizationService.SavedLanguage += LocalizationService_SavedLanguage;
            LocalizationService.DeletedLanguage += LocalizationService_DeletedLanguage;
        }

        private void LocalizationService_DeletedLanguage(ILocalizationService sender, Umbraco.Core.Events.DeleteEventArgs<ILanguage> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<MacroHandler>("Delete: Deleting uSync File for item: {0}", () => item.CultureName);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, item.CultureName.ToSafeAlias());
                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.CultureName, typeof(ILanguage));
            }
        }

        private void LocalizationService_SavedLanguage(ILocalizationService sender, Umbraco.Core.Events.SaveEventArgs<ILanguage> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.SavedEntities)
            {
                LogHelper.Info<LanguageHandler>("Save: Saving uSync file for item: {0}", () => item.CultureName);
                _ioManager.ExportItem(item.Key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
                uSyncBackOfficeContext.Instance.Tracker.RemoveActions(item.CultureName, typeof(ILanguage));
            }
        }
    }
}
