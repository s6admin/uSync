using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Jumoo.uSync.Core.Extensions;
using Jumoo.uSync.Core.Helpers;
using Umbraco.Core.Logging;

namespace Jumoo.uSync.Core.Serializers
{
	public class RelationTypeSerializer : SyncBaseSerializer<IRelationType>, ISyncChangeDetail
	{
		private readonly IRelationService relationService;
		private const string NODE_NAME = "RelationType";

		public override string SerializerType => uSyncConstants.Serailization.RelationType;

		public RelationTypeSerializer()
			: base(uSyncConstants.Serailization.RelationType)
		{
			relationService = ApplicationContext.Current.Services.RelationService;
		}

		public RelationTypeSerializer(string itemType) : base(itemType) { }
		
		/// <summary>
		/// Deserializes the provided XML XElement node to import the RelationType being processed.
		/// </summary>
		/// <param name="node">The node.</param>
		/// <returns></returns>
		internal override SyncAttempt<IRelationType> DeserializeCore(XElement node)
		{
			var relationTypeAlias = node.Element("Alias").ValueOrDefault(""); 
			if (string.IsNullOrEmpty(relationTypeAlias))
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, "Missing RelationType");
			
			Guid relationTypeKey = Guid.Empty;
			Guid childTypeKey = Guid.Empty;
			Guid parentTypeKey = Guid.Empty;

			Guid.TryParse(node.Element("Key").ValueOrDefault(string.Empty), out relationTypeKey);
			if (relationTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not deserialize RelationTypeKey for RelationType " + relationTypeAlias;
				LogHelper.Warn(typeof(RelationTypeSerializer), msg);
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			Guid.TryParse(node.Element("ChildObjectType").ValueOrDefault(string.Empty), out childTypeKey);
			if (childTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not deserialize ChildObjectType for RelationType " + relationTypeAlias;
				LogHelper.Warn(typeof(RelationTypeSerializer), msg);
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			Guid.TryParse(node.Element("ParentObjectType").ValueOrDefault(string.Empty), out parentTypeKey);
			if (parentTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not deserialize ParentObjectType for RelationType " + relationTypeAlias;
				LogHelper.Warn(typeof(RelationTypeSerializer), msg);
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			// All required properties are available

			var allRelationTypes = relationService.GetAllRelationTypes();

			var relationType = default(IRelationType);
			if (allRelationTypes.Any(x => x.Alias == relationTypeAlias))
			{
				relationType = allRelationTypes.FirstOrDefault(x => x.Alias == relationTypeAlias);
			}

			if (relationType == default(IRelationType))
			{				
				relationType = new RelationType(childTypeKey, parentTypeKey, relationTypeAlias);
				relationType.Key = relationTypeKey;
            }
						
			relationType.Name = node.Element("Name").ValueOrDefault(relationTypeAlias);
			relationType.IsBidirectional = node.Element("IsBidirectional").ValueOrDefault(true);

			bool saved;
			try
			{
				relationService.Save(relationType); // S6 TODO after we've successfully profiled this far
				saved = true;
			}
			catch(Exception ex)
			{
				LogHelper.Error(typeof(RelationTypeSerializer), ex.Message, ex);
				saved = false;
			}
						
			return SyncAttempt<IRelationType>.SucceedIf(saved, relationType.Alias, relationType, ChangeType.Import);
		}

		/// <summary>
		/// Serializes the provided IRelationType item for exporting it to the uSync data directory.
		/// </summary>
		/// <param name="item">The RelationType item to serialize</param>
		/// <returns></returns>
		internal override SyncAttempt<XElement> SerializeCore(IRelationType item)
		{
			var node = new XElement(NODE_NAME);

			node.Add(new XElement("Alias", item.Alias));
			node.Add(new XElement("ChildObjectType", item.ChildObjectType));			
			node.Add(new XElement("IsBidirectional", item.IsBidirectional));
			//node.Add(new XElement("Id", item.Key)); // NOTE Id is the primary key of a RelationType and cannot be modified/updated. The Key is used instead because without an <Id> node uSync automatically flags the item as a DELETE action
			node.Add(new XElement("Key", item.Key));
			node.Add(new XElement("Name", item.Name));
			node.Add(new XElement("ParentObjectType", item.ParentObjectType));
			
			return SyncAttempt<XElement>.SucceedIf(
				node != null, item.Alias, node, typeof(IRelationType), ChangeType.Export);

		}

		public override bool IsUpdate(XElement node)
		{
			// S6 NOTE The RelationType Id is never remapped so no <Id> tag will be present in either XElement

			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return true;
			
			Guid relationTypeKey = Guid.Empty;
			Guid.TryParse(node.Element("Key").ValueOrDefault(string.Empty), out relationTypeKey);
			if (relationTypeKey.Equals(Guid.Empty))
				return true;

			var item = relationService.GetRelationTypeById(relationTypeKey);
			if (item == null)
			{
				return true;
			} else
			{
				// TODO If an existing RelationType is found and we don't want Id to be overwritten we need to exclude the <Id> tag from the hash check otherwise it will always return true (mismatch)
			}

			var attempt = Serialize(item);
			if (!attempt.Success)
				return true;

			var itemHash = attempt.Item.GetSyncHash();
			bool hashesMatch = nodeHash.Equals(itemHash); 

            return (!hashesMatch);

		}


		public IEnumerable<uSyncChange> GetChanges(XElement node)
		{
			
			Guid relationTypeKey = Guid.Empty;
			Guid.TryParse(node.Element("Key").ValueOrDefault(string.Empty), out relationTypeKey);
			if (relationTypeKey.Equals(Guid.Empty))
				return null;

			var item = relationService.GetRelationTypeById(relationTypeKey);
			if (item == null)
				return null;

			var attempt = Serialize(item);
			if (attempt.Success)
			{
				return uSyncChangeTracker.GetChanges(node, attempt.Item, "");
			}
			else
			{
				var name = node.Element(NODE_NAME).ValueOrDefault(NODE_NAME);
				return uSyncChangeTracker.ChangeError(name);
			}
		}
	}
}
