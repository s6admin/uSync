

namespace Jumoo.uSync.BackOffice.Handlers
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using System.Collections.Generic;

    using Umbraco.Core;
    using Umbraco.Core.Models;
    using Umbraco.Core.Services;
    using Umbraco.Core.Logging;

    using Jumoo.uSync.Core;
    using Jumoo.uSync.BackOffice.Helpers;
    using Core.Extensions;
    using Umbraco.Core.Models.EntityBase;
    using System.Timers;

    public class DataTypeHandler : uSyncBaseHandler<IDataTypeDefinition>, ISyncHandler, ISyncPostImportHandler
    {
        public string Name { get { return "uSync: DataTypeHandler"; } }
        public int Priority { get { return uSyncConstants.Priority.DataTypes; } }
        public string SyncFolder { get { return Constants.Packaging.DataTypeNodeName; } }

        public DataTypeHandler()
        {
            RequiresPostProcessing = true;
        }

        private static Timer _saveTimer;
        private static Queue<Guid> _saveQueue;
        private static object _saveLock;

        public void RegisterEvents()
        {
            DataTypeService.Saved += DataTypeService_Saved;
            DataTypeService.Deleted += DataTypeService_Deleted;
            DataTypeService.Moved += DataTypeService_Moved;

            // delay trigger - used (upto and including umb 7.4.2
            // saved event on a datatype is called before prevalues
            // are saved - so we just wait a little while before 
            // we save our datatype... 
            //  not ideal but them's the breaks.
            //
            //
            //
            _saveTimer = new Timer(4064); // 1/2 a perfect wait.
            _saveTimer.Elapsed += _saveTimer_Elapsed;

            _saveQueue = new Queue<Guid>();
            _saveLock = new object();
        }

        private void _saveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock( _saveLock)
            {
                while (_saveQueue.Count > 0 )
                {
                    Guid key = _saveQueue.Dequeue();
                    SaveToDisk(key);
                }
            }
        }

        private void DataTypeService_Deleted(IDataTypeService sender, Umbraco.Core.Events.DeleteEventArgs<IDataTypeDefinition> e)
        {
            if (uSyncEvents.Paused)
                return;

            foreach (var item in e.DeletedEntities)
            {
                LogHelper.Info<DataTypeHandler>("Delete: Deleting uSync File for item: {0}", () => item.Name);
                uSyncIOHelper.ArchiveRelativeFile(SyncFolder, GetItemPath(item), item.Name.ToSafeAlias());

                uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Key, item.Name, typeof(IDataTypeDefinition));
            }
        }


        private void DataTypeService_Saved(IDataTypeService sender, Umbraco.Core.Events.SaveEventArgs<IDataTypeDefinition> e)
        {
            if (uSyncEvents.Paused)
                return;

            lock (_saveLock)
            {
                _saveTimer.Stop();
                _saveTimer.Start();
                foreach (var item in e.SavedEntities)
                {
                    _saveQueue.Enqueue(item.Key);
                }
            }
        }

        private void DataTypeService_Moved(IDataTypeService sender, Umbraco.Core.Events.MoveEventArgs<IDataTypeDefinition> e)
        {
            if (uSyncEvents.Paused)
                return;

            lock(_saveLock)
            {
                _saveTimer.Stop();
                _saveTimer.Start();
                foreach(var item in e.MoveInfoCollection)
                {
                    _saveQueue.Enqueue(item.Entity.Key);
                }
            }
        }

        private void SaveToDisk(Guid key)
        {
            LogHelper.Info<DataTypeHandler>("Save: Saving uSync file for item: {0}", () => key);
            var action = _ioManager.ExportItem(key, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);
            if (action.Success)
            {
                NameChecker.ManageOrphanFiles(SyncFolder, key, action.FileName);
            }
        }

        public IEnumerable<uSyncAction> ProcessPostImport(string filepath, IEnumerable<uSyncAction> actions)
        {
            return _ioManager.PostImport(filepath, actions);
        }
    }
}
