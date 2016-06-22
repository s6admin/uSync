using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;

namespace uSync
{
    public class ConsoleApplicationBase : UmbracoApplicationBase
    {
        public string BaseDirectory { get; private set; }
        public string DataDirectory { get; private set; }

        protected override IBootManager GetBootManager()
        {
            var binDirectory = new DirectoryInfo(Environment.CurrentDirectory);
            BaseDirectory = ResolveBasePath(binDirectory);
            DataDirectory = Path.Combine(BaseDirectory, "app_data");

            var appDomainConfigPath = new DirectoryInfo(Path.Combine(BaseDirectory, "config"));

            if (binDirectory.FullName.Equals(BaseDirectory) == false && 
                appDomainConfigPath.Exists == false)
            {
                appDomainConfigPath.Create();
                var baseConfigPath = new DirectoryInfo(Path.Combine(BaseDirectory, "config"));
                var sourcefiles = baseConfigPath.GetFiles("*.config", SearchOption.TopDirectoryOnly);
                foreach(var sourceFile in sourcefiles)
                {
                    sourceFile.CopyTo(sourceFile.FullName.Replace(baseConfigPath.FullName, appDomainConfigPath.FullName), true);
                }
            }

            AppDomain.CurrentDomain.SetData("DataDirectory", DataDirectory);

            return new ConsoleBootManager(this, BaseDirectory);
        }

        public void Start(object sender, EventArgs e)
        {
            base.Application_Start(sender, e);
        }

        private string ResolveBasePath(DirectoryInfo currentFolder)
        {
            var folders = currentFolder.GetDirectories();
            if (folders.Any(x => x.Name.Equals("app_data", StringComparison.OrdinalIgnoreCase)) 
                && folders.Any(x => x.Name.Equals("config", StringComparison.OrdinalIgnoreCase)))
            {
                return currentFolder.FullName;
            }

            if (currentFolder.Parent == null)
                throw new Exception("Base directory containing app_data and Config not found");

            return ResolveBasePath(currentFolder.Parent);
        }
    }
}
