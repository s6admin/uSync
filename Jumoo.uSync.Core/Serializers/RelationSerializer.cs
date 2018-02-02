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
			string relationTypeAlias = node.Element("RelationTypeAlias").ValueOrDefault(string.Empty);
						
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
						
			IContent parent = contentService.GetById(parentKey);
			if(parent == null)
			{
				string msg = "Could not find parent content " + parentKey.ToString() + " for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}
						
			IRelationType relationType = relationService.GetRelationTypeById(relationTypeKey);			
			if(relationType == null)
			{
				relationType = relationService.GetRelationTypeByAlias(relationTypeAlias);
				if(relationType == null)
				{
					string msg = "Could not find relation type " + relationTypeAlias + " (" + relationTypeKey.ToString() + ") " + " for " + relationName;
					LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
				}				
			}

			PropertyType propertyType = child.PropertyTypes.Concat(parent.PropertyTypes).FirstOrDefault(x => x.Key == propertyTypeKey);
			if(propertyType == null)
			{
				string msg = "Could not find property type " + propertyTypeKey + " for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}

			IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionKey);
			if(dataTypeDefinition == null)
			{
				string msg = "Could not find data type definition " + dataTypeDefinitionKey + " for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}

			XElement relationMapping = XElement.Parse(node.Element("Comment").ValueOrDefault(string.Empty));			
			if (relationMapping == null)
			{				
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
				relation = allRelations.FirstOrDefault(x => x.ChildId == child.Id && x.ParentId == parent.Id && x.RelationType.Alias == relationType.Alias);
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
			if(child == null)
			{
				string msg = "Could not get Child content with Id " + relation.ChildId;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
			}

			IContent parent = contentService.GetById(relation.ParentId);
			if (parent == null)
			{
				string msg = "Could not get Parent content with Id " + relation.ParentId;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
			}
						
			
			int propertyTypeId = -1; 
			int dataTypeDefinitionId = -1;
			string propertyTypeKeyValue = string.Empty;
			string dataTypeDefinitionKeyValue = string.Empty;
			XElement relationMappingComment = XElement.Parse(relation.Comment);

			if (relationMappingComment != null)
			{				
				propertyTypeId = relationMappingComment.Attribute("PropertyTypeId").ValueOrDefault(-1);				
				if (propertyTypeId > 0)
				{
					PropertyType propertyType = child.PropertyTypes.Concat(parent.PropertyTypes).FirstOrDefault(x => x.Id == propertyTypeId);
					if (propertyType != null)
					{
						propertyTypeKeyValue = propertyType.Key.ToString();
					} else
					{
						string msg = "PropertyType not found for propertyTypeId " + propertyTypeId;
						LogHelper.Warn(typeof(RelationSerializer), msg);
						return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
					}
                } else
				{
					string msg = "PropertyTypeId could not be retrieved from Relation Comment XML data.";					
					LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
				}

				dataTypeDefinitionId = relationMappingComment.Attribute("DataTypeDefinitionId").ValueOrDefault(-1);

				if(dataTypeDefinitionId > 0)
				{
					IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionId);
					if(dataTypeDefinition != null)
					{
						dataTypeDefinitionKeyValue = dataTypeDefinition.Key.ToString();
                    } else
					{
						string msg = "Data type definition " + dataTypeDefinitionId + " was not found for " + GetRelationNameLabel(relation);
						LogHelper.Warn(typeof(RelationSerializer), msg);
						return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
					}					
				} else
				{
					string msg = "Data type definition Id could not be retrieved from Relation Comment XML data.";
					LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
				}
			}
						
			node.Add(new XElement("ChildId", relation.ChildId));
			node.Add(new XElement("ChildKey", child.Key));
			node.Add(new XElement("ParentId", relation.ParentId)); 
			node.Add(new XElement("ParentKey", parent.Key));
			node.Add(new XElement("RelationTypeAlias", relation.RelationType.Alias));		
			node.Add(new XElement("RelationTypeKey", relation.RelationType.Key)); // TODO RelationTypeKey doesn't appear to match between environments. Should they be updated on import?
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
			} catch(Exception ex)
			{
				// If an Exception is thrown during IsUpdate it should be surpressed because the missing item(s) that cased the error may be a part of the Import process.
				// TODO We may be able to check for this case deeper in the uSync core, but is only a "nice to have"
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
				return null; 
			}

			IRelation item = null;
			try {
				item = GetRelation(node);
			} catch(Exception ex) {
				// NOTE: Exception is already logged in GetRelation
				return uSyncChangeTracker.ChangeError(GetRelationNameLabel(item));
			}
			
			if (item == null)
			{
				return uSyncChangeTracker.NewItem(GetRelationNameLabel(node));
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

		//private bool IsDifferent(XElement node)
		//{
		//	LogHelper.Debug<RelationSerializer>("Using IsDifferent Checker");
		//	var key = node.Attribute("guid").ValueOrDefault(Guid.Empty);
		//	if (key == Guid.Empty)
		//		return true;

		//	var nodeHash = node.GetSyncHash();
		//	if (string.IsNullOrEmpty(nodeHash))
		//		return true;

		//	IRelation item = null;
			
		//	item = GetRelation(node); // Can throw exception which is bubbled and caught
			
		//	if (item == null)
		//		return true;

		//	var attempt = Serialize(item);
		//	if (!attempt.Success)
		//		return true;

		//	var itemHash = attempt.Item.GetSyncHash();

		//	return (!nodeHash.Equals(itemHash));
		//}

		/// <summary>
		/// Returns an IRelation record if a Relation database entry is found that matches the keys provided in the passed node XElement. 
		/// Since Relations do not have truly unique keys this method relies on a combination of the Relation's Type Key, ParentId, and ChildId.
		/// Because of this the GetRelation method will return in one of three ways:
		/// 1. Will return a matching Relation object, implying the Relation data either needs to be updated or has no change
		/// 2. Will return NULL if no matching Relation was found, implying the Relation is NEW and can be created.
		/// 3. Will throw an Exception *that must be caught* if any of the three key objects cannot be found in order to perform the Relation lookup, implying there is an error in the Relation data or one of the expected keys objects. These Relation items should be flagged as errors.
		/// </summary>
		/// <param name="node">The Relation data node.</param>
		/// <returns></returns>
		private IRelation GetRelation(XElement node)
		{
						
			Guid childKey = node.Element("ChildKey").KeyOrDefault();
			Guid parentKey = node.Element("ParentKey").KeyOrDefault();
			Guid relationTypeKey = node.Element("RelationTypeKey").KeyOrDefault();
			string relationTypeAlias = node.Element("RelationTypeAlias").ValueOrDefault(string.Empty);
			IRelation relation = null;
			IRelationType relationType = null;

			if (childKey.Equals(Guid.Empty))						
			{				
				Exception ex = new Exception("Could not find ChildKey to deserialize for Relation ");
				LogHelper.Warn(typeof(RelationSerializer), ex.Message);
				throw ex;
			}
			if (parentKey.Equals(Guid.Empty))
			{
				Exception ex = new Exception("Could not find ParentKey to deserialize for Relation ");				
				LogHelper.Warn(typeof(RelationSerializer), ex.Message);
				throw ex;
			}
			if (relationTypeKey.Equals(Guid.Empty))
			{
				Exception ex = new Exception("Could not find RelationTypeKey to deserialize for Relation ");				
				LogHelper.Warn(typeof(RelationSerializer), ex.Message);
				throw ex;
			}
				
			relationType = relationService.GetRelationTypeById(relationTypeKey);

			if(relationType == null)
			{
				relationType = relationService.GetRelationTypeByAlias(relationTypeAlias);

				if(relationType== null)
				{
					// If the Relation's RelationType couldn't be found by either Key or Alias, the Relation item should be skipped entirely
					Exception ex = new Exception("Could not determine RelationType for Relation");
					LogHelper.Warn(typeof(RelationSerializer), ex.Message);
					throw ex;
				}				
			}

			IEnumerable<IRelation> relationResults = relationService.GetByRelationTypeId(relationType.Id);
			if (relationResults == null || !relationResults.Any())
			{
				return null;
			}

			int parentId = GetIdFromGuid(parentKey);
			int childId = GetIdFromGuid(childKey);

			if (relationType.IsBidirectional)
			{
				relation = relationResults.FirstOrDefault(x => (x.ParentId == parentId && x.ChildId == childId) || (x.ParentId == childId && x.ChildId == parentId));
			} else
			{
				relation = relationResults.FirstOrDefault(x => x.ParentId == parentId && x.ChildId == childId);
			}		

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
