
namespace Jumoo.uSync.Core
{
    using Helpers;
    using Jumoo.uSync.Core.Interfaces;
    using Jumoo.uSync.Core.Serializers;
    using System.Collections.Generic;
    using Umbraco.Core;
    using Umbraco.Core.Logging;
    using Umbraco.Core.Models;
    using System.Linq;
    using System;
    using System.Diagnostics;
    using Umbraco.Core.Services;
    using Umbraco.Core.IO;

    public class uSyncCoreContext
    {
        private static uSyncCoreContext _instance;

        [Obsolete("Pass parameters.")]
        private uSyncCoreContext() {
            _logger = ApplicationContext.Current.ProfilingLogger.Logger;
        }

        public static uSyncCoreContext Instance
        {
            get { return _instance ?? (_instance = new uSyncCoreContext()); }
        }

        public Dictionary<string, ISyncSerializerBase> Serailizers;

        public ISyncContainerSerializerTwoPass<IContentType> ContentTypeSerializer { get; private set; }
        public ISyncContainerSerializerTwoPass<IMediaType> MediaTypeSerializer { get; private set; }

        public ISyncSerializerTwoPass<IMemberType> MemberTypeSerializer { get; private set; }

        public ISyncSerializer<ITemplate> TemplateSerializer { get; private set; }

        public ISyncSerializer<ILanguage> LanguageSerializer { get; private set; }
        public ISyncSerializer<IDictionaryItem> DictionarySerializer { get; private set; }

        public ISyncSerializer<IMacro> MacroSerializer { get; private set; }
        public ISyncContainerSerializerTwoPass<IDataTypeDefinition> DataTypeSerializer { get; private set; }

        public ISyncSerializerWithParent<IContent> ContentSerializer { get; private set; }
        public ISyncSerializerWithParent<IMedia> MediaSerializer { get; private set; }

        public ISyncFileHander2<IMedia> MediaFileMover { get; private set; }

        public uSyncCoreConfig Configuration { get; set; }

        [Obsolete("Intialize via the Ensure Context Method")]
        public void Init()
        {
            EnsureContext(
                ApplicationContext.Current.ProfilingLogger.Logger,
                FileSystemProviderManager.Current.GetUnderlyingFileSystemProvider("usync"),
                ApplicationContext.Current.Services);
        }

        internal void InitializeSerializers()
        {
            Serailizers = new Dictionary<string, ISyncSerializerBase>();

            var types = TypeFinder.FindClassesOfType<ISyncSerializerBase>();
            foreach (var type in types)
            {
                var instance = Activator.CreateInstance(type) as ISyncSerializerBase;
                _logger.Debug<uSyncCoreContext>("Adding Serializer: {0}:{1}", ()=> instance.SerializerType, () => type.Name);

                if (!this.Serailizers.ContainsKey(instance.SerializerType))
                {
                    Serailizers.Add(instance.SerializerType, instance);
                }
                else
                {
                    // we need to see if the new serializer of the same type has a higher priority
                    // then the one we already have...
                    var currentPriority = Serailizers[instance.SerializerType].Priority;
                    _logger.Debug<uSyncCoreContext>("Duplicate Serializer Found: {0} comparing priorites", () => instance.SerializerType);

                    if (instance.Priority > currentPriority)
                    {
                        _logger.Debug<uSyncCoreContext>("Loading new Serializer for {0} {1}", () => instance.SerializerType, ()=> type.Name);
                        Serailizers.Remove(instance.SerializerType);
                        Serailizers.Add(instance.SerializerType, instance);
                    }
                }
            }

            // shortcuts..
            if (Serailizers != null)
            {
                // we load the known shortcuts here. (to maintain the backwards compatability 
                if (Serailizers[uSyncConstants.Serailization.ContentType] is ContentTypeSerializer)
                    ContentTypeSerializer = (ContentTypeSerializer)Serailizers[uSyncConstants.Serailization.ContentType];

                if (Serailizers[uSyncConstants.Serailization.MediaType] is MediaTypeSerializer)
                    MediaTypeSerializer = (MediaTypeSerializer)Serailizers[uSyncConstants.Serailization.MediaType];

                if (Serailizers[uSyncConstants.Serailization.MemberType] is MemberTypeSerializer)
                    MemberTypeSerializer = (MemberTypeSerializer)Serailizers[uSyncConstants.Serailization.MemberType];

                if (Serailizers[uSyncConstants.Serailization.Template] is TemplateSerializer)
                    TemplateSerializer = (TemplateSerializer)Serailizers[uSyncConstants.Serailization.Template];

                if (Serailizers[uSyncConstants.Serailization.Language] is LanguageSerializer)
                    LanguageSerializer = (LanguageSerializer)Serailizers[uSyncConstants.Serailization.Language];

                if (Serailizers[uSyncConstants.Serailization.Dictionary] is DictionarySerializer)
                    DictionarySerializer = (DictionarySerializer)Serailizers[uSyncConstants.Serailization.Dictionary];

                if (Serailizers[uSyncConstants.Serailization.Macro] is MacroSerializer)
                    MacroSerializer = (MacroSerializer)Serailizers[uSyncConstants.Serailization.Macro];

                if (Serailizers[uSyncConstants.Serailization.DataType] is DataTypeSerializer)
                    DataTypeSerializer = (DataTypeSerializer)Serailizers[uSyncConstants.Serailization.DataType];

                if (Serailizers[uSyncConstants.Serailization.Content] is ContentSerializer)
                    ContentSerializer = (ContentSerializer)Serailizers[uSyncConstants.Serailization.Content];

                if (Serailizers[uSyncConstants.Serailization.Media] is MediaSerializer)
                    MediaSerializer = (MediaSerializer)Serailizers[uSyncConstants.Serailization.Media];
            }

        }

        public string Version
        {
            get
            {
                return typeof(Jumoo.uSync.Core.uSyncCoreContext)
                    .Assembly.GetName().Version.ToString();
            }
        }


        private ILogger _logger;
        public uSyncCoreContext(ILogger logger)
        {
            _logger = logger;
        }
        
        internal void Initialize(
            ILogger logger,
            IFileSystem fileSystem,
            ServiceContext services
            )
        {
            this.Configuration = new uSyncCoreConfig();
            InitializeSerializers();
            MediaFileMover = new uSyncMediaFileMover();

            InitializeIOManagers(_logger, fileSystem, services);
        }

        public static uSyncCoreContext EnsureContext(
            ILogger logger,
            IFileSystem fileSystem,
            ServiceContext services)
        {
            var ctx = new uSyncCoreContext(logger);
            ctx.Initialize(logger, fileSystem, services);
            _instance = ctx;
            return _instance;
        }

        public SortedList<int, ISyncIOManager> IOManagers { get; set; }

        public void InitializeIOManagers(
            ILogger logger,
            IFileSystem fileSystem,
            ServiceContext serviceContext)
        {
            IOManagers = new SortedList<int, ISyncIOManager>();

            var types = TypeFinder.FindClassesOfType<ISyncIOManager>();
            foreach (var t in types)
            {
                var instance = Activator.CreateInstance(t,
                    logger, fileSystem, this, serviceContext) as ISyncIOManager;
                if (instance != null)
                {
                    IOManagers.Add(instance.Priority, instance);
                }
            }
        }

        public ISyncIOManager GetIOManager(Type itemType)
        {
            var IOManager = IOManagers.Single(x => x.Value.ItemType == itemType);
            if (IOManager.Value == null)
                throw new KeyNotFoundException();

            return IOManager.Value;
        }

    }
}
