using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using umbraco.editorControls;
using umbraco.interfaces;
using Umbraco.Core;

namespace uSync
{
    public class ConsoleBootManager : CoreBootManager
    {
        public ConsoleBootManager(UmbracoApplicationBase umbracoApplication, string baseDirectory) : base(umbracoApplication)
        {
            var interfaceAssemblyName = typeof(IDataType).Assembly.FullName;
            var editorControlerAssemblyName = typeof(uploadField).Assembly.FullName;

            base.InitializeApplicationRootPath(baseDirectory);
        }

        protected override void InitializeApplicationEventsResolver()
        {
            base.InitializeApplicationEventsResolver();
        }

        protected override void InitializeResolvers()
        {
            base.InitializeResolvers();
        }
    }
}
