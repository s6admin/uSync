namespace Jumoo.uSync.BackOffice.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;

    using Jumoo.uSync.Core;

    using Jumoo.uSync.BackOffice.Helpers;

    using Umbraco.Core.Logging;
    using Umbraco.Core.Models.EntityBase;
    using System;
    using Umbraco.Core;
    using Umbraco.Core.Models;
    using System.Xml.Linq;
    using Core.Extensions;
    using Core.Interfaces;

    abstract public class uSyncBaseHandler<T>
    {
        bool _useShortName;
        protected ISyncIOManager _ioManager;

        public uSyncBaseHandler()
        {
            _ioManager = uSyncCoreContext.Instance.GetIOManager(typeof(T));

            // short Id Setting, means we save with id.config not {{name}}.config
            _useShortName = uSyncBackOfficeContext.Instance.Configuration.Settings.UseShortIdNames;
        }

        // do things that get imported by this handler then require some form of 
        // post import processing, if this is set to true then the items will
        // also be post processed. 
        internal bool RequiresPostProcessing = false;

        // abstract public SyncAttempt<T> Import(string filePath, bool force = false);

        public IEnumerable<uSyncAction> ImportAll(string folder, bool force)
        {
            return _ioManager.Import(folder, force);
        }

        public uSyncAction DeleteItem(Guid key, string keyString)
        {
            return _ioManager.DeleteItem(key, keyString);
        }

        public IEnumerable<uSyncAction> ExportAll(string folder)
        {
            return _ioManager.Export(folder);
        }


        virtual public string GetItemPath(T item)
        {
            return GetEntityPath((IUmbracoEntity)item);
        }

        internal string GetEntityPath(IUmbracoEntity item)
        {
            string path = string.Empty;
            if (item != null)
            {
                if (item.ParentId > 0)
                {
                    var parent = ApplicationContext.Current.Services.EntityService.Get(item.ParentId);
                    if (parent != null)
                    {
                        path = GetEntityPath(parent);
                    }
                }

                path = Path.Combine(path, GetItemFileName(item));
            }
            return path;

        }

        /// <summary>
        ///  second pass placeholder, some things require a second pass
        ///  (doctypes for structures to be in place)
        /// 
        ///  they just override this function to do their thing.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="item"></param>
        virtual public void ImportSecondPass(string file, T item)
        {

        }

        /// <summary>
        ///  reutns a list of actions saying what will happen 
        /// on a import (force = false)
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public IEnumerable<uSyncAction> Report(string folder)
        {
            return _ioManager.Report(folder);
        }

        public uSyncAction ReportItem(string file)
        {
            return _ioManager.ReportItem(file);
        }

        protected string GetItemFileName(IUmbracoEntity item)
        {
            if (item != null)
            {
                if (_useShortName)
                    return uSyncIOHelper.GetShortGuidPath(item.Key);

                return item.Name.ToSafeFileName();
            }

            // we should never really get here, but if for
            // some reason we do - just return a guid.
            return uSyncIOHelper.GetShortGuidPath(Guid.NewGuid());

        }

        protected string GetItemFileName(IEntity item, string name)
        {
            if (_useShortName)
                return uSyncIOHelper.GetShortGuidPath(item.Key);

            return name.ToSafeFileName();
        }
    }
}
