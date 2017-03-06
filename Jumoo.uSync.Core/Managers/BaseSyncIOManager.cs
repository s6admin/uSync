using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Jumoo.uSync.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.IO;
using System.Xml.Linq;
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using Umbraco.Core.Models.EntityBase;
using Umbraco.Core;

namespace Jumoo.uSync.Core.IO
{
    public abstract class BaseSyncIOManager<TEntity>
    {
        protected bool requiresPostProcessing = false; 
        protected ILogger logger;
        protected readonly IFileSystem fileSystem;
        protected readonly uSyncCoreContext uSyncContext;
        protected IEntityService entityService;
        protected UmbracoObjectTypes objectType;
        protected UmbracoObjectTypes containerType;

        public BaseSyncIOManager(
            ILogger Logger, 
            IFileSystem FileSystem, 
            uSyncCoreContext USyncContext,
            ServiceContext serviceContext)
        {
            logger = Logger;
            fileSystem = FileSystem;
            uSyncContext = USyncContext;
            entityService = serviceContext.EntityService;
        }

        public abstract SyncAttempt<TEntity> ImportItem(string file, bool force);
        virtual public void ImportItemAgain(string file, TEntity item) { }
        public abstract uSyncAction ExportItem(Guid key, string folder);

        public abstract uSyncAction DeleteItem(Guid key, string name);

        public abstract uSyncAction ReportItem(string file);

        virtual public IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions) {
            return new List<uSyncAction>();
        }

        protected virtual bool RemoveContainer(int id) {
            return false;
        }

        public IEnumerable<uSyncAction> Import(string folder, bool force)
        {
            logger.Info<Events>("Import: {0}", () => Path.GetFileName(folder));
            List<uSyncAction> actions = new List<uSyncAction>();


            Dictionary<string, TEntity> updates = new Dictionary<string, TEntity>();

            actions.AddRange(ProcessActions());

            actions.AddRange(ImportFolder(folder, force, updates));

            foreach(var update in updates)
            {
                ImportItemAgain(update.Key, update.Value);
            }

            return actions;
        }

        private IEnumerable<uSyncAction> ImportFolder(string folder, bool force, Dictionary<string, TEntity> updates)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            string mappedFolder = folder;
            
            if (fileSystem.DirectoryExists(mappedFolder))
            {
                foreach(var file in fileSystem.GetFiles(mappedFolder, "*.config"))
                {
                    var attempt = ImportItem(file, force);
                    if (attempt.Success && attempt.Item != null)
                    {
                        updates.Add(file, attempt.Item);
                    }

                    actions.Add(uSyncActionHelper<TEntity>.SetAction(attempt, file, requiresPostProcessing));
                }

                foreach(var child in fileSystem.GetDirectories(mappedFolder))
                {
                    actions.AddRange(ImportFolder(child, force, updates));
                }
            }

            return actions;
        }

        public virtual IEnumerable<uSyncAction> Export(string folder)
        {
            return ExportFolder(-1, folder);
        }

        private IEnumerable<uSyncAction> ExportFolder(int parent, string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            if (containerType != UmbracoObjectTypes.Unknown)
            {
                var containers = entityService.GetChildren(parent, containerType);
                foreach (var container in containers)
                {
                    actions.AddRange(ExportFolder(container.Id, folder));
                }
            }

            var nodes = entityService.GetChildren(parent, objectType);
            foreach(var node in nodes)
            {
                actions.Add(ExportItem(node.Key, folder));
                actions.AddRange(ExportFolder(node.Id, folder));
            }

            return actions;
        }

        protected IEnumerable<uSyncAction> ProcessActions()
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            List<SyncAction> processAction = new List<SyncAction>(); //TODO: Load actions here...
            // some action loader needed...
            foreach(var action in processAction)
            {
                switch (action.Action)
                {
                    case SyncActionType.Delete:
                        actions.Add(DeleteItem(action.Key, action.Name));
                        break;
                }
            }

            return actions;
        }

        public IEnumerable<uSyncAction> Report(string folder)
        {
            List<uSyncAction> actions = new List<uSyncAction>();

            var mappedFolder = folder;
            if (fileSystem.DirectoryExists(folder))
            {
                foreach(var file in fileSystem.GetFiles(mappedFolder, "*.config"))
                {
                    actions.Add(ReportItem(file));
                }
                foreach(var child in fileSystem.GetDirectories(mappedFolder))
                {
                    actions.AddRange(Report(child));
                }
            }

            return actions;

        }

        protected IEnumerable<uSyncAction> CleanEmptyContainers(string folder, int parentId)
        {
            List<uSyncAction> actions = new List<uSyncAction>();
            var containers = entityService.GetChildren(parentId, containerType);
            foreach (var container in containers)
            {
                if (entityService.GetChildren(container.Id).Any())
                {
                    
                    var remove = RemoveContainer(container.Id);
                    actions.Add(uSyncAction.SetAction(remove, container.Name,
                        typeof(EntityContainer), ChangeType.Delete, "Empty"));
                }
            }
            return actions;
        }



        protected XElement GetNode(string file)
        {
            if (!fileSystem.FileExists(file))
                throw new System.IO.FileNotFoundException();

            using (var stream = fileSystem.OpenFile(file))
            {
                return XElement.Load(stream);
            }
            
        }

        protected string SavePath(string path, TEntity item)
        {
            return Path.Combine(path, GetItemPath(item));
        }

        protected virtual string GetItemPath(TEntity item)
        {
            return GetEntityPath((IUmbracoEntity)item);
        }

        protected string GetEntityPath(IUmbracoEntity entity)
        {
            var path = string.Empty;
            if (entity != null)
            {
                if (entity.ParentId > 0)
                {
                    var parent = entityService.Get(entity.ParentId);
                    if (parent != null)
                        path = GetEntityPath(parent);
                }
            }

            return Path.Combine(path, GetItemFileName(entity));
        }

        protected string GetItemFileName(IUmbracoEntity item)
        {
            return item.Name.ToSafeFileName();
        }

        protected void SaveNode(XElement item, string file)
        {
            using (MemoryStream s = new MemoryStream())
            {
                item.Save(s);
                fileSystem.AddFile(file, s, true);
            }
        }
    }
}
