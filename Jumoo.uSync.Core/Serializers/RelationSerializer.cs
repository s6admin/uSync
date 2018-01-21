using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jumoo.uSync.Core.Helpers;
using Jumoo.uSync.Core.Extensions;
using Umbraco.Core.Models;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Logging;

namespace Jumoo.uSync.Core.Serializers
{
	class RelationSerializer : SyncBaseSerializer<IRelation>, ISyncChangeDetail
	{
		private readonly IRelationService relationService;
		private const string NODE_NAME = "Relation"; 

		public override string SerializerType => uSyncConstants.Serailization.Relation;

		public RelationSerializer()
			:base("Relation")
		{
			relationService = ApplicationContext.Current.Services.RelationService;
		}

		public RelationSerializer(string itemType) : base(itemType) { }
		
		internal override SyncAttempt<IRelation> DeserializeCore(XElement node)
		{
			string relationName = string.Empty;
			var relationKey = node.Element("Key").ValueOrDefault(""); 
			if (relationKey == null || relationKey.Equals(Guid.Empty))
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, "Missing or invalid Relation key");

			var allRelations = relationService.GetAllRelations();
			var relation = default(IRelation);
			if(allRelations.Any(x => x.Key.Equals(relationKey)))
			{
				relation = allRelations.FirstOrDefault(x => x.Key.Equals(relationKey));
			}
			
			if(relation == default(IRelation))
			{
				// It doesn't seem possible to know what TYPE of relation to create in this case because the value would be coming directly from the relation object
				
				string relationTypeValue = node.Element("RelationType").ValueOrDefault(string.Empty);
				Guid relationTypeKey = relationTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(relationTypeValue);
				int parentId = -1;
				int childId = -1;

                if (!int.TryParse(node.Element("ParentId").ValueOrDefault(string.Empty), out parentId))
				{
					string msg = "Could not find parentId in xml data for Relation " + relationKey;
                    LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
				}

				if (!int.TryParse(node.Element("ChildId").ValueOrDefault(string.Empty), out childId))
				{
					string msg = "Could not find ChildId in xml data for Relation " + relationKey;
					LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
				}

				IRelationType relationType = relationService.GetRelationTypeById(relationTypeKey);

				if (relationType != null)
				{
					// S6 TODO If the relation object is used directly to create the XML then we may need to create our own that includes additional needed properties like the Guid keys for parentId and childId
					relation = new Relation(parentId, childId, relationType);
				}				
			}

			relationName = relation.Key.ToString(); 

			return SyncAttempt<IRelation>.Succeed(relationName, relation, ChangeType.Import);
		}

		internal override SyncAttempt<XElement> SerializeCore(IRelation item)
		{
			var node = new XElement(NODE_NAME);

			node.Add(new XElement("Id", item.Id)); //relation.Id
			node.Add(new XElement("ChildId", item.ChildId)); //relation.ChildId
			node.Add(new XElement("ParentId", item.ParentId)); //relation.ParentId
			node.Add(new XElement("Key", item.Key)); //relation.Key TODO These might always be empty, hence unreliable
			node.Add(new XElement("RelationTypeKey", item.RelationType.Key)); //relation.RelationType
			node.Add(new XElement("RelationTypeId", item.RelationTypeId)); //relation.RelationTypeId
			node.Add(new XElement("Comment", item.Comment)); // S6 TODO Nested Ids may need to be converted to Guids
			//node.Add(new XElement("", item)); //relation.UpdateDate
			//node.Add(new XElement("", item)); //relation.CreateDate

			return SyncAttempt<XElement>.SucceedIf(
			node != null, item.Key.ToString().ToSafeAlias(), node, typeof(IRelation), ChangeType.Export);
		}

		public override bool IsUpdate(XElement node)
		{
			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return true;

			//string relationValue = node.Element("Key").ValueOrDefault(string.Empty);			
			//Guid relationKey = relationValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(relationValue); // S6 TODO Handle improper strings better
			//if (relationKey.Equals(Guid.Empty))
			//	return true;

			int relationId = -1;
			if(!int.TryParse(node.Element("Id").ValueOrDefault(string.Empty), out relationId))
			{
				// S6 TODO LogHelper
				return true;
			}
						
			var item = relationService.GetById(relationId);
			if (item == null)
				return true;

			var attempt = Serialize(item);
			if (!attempt.Success)
				return true;

			var itemHash = attempt.Item.GetSyncHash();

			return (!nodeHash.Equals(itemHash));

			//return base.IsUpdate(node);
		}

		public IEnumerable<uSyncChange> GetChanges(XElement node)
		{
			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return null;

			int relationId = -1;
			if (!int.TryParse(node.Element("Id").ValueOrDefault(string.Empty), out relationId))
			{
				// S6 TODO LogHelper
				return null;
			}

			var item = relationService.GetById(relationId);
			if (item == null)
				return null;

			var attempt = Serialize(item);
			if (attempt.Success)
			{
				return uSyncChangeTracker.GetChanges(node, attempt.Item, "");
			}
			else
			{
				return uSyncChangeTracker.ChangeError("Relation" + relationId);
			}
		}
	}
}
