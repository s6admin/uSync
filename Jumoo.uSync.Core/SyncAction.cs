using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.Core
{
    public class SyncAction
    {
        public string TypeName { get; set; }
        public string Name { get; set; }
        public Guid Key { get; set; }
        public SyncActionType Action { get; set; }
    }

    public enum SyncActionType
    {
        Delete,
        Rename,
        Obsolete,
    }

}

