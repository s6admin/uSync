using Jumoo.uSync.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.IO;
using Umbraco.Core.Services;

namespace Jumoo.uSync.IO
{
    /// <summary>
    ///  a usync io layer - basically seperating out the reading / writing from the 
    ///  syncing - so we can do things at a migration level. 
    /// </summary>
    public class uSyncIO
    {
        public static uSyncIO Current;

        public SortedList<int, ISyncIOManager> Managers { get; set; }

        public ISyncIOManager GetByType(Type itemType)
        {
            var manager = Managers.Single(x => x.Value.ItemType == itemType);
            if (manager.Value == null)
                throw new KeyNotFoundException();

            return manager.Value;
        }

        public uSyncIO(
            ILogger logger,
            IFileSystem fileSystem,
            uSyncCoreContext uSyncContext,
            ServiceContext serviceContext)

        {
            LoadManagers(logger, fileSystem, uSyncContext, serviceContext);
        }

        private void LoadManagers(
            ILogger logger,
            IFileSystem fileSystem,
            uSyncCoreContext uSyncContext,
            ServiceContext serviceContext)
        {
            Managers = new SortedList<int, ISyncIOManager>();

            var types = TypeFinder.FindClassesOfType<ISyncIOManager>();
            foreach(var t in types)
            {
                var instance = Activator.CreateInstance(t,
                    logger, fileSystem, uSyncContext, serviceContext) as ISyncIOManager;
                if (instance != null)
                {
                    Managers.Add(instance.Priority, instance);
                }
            }
        }

        public uSyncIO EnsureContext(
            ILogger logger, 
            IFileSystem fileSystem,
            uSyncCoreContext uSyncContext,
            ServiceContext serviceContext)
        {
            var ctx = new uSyncIO(logger, fileSystem, uSyncContext, serviceContext);
            Current = ctx;
            return Current;
        }
    }
}
