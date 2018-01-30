﻿using System;
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
			/* 				
				Sinces Relations don't have persisted Keys and Aliases can be changed from the Umbraco backoffice
				we need to use a combination of relType + parentId + childId to reliably identify matching Relation 
				records during a deserializing/import.			 
			 */
			string relationName = GetRelationNameLabel(node);			
			Guid childKey = node.Element("ChildKey").KeyOrDefault();
			Guid parentKey = node.Element("ParentKey").KeyOrDefault();
			Guid relationTypeKey = node.Element("RelationTypeKey").KeyOrDefault();
			Guid propertyTypeKey = node.Element("PropertyTypeKey").KeyOrDefault();
			Guid dataTypeDefinitionKey = node.Element("DataTypeDefinitionKey").KeyOrDefault();
						
			if (childKey.Equals(Guid.Empty)) {
				string msg = "Could not find ChildKey to deserialize for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}
			if(parentKey.Equals(Guid.Empty))
			{
				string msg = "Could not find ParentKey to deserialize for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}			
			if (relationTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not find RelationTypeKey to deserialize for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}
			if (propertyTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not find PropertyTypeKey to deserialize for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}
			if (dataTypeDefinitionKey.Equals(Guid.Empty))
			{
				string msg = "Could not find DataTypeDefinitionKey to deserialize for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}

			// If all keys are valid, begin retrieving content in target environment

			IContent child = contentService.GetById(childKey); 
			if(child == null)
			{
				string msg = "Could not find child content " + childKey.ToString() + " for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}
						
			int parentId = GetIdFromGuid(parentKey);
			if(parentId <= 0)
			{
				string msg = "Could not find parent content " + parentKey.ToString() + " for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}

			IRelationType relationType = relationService.GetRelationTypeById(relationTypeKey);			
			if(relationType == null)
			{
				string msg = "Could not find relation type " + relationTypeKey.ToString() + " for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}

			PropertyType propertyType = child.PropertyTypes.FirstOrDefault(x => x.Key == propertyTypeKey);
			IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionKey);
			
			XElement relationMapping = XElement.Parse(node.Element("Comment").ValueOrDefault(string.Empty));
			
			if (relationMapping == null)
			{
				// S6 TODO Should this completely fail or can we attempt to reassemble the RelationMapping tag since we know its structure?
				string msg = "Could not deserialize relation mapping xml node for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			} else
			{
				// Ensure values in Comment node are updated and correct before we				
				relationMapping.Attribute("PropertyTypeId").SetValue(propertyType.Id); 				
				relationMapping.Attribute("DataTypeDefinitionId").SetValue(dataTypeDefinition.Id);                
			}

			// Look for existing relation record
			var relation = default(IRelation);
			var allRelations = relationService.GetAllRelationsByRelationType(relationType.Id);						
			if(allRelations != null && allRelations.Any()) 
			{
				relation = allRelations.FirstOrDefault(x => x.ChildId == child.Id && x.ParentId == parentId && x.RelationType.Alias == relationType.Alias);
            }
			
			if (relation == default(IRelation))
			{
				// No matching relation record found, create a new one				
				relation = new Relation(parentId, child.Id, relationType);								
			}
			
			// Update relation record values
			relation.ChildId = child.Id;
			relation.ParentId = parentId;
			relation.Comment = relationMapping.ToString();

			bool saved;

			try
			{
				relationService.Save(relation); // TODO Save record once we've confirmed properties are correct
				saved = true;
			} catch(Exception ex)
			{
				LogHelper.Error(typeof(RelationSerializer), ex.Message, ex);
				saved = false;
			}
						
			return SyncAttempt<IRelation>.SucceedIf(saved, GetRelationNameLabel(relation), relation, ChangeType.Import);
		}
		
		/// <summary>
		/// Takes an existing IRelation object and creates a data XML node for exporting.
		/// </summary>
		/// <param name="relation">The item.</param>
		/// <returns></returns>
		internal override SyncAttempt<XElement> SerializeCore(IRelation relation)
		{
			var node = new XElement(NODE_NAME);

			// TODO NOTE: We're only mapping Guids for DOCUMENTS at the moment, though Relations can exist for many other entities: Members, DocumentTypes, Media, MediaTypes, Recycle Bin, etc...
			
			IContent child = contentService.GetById(relation.ChildId);			
			Guid? childKeyValue = child != null ? child.Key : Guid.Empty;
			Guid? parentKeyValue = GetGuidFromId(relation.ParentId);
			XElement relationMappingComment = XElement.Parse(relation.Comment);
			int propertyTypeId = -1; 
			int dataTypeDefinitionId = -1;
			string propertyTypeKeyValue = string.Empty;
			string dataTypeDefinitionKeyValue = string.Empty;

			if (childKeyValue == null || childKeyValue.Equals(Guid.Empty))
			{
				LogHelper.Warn(typeof(RelationSerializer), "Could not retrieve child key from Relation's childId " + relation.ChildId);
			}
			if (parentKeyValue == null || parentKeyValue.Equals(Guid.Empty))
			{
				LogHelper.Warn(typeof(RelationSerializer), "Could not retrieve parent key from Relation's parentId " + relation.ParentId);
			}			

			if (relationMappingComment != null)
			{				
				propertyTypeId = relationMappingComment.Attribute("PropertyTypeId").ValueOrDefault(-1);				
				if (propertyTypeId > 0)
				{
					PropertyType propertyType = child.PropertyTypes.FirstOrDefault(x => x.Id == propertyTypeId);
					if (propertyType != null)
					{
						propertyTypeKeyValue = propertyType.Key.ToString();
					}
                }

				dataTypeDefinitionId = relationMappingComment.Attribute("DataTypeDefinitionId").ValueOrDefault(-1);

				if(dataTypeDefinitionId > 0)
				{
					IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);
					if(dataTypeDefinition != null)
					{
						dataTypeDefinitionKeyValue = dataTypeDefinition.Key.ToString();
                    }					
				}
			}
						
			node.Add(new XElement("ChildId", relation.ChildId));
			node.Add(new XElement("ChildKey", childKeyValue));
			node.Add(new XElement("ParentId", relation.ParentId)); 
			node.Add(new XElement("ParentKey", parentKeyValue));			
			node.Add(new XElement("RelationTypeKey", relation.RelationType.Key));			
			node.Add(new XElement("Comment", relation.Comment)); 
			node.Add(new XElement("PropertyTypeKey", propertyTypeKeyValue));
			node.Add(new XElement("DataTypeDefinitionKey", dataTypeDefinitionKeyValue));

			return SyncAttempt<XElement>.SucceedIf(
			node != null, GetRelationNameLabel(relation), node, typeof(IRelation), ChangeType.Export);
		}

		public override bool IsUpdate(XElement node)
		{
			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return true;

			IRelation item = null;
			try
			{
				item = GetRelation(node);
			}
			catch (Exception ex)
			{
				// GetRelation may throw an Exception if the specified RelationType isn't found, but we aren't concerned about this during IsUpdate calls
			}
			
			if (item == null)
				return true;

			var attempt = Serialize(item);
			if (!attempt.Success)
				return true;

			var itemHash = attempt.Item.GetSyncHash();

			return (!nodeHash.Equals(itemHash));			
		}

		public IEnumerable<uSyncChange> GetChanges(XElement node)
		{
			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
			{
				return null; //return uSyncChangeTracker.ChangeError(GetRelationNameLabel(node));
			}

			IRelation item = null;
			try {
				item = GetRelation(node);
			} catch(Exception ex) {
				LogHelper.Warn(typeof(RelationSerializer), ex.Message + " Item will be skipped.");
                //return uSyncChangeTracker.ChangeError(GetRelationNameLabel(node));
			}
			if (item == null)
			{
				return null; //return uSyncChangeTracker.ChangeError(GetRelationNameLabel(item));
			}
			else if (item == default(IRelation))
			{
				return uSyncChangeTracker.NewItem(GetRelationNameLabel(item));
			}				

			var attempt = Serialize(item);
			if (attempt.Success)
			{
				return uSyncChangeTracker.GetChanges(node, attempt.Item, "");
			}
			else
			{
				return uSyncChangeTracker.ChangeError(GetRelationNameLabel(item));
			}
		}

		private bool IsDifferent(XElement node)
		{
			LogHelper.Debug<RelationSerializer>("Using IsDifferent Checker");
			var key = node.Attribute("guid").ValueOrDefault(Guid.Empty);
			if (key == Guid.Empty)
				return true;

			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return true;

			IRelation item = null;
			try
			{
				item = GetRelation(node);
			}
			catch (Exception ex)
			{
				//LogHelper.Warn(typeof(RelationSerializer), ex.Message);
				// GetRelation may throw an Exception if the specified RelationType isn't found, but we aren't concerned about this during IsDifferent calls
			}
			if (item == null)
				return true;

			var attempt = Serialize(item);
			if (!attempt.Success)
				return true;

			var itemHash = attempt.Item.GetSyncHash();

			return (!nodeHash.Equals(itemHash));
		}

		private IRelation GetRelation(XElement node)
		{
						
			Guid childKey = node.Element("ChildKey").KeyOrDefault();
			Guid parentKey = node.Element("ParentKey").KeyOrDefault();
			Guid relationTypeKey = node.Element("RelationTypeKey").KeyOrDefault();
			IRelation relation = default(IRelation);
			IRelationType relationType = null;

			if (childKey.Equals(Guid.Empty))						
			{
				string msg = "Could not find ChildKey to deserialize for Relation ";
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return null;
			}
			if (parentKey.Equals(Guid.Empty))
			{
				string msg = "Could not find ParentKey to deserialize for Relation ";
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return null;
			}
			if (relationTypeKey.Equals(Guid.Empty))
			{
				string msg = "Could not find RelationTypeKey to deserialize for Relation ";
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return null;			
			}
				
			relationType = relationService.GetRelationTypeById(relationTypeKey);

			if(relationType == null)
			{
				// If the Relation's RelationType isn't found it should be skipped entirely
				throw new Exception("Could not determine RelationType for " + GetRelationNameLabel(node) + ".");
			}

			IEnumerable<IRelation> relationResults = relationService.GetByRelationTypeId(relationType.Id);
			if (relationResults == null || !relationResults.Any())
			{
				return null;
			}

			int parentId = GetIdFromGuid(parentKey);
			int childId = GetIdFromGuid(childKey);

			relation = relationResults.FirstOrDefault(x => x.ParentId == parentId && x.ChildId == childId);

			return relation;
		}

		public string GetRelationNameLabel(IRelation relation)
		{
			return "Relation " + relation.Id;
		}

		public string GetRelationNameLabel(XElement node)
		{
			string label = node.Element("Comment").Attribute("PropertyTypeId").ValueOrDefault(string.Empty) +
				"Parent: " + node.Element("ParentId").ValueOrDefault(string.Empty) +
				" Child: " + node.Element("ChildId").ValueOrDefault(string.Empty);
			return "Relation " + label;
		}

		internal int GetIdFromGuid(Guid guid)
		{
			var item = ApplicationContext.Current.Services.EntityService.GetByKey(guid);
			if (item != null)
				return item.Id;

			return -1;
		}

		internal Guid? GetGuidFromId(int id)
		{
			var item = ApplicationContext.Current.Services.EntityService.Get(id);
			if (item != null)
				return item.Key;

			return null;
		}
	}
}