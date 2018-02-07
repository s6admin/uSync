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
				Since Relations don't have persisted Keys and Aliases can be changed from the Umbraco backoffice
				we attempt to distinguish each processed Relation with a custom RelationKey Guid added to the
				Comment data. If that is not available the fallback (vanilla Umbraco) is to use a key 
				combination of parentId + childId + relType which matches the database table IX constraint
			 */
			string relationName = GetRelationNameLabel(node);			
			Guid childKey = node.Element("ChildKey").ValueOrDefault(Guid.Empty);
			Guid parentKey = node.Element("ParentKey").ValueOrDefault(Guid.Empty);
			Guid relationKey = Guid.Empty; // May come from Comment data
			Guid relationTypeKey = node.Element("RelationTypeKey").ValueOrDefault(Guid.Empty);
			Guid propertyTypeKey = node.Element("PropertyTypeKey").ValueOrDefault(Guid.Empty);
			Guid dataTypeDefinitionKey = node.Element("DataTypeDefinitionKey").ValueOrDefault(Guid.Empty);
			string relationTypeAlias = node.Element("RelationTypeAlias").ValueOrDefault(string.Empty);
			XElement relationComment = null;
			bool convertCommentIds = false;

			#region Ensure Required data is present

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

			relationComment = XElement.Parse(node.Element("Comment").ValueOrDefault(string.Empty));
			if (relationComment == null) //TODO THis is never null
			{
				string msg = "No RelationMapping data was found to deserialize for " + relationName;
				LogHelper.Warn(typeof(RelationSerializer), msg);
				return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
			}
			else if(relationComment.Element("RelationMapping") != null)
			{
				// PropertyAlias, PropertyTypeKey and DataTypeDefinitionKey are part of nuPicker RelationMapping 
				// but are not required for vanilla Relation records so they need to be processed as a group if .
				// They must either be all present or all omitted otherwise we won't allow the Relation to be imported

				string propertyAlias = relationComment.Attribute("PropertyAlias").ValueOrDefault(string.Empty);

				// If all three properties are absent the comment doesn't require any Id processing and is deserialized as-is
				if (propertyAlias.IsNullOrWhiteSpace() && propertyTypeKey.Equals(Guid.Empty) && dataTypeDefinitionKey.Equals(Guid.Empty))
				{
					// A non-nuPicker relation is being processed (but still has our custom Relation Key wrapped in the <RelationMapping> tag
					convertCommentIds = false;
				} else
				{
					convertCommentIds = true;
					// Verify that each required key is present to map PropertyTypeId and DataTypeDefinitionId
					if (propertyTypeKey.Equals(Guid.Empty))
					{
						string msg = "Could not find required PropertyTypeKey to deserialize for " + relationName;
						LogHelper.Warn(typeof(RelationSerializer), msg);
						return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
					}
					if (dataTypeDefinitionKey.Equals(Guid.Empty))
					{
						string msg = "Could not find required DataTypeDefinitionKey to deserialize for " + relationName;
						LogHelper.Warn(typeof(RelationSerializer), msg);
						return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
					}
				}											
			}

			#endregion Ensure Required data is present

			#region Deserialize data

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

			if (convertCommentIds)
			{
				PropertyType propertyType = child.PropertyTypes.Concat(parent.PropertyTypes).FirstOrDefault(x => x.Key == propertyTypeKey);
				if (propertyType == null)
				{
					string msg = "Could not find property type " + propertyTypeKey + " for " + relationName;
					LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
				}

				IDataTypeDefinition dataTypeDefinition = dataTypeService.GetDataTypeDefinitionById(dataTypeDefinitionKey);
				if (dataTypeDefinition == null)
				{
					string msg = "Could not find data type definition " + dataTypeDefinitionKey + " for " + relationName;
					LogHelper.Warn(typeof(RelationSerializer), msg);
					return SyncAttempt<IRelation>.Fail(relationName, ChangeType.Import, msg);
				}

				if (relationComment.Element("RelationMapping") != null)
				{
					// Ensure Id values in Comment node are updated and correct for the target environment before continuing with import
					relationComment.Attribute("PropertyTypeId").SetValue(propertyType.Id);
					relationComment.Attribute("DataTypeDefinitionId").SetValue(dataTypeDefinition.Id);
				}
			}

			// Get Relation Key from Comment if one exists. Do NOT create a Key here. That is performed during the Relation's Saving event
			relationKey = relationComment.Attribute("RelationKey").ValueOrDefault(Guid.Empty);

			// Look for existing relation record
			var relation = default(IRelation);
			var allRelationsByType = relationService.GetAllRelationsByRelationType(relationType.Id);						
			if(allRelationsByType != null && allRelationsByType.Any()) 
			{
				// Primary match by custom Relation Key
				if (!relationKey.Equals(Guid.Empty))
				{					
					relation = allRelationsByType.FirstOrDefault(x =>
						(x.Comment.Length > 0 &&
						XElement.Parse(x.Comment).Attribute("RelationKey").ValueOrDefault(Guid.Empty).Equals(relationKey)));
				}

				// If Relation by custom Key isn't found, check for a match using the parent/child Ids
				if(relation == null)
				{
					relation = allRelationsByType.FirstOrDefault(x => x.ChildId == child.Id && x.ParentId == parent.Id);
					
					// If relation type is bidirectional check the opposite parent/child combination and consider it a match, otherwise a second/reverse relation record would be created for the same relationship
					if (relation == null && relationType.IsBidirectional)
					{
						relation = allRelationsByType.FirstOrDefault(x => x.ChildId == parent.Id && x.ParentId == child.Id);
					}
				}				
            }
						
			if (relation == default(IRelation))
			{
				// No matching relation record found, create a new one. Don't create a custom Relation Key here, that will be handled in the Save routine		
				relation = new Relation(parent.Id, child.Id, relationType);				
			}

			// Update relation record values
			relation.ChildId = child.Id;
			relation.ParentId = parent.Id;
			relation.Comment = relationComment.ToString();

			#endregion Deserialize data

			bool saved;

			try
			{
				relationService.Save(relation); 
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
			Guid relationKey = Guid.Empty;
			XElement relationMappingComment = relation.Comment.Length > 0 ? XElement.Parse(relation.Comment) : null;

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
						//return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
					}
                } else
				{
					string msg = "PropertyTypeId could not be retrieved from Relation Comment XML data.";					
					LogHelper.Warn(typeof(RelationSerializer), msg);
					//return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
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
						//return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
					}					
				} else
				{
					string msg = "Data type definition Id could not be retrieved from Relation Comment XML data.";
					LogHelper.Warn(typeof(RelationSerializer), msg);
					//return SyncAttempt<XElement>.Fail(GetRelationNameLabel(relation), ChangeType.Export, msg);
				}

				// Look for our custom RelationKey. If one isn't present we need to either create an empty placeholder value or omitt the value entirely
				relationKey = relationMappingComment.Attribute("RelationKey").ValueOrDefault(Guid.Empty);
				if (relationKey.Equals(Guid.Empty))
				{
					//relationKey = Guid.NewGuid();
					//relationMappingComment.SetAttributeValue("RelationKey", relationKey); 					
				}
			}
						
			node.Add(new XElement("ChildId", relation.ChildId));
			node.Add(new XElement("ChildKey", child.Key));
			node.Add(new XElement("ParentId", relation.ParentId)); 
			node.Add(new XElement("ParentKey", parent.Key));
			node.Add(new XElement("RelationTypeAlias", relation.RelationType.Alias));		
			node.Add(new XElement("RelationTypeKey", relation.RelationType.Key));			
			node.Add(new XElement("Comment", relationMappingComment == null ? string.Empty : relationMappingComment.ToString()));
			if (!propertyTypeKeyValue.IsNullOrWhiteSpace())
			{
				node.Add(new XElement("PropertyTypeKey", propertyTypeKeyValue));
			}
			if (!dataTypeDefinitionKeyValue.IsNullOrWhiteSpace())
			{
				node.Add(new XElement("DataTypeDefinitionKey", dataTypeDefinitionKeyValue));
			}			
			
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
				// If an Exception is thrown during IsUpdate (ie. an import "Report") it should be surpressed in case the missing item(s) that caused the error are part of the Import process.
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

			Guid childKey = node.Element("ChildKey").ValueOrDefault(Guid.Empty);
			Guid parentKey = node.Element("ParentKey").ValueOrDefault(Guid.Empty);
			Guid relationTypeKey = node.Element("RelationTypeKey").ValueOrDefault(Guid.Empty);
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
			XElement comment = relation.Comment.Length > 0 ? XElement.Parse(relation.Comment) : null;
			Guid relationKey = comment != null ? comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty) : Guid.Empty;
			string fileNameKey = relationKey.Equals(Guid.Empty) ? relation.Id.ToString() : relationKey.ToString();

			return "Relation " + relation.Id + " " + fileNameKey;
		}

		public string GetRelationNameLabel(XElement node)
		{
			Guid relationKey = node.Element("Comment").Attribute("RelationKey").ValueOrDefault(Guid.Empty);
			string fileNameKey = relationKey.Equals(Guid.Empty) ? node.Element("Id").ValueOrDefault(string.Empty) : relationKey.ToString();

			//string label = node.Element("Id").ValueOrDefault(string.Empty) + 
			//	node.Element("Comment").Attribute("PropertyTypeAlias").ValueOrDefault(string.Empty) +				
			//	" Parent: " + node.Element("ParentId").ValueOrDefault(string.Empty) +
			//	" Child: " + node.Element("ChildId").ValueOrDefault(string.Empty);

			return "Relation " + fileNameKey;
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
