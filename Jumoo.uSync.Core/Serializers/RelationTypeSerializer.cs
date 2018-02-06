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
using Umbraco.Core.Events;

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
			var relationTypeAlias = node.Element("Alias").ValueOrDefault(string.Empty); 
			if (string.IsNullOrEmpty(relationTypeAlias))
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, "Missing RelationType Alias");

			Guid relationTypeKey = node.Element("Key").ValueOrDefault(Guid.Empty);
			Guid childTypeKey = node.Element("ChildObjectType").ValueOrDefault(Guid.Empty);
			Guid parentTypeKey = node.Element("ParentObjectType").ValueOrDefault(Guid.Empty);
			
			if (relationTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not deserialize RelationTypeKey for RelationType " + relationTypeAlias;
				LogHelper.Warn(typeof(RelationTypeSerializer), msg);
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}
						
			if (childTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not deserialize ChildObjectType for RelationType " + relationTypeAlias;
				LogHelper.Warn(typeof(RelationTypeSerializer), msg);
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}
						
			if (parentTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not deserialize ParentObjectType for RelationType " + relationTypeAlias;
				LogHelper.Warn(typeof(RelationTypeSerializer), msg);
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			// All required properties are available, proceed with deserialization attempt below...
			var allRelationTypes = relationService.GetAllRelationTypes(); // Get relationTypes ONCE and maintain locally so we don't hit the db so often

			IRelationType relationType = allRelationTypes.FirstOrDefault(x => x.Alias == relationTypeAlias); // S6 TODO Prioritize Key over Alias			
			if (relationType == default(IRelationType))
			{				
				relationType = new RelationType(childTypeKey, parentTypeKey, relationTypeAlias);			
            }

			relationType.Key = relationTypeKey;
			relationType.Name = node.Element("Name").ValueOrDefault(relationTypeAlias);
			relationType.IsBidirectional = node.Element("IsBidirectional").ValueOrDefault(true);

			bool saved;
			try
			{
				relationService.Save(relationType); 
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
			//node.Add(new XElement("Id", item.Id)); 
			node.Add(new XElement("Key", item.Key));
			node.Add(new XElement("Name", item.Name));			
			node.Add(new XElement("ParentObjectType", item.ParentObjectType));
			
			return SyncAttempt<XElement>.SucceedIf(
				node != null, item.Alias, node, typeof(IRelationType), ChangeType.Export);

		}

		public override bool IsUpdate(XElement node)
		{
			// S6 NOTE The RelationType Id value should not be changed/updated so the <Id> tag is excluded from both XElement objects so the hash can be compared properly

			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return true;
			
			Guid relationTypeKey = node.Element("Key").ValueOrDefault(Guid.Empty);			
			if (relationTypeKey.Equals(Guid.Empty))
				return true;

			var item = relationService.GetRelationTypeById(relationTypeKey);
			if (item == null)
			{
				return true;
			} else
			{
				
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
			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return null;

			Guid relationTypeKey = node.Element("Key").ValueOrDefault(Guid.Empty);			
			if (relationTypeKey.Equals(Guid.Empty))
			{
				return null; //return uSyncChangeTracker.ChangeError(node.NameFromNode());
			}				

			var item = relationService.GetRelationTypeById(relationTypeKey);
			if (item == null)
			{
				// If no matching Key was found also check for entity by Alias before deciding if Item is new
				string aliasValue = node.Element("Alias").ValueOrDefault(string.Empty);
				if(aliasValue.IsNullOrWhiteSpace())
				{
					return null; //return uSyncChangeTracker.ChangeError(node.NameFromNode());					
				} else
				{
					item = relationService.GetRelationTypeByAlias(aliasValue);
					if (item == null)
					{
						return uSyncChangeTracker.NewItem(node.NameFromNode());
					} 
				}							
			}
				
			var attempt = Serialize(item);
			if (attempt.Success)
			{
				return uSyncChangeTracker.GetChanges(node, attempt.Item, "");
			}
			else
			{				
				return uSyncChangeTracker.ChangeError(node.NameFromNode());
			}			
		}
	}
}
