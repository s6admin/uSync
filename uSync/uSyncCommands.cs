using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Jumoo.uSync.Snapshots;
using Umbraco.Core.IO;

namespace uSync
{
    public class uSyncCommands
    {
        public void Import(string folderName)
        {

        }

        public void Export(string folderName)
        {

        }

        public void CreateSnapshot(string name)
        {

            Console.WriteLine("Creating Snapshot....");

            var root = IOHelper.MapPath("~/uSync/Snapshots");
            SnapshotManager snapshotManager = new SnapshotManager(root);

            var snapshot = snapshotManager.CreateSnapshot(name);
            if (snapshot != null)
            {
                Console.WriteLine("Created Snapshot: {0}", snapshot.Items.Count());
            }
            else
            {
                Console.WriteLine("Snapshot creation failed");
            }
        }

        public void ImportSnapshot(string name)
        {

        }

        public void AllSnapshots(string rootFolder)
        {

        }
    }
}
