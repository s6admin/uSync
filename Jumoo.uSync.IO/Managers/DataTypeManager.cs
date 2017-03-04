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
    public class DataTypeManager : BaseSyncManager<IDataTypeDefinition>, ISyncManager
    {
        public Guid Key => Guid.Parse("31D05C33-5FE5-42E6-866A-E11CB4EE19C7");
        public string Name => "DataTypeManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }
        public Type ItemType => typeof(IDataTypeDefinition);


        private readonly IDataTypeService dataTypeService;

        public DataTypeManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.DataType;
            containerType = UmbracoObjectTypes.DataTypeContainer;
            dataTypeService = serviceContext.DataTypeService;

            requiresPostProcessing = true;
        }

        public override SyncAttempt<IDataTypeDefinition> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.DataTypeSerializer.DeSerialize(node, force);

            return SyncAttempt<IDataTypeDefinition>.Fail(file, ChangeType.ImportFail);
        }


        public override void ImportItemAgain(string file, IDataTypeDefinition item)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            uSyncContext.DataTypeSerializer.DesearlizeSecondPass(item, node);
        }

        public override IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions)
        {
            if (actions.Any(x => x.ItemType == typeof(IDataTypeDefinition)))
            {
                return CleanEmptyContainers(folder, -1);
            }
            return null;
        }

        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                var item = dataTypeService.GetDataTypeDefinitionById(key);

                if (item != null)
                {
                    dataTypeService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(IDataTypeDefinition), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(IDataTypeDefinition), ChangeType.Delete, "Not found");
                   
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var item = dataTypeService.GetDataTypeDefinitionById(key);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(IDataTypeDefinition), "Item not set");

            try
            {
                var attempt = uSyncContext.DataTypeSerializer.Serialize(item);
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
                logger.Warn<DataTypeManager>("Error saving data type {0}", () => ex.ToString());
                return uSyncAction.Fail(item.Name, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.DataTypeSerializer.IsUpdate(node);

            var action = uSyncActionHelper<IDataTypeDefinition>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.DataTypeSerializer).GetChanges(node);

            return action;
        }

        protected override bool RemoveContainer(int id)
        {
            var attempt = dataTypeService.DeleteContainer(id);
            return attempt.Success;
        }

    }
}
