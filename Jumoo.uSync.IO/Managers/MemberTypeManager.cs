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
    public class MemberTypeManager : BaseSyncManager<IMemberType>, ISyncManager
    {
        public Guid Key => Guid.Parse("E08B03C9-C010-4462-BE7D-7E9DF6DA452C");
        public string Name => "MemberTypeManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }

        private readonly IMemberTypeService memberTypeService;

        public MemberTypeManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.MemberType;
            containerType = UmbracoObjectTypes.Unknown;

            memberTypeService = serviceContext.MemberTypeService;
        }

        public override SyncAttempt<IMemberType> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.MemberTypeSerializer.DeSerialize(node, force);

            return SyncAttempt<IMemberType>.Fail(file, ChangeType.ImportFail);
        }


        public override void ImportItemAgain(string file, IMemberType item)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            uSyncContext.MemberTypeSerializer.DesearlizeSecondPass(item, node);
        }

        public IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions)
        {
            if (actions.Any(x => x.ItemType == typeof(IMemberType)))
            {
                return CleanEmptyContainers(folder, -1);
            }
            return null;
        }

        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var item = memberTypeService.Get(key);

                if (item != null)
                {
                    memberTypeService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(IMemberType), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(IMemberType), ChangeType.Delete, "Not found");
                   
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var item = memberTypeService.Get(key);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(IMemberType), "Item not set");

            try
            {
                var attempt = uSyncContext.MemberTypeSerializer.Serialize(item);
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
                logger.Warn<ContentTypeManager>("Error saving member type {0}", () => ex.ToString());
                return uSyncAction.Fail(item.Name, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.MemberTypeSerializer.IsUpdate(node);

            var action = uSyncActionHelper<IMemberType>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.MemberTypeSerializer).GetChanges(node);

            return action;
        }

        protected override bool RemoveContainer(int id)
        {
            // var attempt = memberTypeService.Delete()
            return true;
        }

    }
}
