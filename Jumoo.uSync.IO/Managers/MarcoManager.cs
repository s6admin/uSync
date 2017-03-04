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
    public class MacroManager : BaseSyncManager<IMacro>, ISyncManager
    {
        public Guid Key => Guid.Parse("44CFA711-0D6D-470A-BF23-2E7BFEFBE8C7");
        public string Name => "MacroManager";
        public int Priority { get; set; }
        public string SyncFolder { get; set; }

        private readonly IMacroService macroService;
        public Type ItemType => typeof(IMacro);

        public MacroManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext, 
            ServiceContext serviceContext) 
            : base(Logger, FileSystem, USyncContext, serviceContext)
        {
            objectType = UmbracoObjectTypes.Unknown;
            containerType = UmbracoObjectTypes.Unknown;
            macroService = serviceContext.MacroService;

            requiresPostProcessing = true;
        }

        public override SyncAttempt<IMacro> ImportItem(string file, bool force)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            var node = GetNode(file);
            if (node != null)
                return uSyncContext.MacroSerializer.DeSerialize(node, force);

            return SyncAttempt<IMacro>.Fail(file, ChangeType.ImportFail);
        }

        public override IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions)
        {
            if (actions.Any(x => x.ItemType == typeof(IMacro)))
            {
                return CleanEmptyContainers(folder, -1);
            }
            return null;
        }

        public override uSyncAction DeleteItem(Guid key, string name)
        {
            if (key != Guid.Empty)
            {
                // double lookup, for macro's because they are not (yet) searchable by key.
                var entity = entityService.GetByKey(key);
                var item = macroService.GetById(entity.Id);

                if (item != null)
                {
                    macroService.Delete(item);
                    return uSyncAction.SetAction(true, name, typeof(IMacro), ChangeType.Delete);
                }
            }
            return uSyncAction.Fail(name, typeof(IMacro), ChangeType.Delete, "Not found");
                   
        }

        public override IEnumerable<uSyncAction> Export(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            foreach(var item in macroService.GetAll())
            {
                if (item != null)
                    actions.Add(ExportMacro(item, folder));
            }

            return actions;
        }

        public override uSyncAction ExportItem(Guid key, string folder)
        {
            var entity = entityService.GetByKey(key);
            var item = macroService.GetById(entity.Id);
            if (item == null)
                return uSyncAction.Fail(Path.GetFileName(folder), typeof(IMacro), "Item not set");

            return ExportMacro(item, folder);
        }

        private uSyncAction ExportMacro(IMacro item, string folder)
        { 
            try
            {
                var attempt = uSyncContext.MacroSerializer.Serialize(item);
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
                logger.Warn<DataTypeManager>("Error saving macro {0}", () => ex.ToString());
                return uSyncAction.Fail(item.Name, item.GetType(), ChangeType.Export, ex);
            }
        }

        public override uSyncAction ReportItem(string file)
        {
            var node = GetNode(file);
            var update = uSyncContext.MacroSerializer.IsUpdate(node);

            var action = uSyncActionHelper<IMacro>.ReportAction(update, node.NameFromNode());
            if (action.Change > ChangeType.NoChange)
                action.Details = ((ISyncChangeDetail)uSyncContext.MacroSerializer).GetChanges(node);

            return action;
        }
    }
}
