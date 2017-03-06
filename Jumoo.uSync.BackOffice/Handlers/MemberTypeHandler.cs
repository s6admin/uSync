using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jumoo.uSync.Core;

using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Logging;

using System.IO;
using Jumoo.uSync.BackOffice.Helpers;
using System.Xml.Linq;
using Jumoo.uSync.Core.Extensions;

namespace Jumoo.uSync.BackOffice.Handlers
{
    public class MemberTypeHandler : uSyncBaseHandler<IMemberType>, ISyncHandler
    {
        public string Name { get { return "uSync: MemberTypeHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.MemberTypes; } }
        public string SyncFolder { get { return "MemberType"; } }

        private IMemberTypeService _memberTypeService; 

        public MemberTypeHandler()
        {
            _memberTypeService = ApplicationContext.Current.Services.MemberTypeService;
        }
        public void RegisterEvents()
        {
            MemberTypeService.Saved += MemberTypeService_Saved;
            MemberTypeService.Deleted += MemberTypeService_Deleted;
        }

        private void MemberTypeService_Deleted(IMemberTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IMemberType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach(var item in e.DeletedEntities)
            {
                LogHelper.Info<MediaTypeHandler>("Delete: Remove usync files for {0}", () => item.Name);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, GetItemPath(item), "def");
                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Key, item.Alias, typeof(IMemberType));
            }
        }

        private void MemberTypeService_Saved(IMemberTypeService sender, Umbraco.Core.Events.SaveEventArgs<IMemberType> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach(var item in e.SavedEntities)
            {
                LogHelper.Info<MemberTypeHandler>("Save: Saving uSync files for : {0}", () => item.Name);

                var action = _ioManager.ExportItem(item.Key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
                if (action.Success)
                {
                    NameChecker.ManageOrphanFiles("MemberType", item.Key, action.FileName);
                }
            }
        }
    }
}
