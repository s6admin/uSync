﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core.Models;

using Jumoo.uSync.Core.Interfaces;
using Jumoo.uSync.Core.Extensions;
using Umbraco.Core;
using Jumoo.uSync.Core.Helpers;
using Umbraco.Core.Logging;
using System.Globalization;

namespace Jumoo.uSync.Core.Serializers
{
    public class ContentSerializer : ContentBaseSerializer<IContent>
    {
        private bool _blueprintEnabled; 

        public override string SerializerType
        {
            get
            {
                return uSyncConstants.Serailization.Content;
            }
        }

        public ContentSerializer() : base(string.Empty)
        {
            _blueprintEnabled = Umbraco.Core.Configuration.UmbracoVersion.Current >= new Version(7, 7, 0);
        }

        internal override SyncAttempt<IContent> DeserializeCore(XElement node, int parentId, bool forceUpdate = false)
        {
            var nodeGuid = node.Attribute("guid");
            if (nodeGuid == null)
                return SyncAttempt<IContent>.Fail(node.NameFromNode(), ChangeType.Import, "No Guid in XML");

            Guid guid = new Guid(nodeGuid.Value);

            var name = node.Attribute("nodeName").Value;
            var type = node.Attribute("nodeTypeAlias").Value;
            var templateAlias = node.Attribute("templateAlias").Value;

            var blueprint = node.Attribute("isBlueprint").ValueOrDefault(false);

            var sortOrder = int.Parse(node.Attribute("sortOrder").Value);
            var published = bool.Parse(node.Attribute("published").Value);
            var parentGuid = node.Attribute("parentGUID").ValueOrDefault(Guid.Empty);

            if (parentGuid != Guid.Empty)
            {
                var parent = _contentService.GetById(parentGuid);
                if (parent != null)
                {
                    parentId = parent.Id;
                }
            }

            // because later set the guid, we are going for a match at this point
            var item = _contentService.GetById(guid);
            if (blueprint && _blueprintEnabled)
            {
                LogHelper.Debug<ContentSerializer>("Finding Blueprint: {0}", () => guid);
                item = GetBlueprint(guid);
            }

            if (item == null)
            {
                LogHelper.Debug<ContentSerializer>("Looking for node by name and type [{0}] {1} {2}", () => parentId, () => name, () => type);
                // legacy match by name and content type. 
                item = GetContentByNameAndAlias(_contentService.GetChildren(parentId), name, type);

                if (item == null)
                {
                    try
                    {
                        item = _contentService.CreateContent(name, parentId, type);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Warn<ContentSerializer>("Unable to create content: {0} - {1}", () => name, () => ex.ToString());
                    }
                }
            }
            else if (item.Trashed)
            {
                item.ChangeTrashedState(false);
            }


            // if we are still null, then we have failed to get the item. 
            if (item == null)
                return SyncAttempt<IContent>.Fail(node.NameFromNode(), ChangeType.ImportFail, "Cannot find or create content item");

            //
            // Change doctype if it changes, we could lose values here, but we 
            // are going to set them all later so should be fine. 
            //
            var contentType = ApplicationContext.Current.Services.ContentTypeService.GetContentType(type);
            if (contentType != null && item.ContentTypeId != contentType.Id)
            {
                item.ChangeContentType(contentType);
            }

            var template = ApplicationContext.Current.Services.FileService.GetTemplate(templateAlias);
            if (template != null)
                item.Template = template;

            item.Key = guid;

            item.SortOrder = sortOrder;
            item.Name = name;

            if (item.ParentId != parentId)
                item.ParentId = parentId;

            if (node.Attribute("publishAt") != null)
            {
                item.ReleaseDate = node.Attribute("publishAt").ValueOrDefault(DateTime.MinValue).ToUniversalTime();
            }

            if (node.Attribute("unpublishAt") != null)
            {
                item.ExpireDate = node.Attribute("unpublishAt").ValueOrDefault(DateTime.MaxValue).ToUniversalTime();
            }


            /* property values are set on the second pass, 
               so for speed lets no do them here... 
            */

            // items will go through a second pass, so we 'just' save them on the first pass
            // and publish them (if needed) on the second pass - lot less cache rebuilding this way.
            // PublishOrSave(item, false);
            if (blueprint && _blueprintEnabled)
            {
                SaveBlueprint(item);
            }
            else
            {
                _contentService.Save(item, 0, false);
            }

            return SyncAttempt<IContent>.Succeed(item.Name, item, ChangeType.Import);
        }


        /// <summary>
        ///  called from teh base when things change, we need to save or publish our content
        /// </summary>
        /// <param name="item"></param>
        /// 
        public override void PublishOrSave(IContent item, bool published, bool raiseEvents = false)
        {
            if (published)
            {
                var publishAttempt = _contentService.SaveAndPublishWithStatus(item, 0, raiseEvents);
                if (!publishAttempt.Success)
                {
                    // publish didn't work :(
                }
            }
            else
            {
                _contentService.Save(item, 0, false);
                if (item.Published)
                    _contentService.UnPublish(item);
            }
        }

        internal override SyncAttempt<XElement> SerializeCore(IContent item)
        {
            LogHelper.Debug<ContentSerializer>("Serialize Core: {0}", () => item.Name);

            var ContentTypeAlias = item.ContentType.Alias;
            var attempt = base.SerializeBase(item, ContentTypeAlias);

            if (!attempt.Success)
                return attempt;

            var node = attempt.Item;
			
            // content specifics..
            node.Add(new XAttribute("parentGUID", item.Level > 1 ? item.Parent().Key : Guid.Empty));
            node.Add(new XAttribute("nodeTypeAlias", item.ContentType.Alias));
            node.Add(new XAttribute("templateAlias", item.Template == null ? "" : item.Template.Alias));

            node.Add(new XAttribute("sortOrder", item.SortOrder));
            node.Add(new XAttribute("published", item.Published));

            if (_blueprintEnabled) { 
                LogHelper.Debug<ContentSerializer>("Is Blueprint?");
                node.Add(new XAttribute("isBlueprint", IsBlueprint(item)));
            }


            if (item.ExpireDate != null)
            {
                node.Add(new XAttribute("unpublishAt", item.ExpireDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'")));
            }

            if (item.ReleaseDate != null)
            {
                node.Add(new XAttribute("publishAt", item.ReleaseDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff'Z'")));
            }

            LogHelper.Debug<ContentSerializer>("Returning Node");
            return SyncAttempt<XElement>.Succeed(item.Name, node, typeof(IContent), ChangeType.Export);
        }

        public override bool IsUpdate(XElement node)
        {
            if (uSyncCoreContext.Instance.Configuration.Settings.ContentMatch.Equals("mismatch", StringComparison.OrdinalIgnoreCase))
                return IsDiffrent(node);
            else
                return IsNewer(node);
        }

        /// <summary>
        ///  the contentedition way, we only update if content in the node is newer
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private bool IsNewer(XElement node)
        {
            LogHelper.Debug<ContentSerializer>("Using IsNewer Checker");

            var key = node.Attribute("guid").ValueOrDefault(Guid.Empty);
            if (key == Guid.Empty)
                return true;

            var item = _contentService.GetById(key);
            if (item == null)
                return true;

            DateTime updateTime = node.Attribute("updated").ValueOrDefault(DateTime.Now).ToUniversalTime();

            // LogHelper.Debug<ContentSerializer>("IsUpdate: File {0}, DB {1} {2}", () => updateTime, () => item.UpdateDate.ToUniversalTime(), ()=>item.UpdateDate.Kind.ToString());
            if ((updateTime - item.UpdateDate.ToUniversalTime()).TotalSeconds > 1)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// are the node and content diffrent, this is the standard uSync way of doing comparisons. 
        /// </summary>
        private bool IsDiffrent(XElement node)
        {
            LogHelper.Debug<ContentSerializer>("Using IsDiffrent Checker");
            var key = node.Attribute("guid").ValueOrDefault(Guid.Empty);
            if (key == Guid.Empty)
                return true;

            var nodeHash = node.GetSyncHash();
            if (string.IsNullOrEmpty(nodeHash))
                return true;

            var item = _contentService.GetById(key);
            if (item == null)
                return true;

            var attempt = Serialize(item);
            if (!attempt.Success)
                return true;

            var itemHash = attempt.Item.GetSyncHash();

            return (!nodeHash.Equals(itemHash));
        }

        public override IContent GetItemOrDefault(XElement node, int parentId)
        {
            var key = node.Attribute("guid").ValueOrDefault(Guid.Empty);
            if (key == Guid.Empty)
                return null;

            var item = _contentService.GetById(key);
            if (item != null)
                return item;

            // legacy lookup 
            var name = node.Attribute("nodeName").Value;
            var type = node.Attribute("nodeTypeAlias").Value;
            var nodes = _contentService.GetChildren(parentId);

            return GetContentByNameAndAlias(nodes, name, type);
        }

        public override SyncAttempt<IContent> DesearlizeSecondPass(IContent item, XElement node)
        {
            base.DeserializeMappedIds(item, node);

            int sortOrder = node.Attribute("sortOrder").ValueOrDefault(-1);
            if (sortOrder >= 0)
                item.SortOrder = sortOrder;

            var published = node.Attribute("published").ValueOrDefault(false);
            var blueprint = node.Attribute("isBlueprint").ValueOrDefault(false);

            if (!blueprint)
            {
                PublishOrSave(item, published, true);
            }
            else
            {
                SaveBlueprint(item);
            }


            return SyncAttempt<IContent>.Succeed(item.Name, ChangeType.Import);
        }

        private bool IsBlueprint(IContent item)
        {
            if (_blueprintEnabled)
                return item.IsBlueprint;

            return false;
        }

        private void SaveBlueprint(IContent item)
        {
            if (_blueprintEnabled)
                _contentService.SaveBlueprint(item);
        }

        private IContent GetBlueprint(Guid guid)
        {
            if (_blueprintEnabled)
            {
                return _contentService.GetBlueprintById(guid);
            }
            return null;
        }

        private IContent GetContentByNameAndAlias(IEnumerable<IContent> nodes, string name, string contentTypeAlias)
        {
            if (nodes == null)
                return null;

            // this isn't the quickest thing - but hopefully once the first sync is done, we don't get here that often.
            return nodes.FirstOrDefault(x => x.Name == name && x.ContentType.Alias == contentTypeAlias);
        }
    }
}
