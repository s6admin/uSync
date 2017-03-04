using Jumoo.uSync.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.IO
{
    /// <summary>
    ///  an interface to handler the io of usyncness, 
    /// </summary>
    public interface ISyncManager
    {

        Guid Key { get;  }
        string Name { get; }
        string SyncFolder { get; set; }

        int Priority { get; set; }
        Type ItemType { get; }


        IEnumerable<uSyncAction> Import(string folder, bool force);
        IEnumerable<uSyncAction> PostImport(string folder, IEnumerable<uSyncAction> actions);
        IEnumerable<uSyncAction> Export(string folder);

        IEnumerable<uSyncAction> Report(string folder);
    }
}
