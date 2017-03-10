using Jumoo.uSync.BackOffice.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumoo.uSync.Uploader
{
    public class uSyncUploader : IuSyncAddOn, IuSyncTab
    {
        public BackOfficeTab GetTabInfo()
        {
            return new BackOfficeTab
            {
                name = "Uploads",
                template = "/app_plugins/uSync.Uploads/uploaddashboard.html"
            };
        }

        public string GetVersionInfo()
        {
            return string.Format("uSync.Uploader: {0}",
                typeof(Jumoo.uSync.Uploader.uSyncUploader)
                .Assembly.GetName().Version.ToString());
        }
    }
}
