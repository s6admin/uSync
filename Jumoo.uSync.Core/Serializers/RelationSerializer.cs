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
		private readonly IContentService contentService;
		private readonly IContentTypeService contentTypeService;
		private readonly IDataTypeService dataTypeService;

		// TODO Needed for PropertyType?
		private readonly IEntityService entityService;
		
		private const string NODE_NAME = "Relation"; 

		public override string SerializerType => uSyncConstants.Serailization.Relation;

		public RelationSerializer()
			:base("Relation")
		{
			relationService = ApplicationContext.Current.Services.RelationService;
			contentService = ApplicationContext.Current.Services.ContentService;
			contentTypeService = ApplicationContext.Current.Services.ContentTypeService;
			dataTypeService = ApplicationContext.Current.Services.DataTypeService;			
		}

		public RelationSerializer(string itemType) : base(itemType) { }
		
		internal override SyncAttempt<IRelation> DeserializeCore(XElement node)
		{
			// Deserialization is stricter than serialization. Only allow import creation and/or update if all needed keys are valid
			// Ensure we have all necessary keys to create and/or update the Relation
			string relationName = string.Empty;
			Guid relationKey = Guid.Empty;
			Guid childKey = Guid.Empty;
			Guid parentKey = Guid.Empty;
			Guid relationTypeKey = Guid.Empty;
			Guid propertyTypeKey = Guid.Empty;
			Guid dataTypeDefinitionKey = Guid.Empty;

			if (!Guid.TryParse(node.Element("Key").ValueOrDefault(""), out relationKey))
			{
				string msg = "Missing or invalid Relation Key for " + node.NameFromNode();
                LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}		
			if (!Guid.TryParse(node.Element("ChildKey").ValueOrDefault(string.Empty), out childKey)) {
				string msg = "Could not find ChildKey to deserialize for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}
			if(!Guid.TryParse(node.Element("ParentKey").ValueOrDefault(string.Empty), out parentKey))
			{
				string msg = "Could not find ParentKey to deserialize for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}			
			if (!Guid.TryParse(node.Element("RelationTypeKey").ValueOrDefault(string.Empty), out relationTypeKey))
			{
				string msg = "Could not find RelationTypeKey to deserialize for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}
			if (!Guid.TryParse(node.Element("PropertyTypeKey").ValueOrDefault(string.Empty), out propertyTypeKey))
			{
				string msg = "Could not find PropertyTypeKey to deserialize for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}
			if (!Guid.TryParse(node.Element("DataTypeDefinitionKey").ValueOrDefault(string.Empty), out dataTypeDefinitionKey))
			{
				string msg = "Could not find DataTypeDefinitionKey to deserialize for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			// If all keys are valid, begin retrieving content in target environment

			IContent child = contentService.GetById(childKey);
			if(child == null)
			{
				string msg = "Could not find child content " + childKey.ToString() + " for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			IContent parent = contentService.GetById(parentKey);
			if(parent == null)
			{
				string msg = "Could not find parent content " + parentKey.ToString() + " for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			IRelationType relationType = relationService.GetRelationTypeById(relationTypeKey);			
			if(relationType == null)
			{
				string msg = "Could not find relation type " + relationTypeKey.ToString() + " for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			}

			PropertyType propertyType = child.PropertyTypes.FirstOrDefault(x => x.Key == propertyTypeKey);
			IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionKey);
			
			XElement relationMapping = XElement.Parse(node.Element("Comment").ValueOrDefault(string.Empty));
			
			if (relationMapping == null)
			{
				// S6 TODO Should this completely fail or can we attempt to reassemble the RelationMapping tag since we know its structure?
				string msg = "Could not deserialize relation mapping xml node for Relation " + relationKey;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(node.NameFromNode(), ChangeType.Import, msg);
			} else
			{
				// Ensure values in Comment node are updated and correct before we
				relationMapping.Attribute("PropertyTypeKey").SetValue(propertyType.Key);
				relationMapping.Attribute("PropertyTypeId").SetValue(propertyType.Id); // Target environment Id, NOT the Id from xml
				relationMapping.Attribute("DataTypeDefinitionKey").SetValue(dataTypeDefinition.Key);
				relationMapping.Attribute("DataTypeDefinitionId").SetValue(dataTypeDefinition.Id);                
			}

			// Look for existing relation record
			var allRelations = relationService.GetAllRelationsByRelationType(relationType.Id);
			var relation = default(IRelation);
			if(allRelations.Any(x => x.Key.Equals(relationKey)))
			{
				relation = allRelations.FirstOrDefault(x => x.Key.Equals(relationKey));
			}
			
			if (relation == default(IRelation))
			{
				// No matching relation record found, create a new one				
				relation = new Relation(parent.Id, child.Id, relationType);								
			}
			
			// Update relation record values
			relation.ChildId = child.Id;
			relation.ParentId = parent.Id;
			relation.Comment = relationMapping.ToString();

			bool saved;

			try
			{
				//relationService.Save(relation); // TODO Save record once we've confirmed properties are correct
				saved = true;
			} catch(Exception ex)
			{
				LogHelper.Error(typeof(RelationSerializer), ex.Message, ex);
				saved = false;
			}
						
			return SyncAttempt<IRelation>.SucceedIf(saved, relation.Key.ToString().ToSafeAlias(), relation, ChangeType.Import);
		}

		/// <summary>
		/// Takes an existing IRelation object and creates a data XML node for exporting.
		/// </summary>
		/// <param name="item">The item.</param>
		/// <returns></returns>
		internal override SyncAttempt<XElement> SerializeCore(IRelation item)
		{
			var node = new XElement(NODE_NAME);

			// S6 TODO NOTE: We're only mapping Guids for DOCUMENTS at the moment, though Relations can exist for many other entities: Members, DocumentTypes, Media, MediaTypes, Recycle Bin, etc...
			
			IContent child = contentService.GetById(item.ChildId);
			IContent parent = contentService.GetById(item.ParentId);
			string childKeyValue = child != null ? child.Key.ToString() : string.Empty;
			string parentKeyValue = parent != null ? parent.Key.ToString() : string.Empty;
			XElement relationMapping = XElement.Parse(item.Comment);
			int propertyTypeId = -1; 
			int dataTypeDefinitionId = -1;
			string propertyTypeKeyValue = string.Empty;
			string dataTypeDefinitionKeyValue = string.Empty;

			if (relationMapping != null)
			{				
				propertyTypeId = relationMapping.Attribute("PropertyTypeId").ValueOrDefault(-1);				
				if (propertyTypeId > 0)
				{
					PropertyType propertyType = child.PropertyTypes.FirstOrDefault(x => x.Id == propertyTypeId);
					if (propertyType != null)
					{
						propertyTypeKeyValue = propertyType.Key.ToString();
					}
                }

				dataTypeDefinitionId = relationMapping.Attribute("DataTypeDefinitionId").ValueOrDefault(-1);

				if(dataTypeDefinitionId > 0)
				{
					IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);
					if(dataTypeDefinition != null)
					{
						dataTypeDefinitionKeyValue = dataTypeDefinition.Key.ToString();
                    }					
				}
			}			

			node.Add(new XElement("Id", item.Id));
			node.Add(new XElement("ChildId", item.ChildId));
			node.Add(new XElement("ChildKey", childKeyValue));
			node.Add(new XElement("ParentId", item.ParentId)); 
			node.Add(new XElement("ParentKey", parentKeyValue));
			node.Add(new XElement("Key", item.Key));
			node.Add(new XElement("RelationTypeKey", item.RelationType.Key));
			node.Add(new XElement("RelationTypeId", item.RelationTypeId)); 
			node.Add(new XElement("Comment", item.Comment)); 
			node.Add(new XElement("PropertyTypeKey", propertyTypeKeyValue));
			node.Add(new XElement("DataTypeDefinitionKey", dataTypeDefinitionKeyValue));
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
