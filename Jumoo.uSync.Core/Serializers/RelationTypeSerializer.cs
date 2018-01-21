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


namespace Jumoo.uSync.Core.Serializers
{
	public class RelationTypeSerializer : SyncBaseSerializer<IRelationType>, ISyncChangeDetail
	{
		private readonly IRelationService relationService;
		private const string NODE_NAME = "RelationType"; // S6 "Alias"? "Name"? Other?...

		public override string SerializerType => uSyncConstants.Serailization.RelationType;

		public RelationTypeSerializer()
			: base(uSyncConstants.Serailization.RelationType)
		{
			relationService = ApplicationContext.Current.Services.RelationService;
		}

		public RelationTypeSerializer(string itemType) : base(itemType) { }

		internal override SyncAttempt<IRelationType> DeserializeCore(XElement node)
		{
			var relationTypeAlias = node.Element(NODE_NAME).ValueOrDefault(""); // S6 Name or Alias?
			if (string.IsNullOrEmpty(relationTypeAlias))
				return SyncAttempt<IRelationType>.Fail(node.NameFromNode(), ChangeType.Import, "Missing RelationType");

			//var languageId = node.Element("LanguageId").ValueOrDefault(-1);
			//var rootKeyNode = node.Element("RootContent");

			//IContent contentNode = null;
			//if (rootKeyNode != null)
			//{
			//	var rootKey = rootKeyNode.Attribute("Key").ValueOrDefault(Guid.Empty);
			//	contentNode = ApplicationContext.Current.Services.ContentService.GetById(rootKey);
			//}

			var allRelationTypes = relationService.GetAllRelationTypes();

			var relationType = default(IRelationType);
			if (allRelationTypes.Any(x => x.Alias == relationTypeAlias))
			{
				relationType = allRelationTypes.FirstOrDefault(x => x.Alias == relationTypeAlias);
			}

			//// S6 childType and parentType from node data
			//string childTypeValue = node.Element("ChildObjectType").ValueOrDefault(string.Empty);
			//string parentTypeValue = node.Element("ParentObjectType").ValueOrDefault(string.Empty);
			//Guid childType = childTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(childTypeValue); // S6 TODO Handle improper strings better 
			//Guid parentType = parentTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(parentTypeValue); // S6 TODO Handle improper strings better

			if (relationType == default(IRelationType))
			{
				
				string childTypeValue = node.Element("ChildObjectType").ValueOrDefault(string.Empty);
				string parentTypeValue = node.Element("ParentObjectType").ValueOrDefault(string.Empty);
				Guid childType = childTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(childTypeValue); // S6 TODO Handle improper strings better
				Guid parentType = parentTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(parentTypeValue); // S6 TODO Handle improper strings better

				relationType = new RelationType(childType, parentType, relationTypeAlias); //new UmbracoDomain(relationTypeAlias);				
			}

			//if (languageId > -1 && relationType.LanguageId != languageId)
			//	relationType.LanguageId = languageId;

			//if (contentNode != null)							
			//	relationType.RootContentId = contentNode.Id;
			
			//relationService.Save(relationType); // S6 TODO after we've successfully profiled this far

			return SyncAttempt<IRelationType>.Succeed(relationType.Alias, relationType, ChangeType.Import);

		}

		internal override SyncAttempt<XElement> SerializeCore(IRelationType item)
		{
			var node = new XElement(NODE_NAME);

			node.Add(new XElement("Alias", item.Alias));
			node.Add(new XElement("ChildObjectType", item.ChildObjectType));			
			node.Add(new XElement("IsBidirectional", item.IsBidirectional));
			node.Add(new XElement("Id", item.Id)); 
			node.Add(new XElement("Key", item.Key));
			node.Add(new XElement("Name", item.Name));
			node.Add(new XElement("ParentObjectType", item.ParentObjectType));
			
			return SyncAttempt<XElement>.SucceedIf(
				node != null, item.Alias, node, typeof(IRelationType), ChangeType.Export);

		}

		public override bool IsUpdate(XElement node)
		{
			var nodeHash = node.GetSyncHash();
			if (string.IsNullOrEmpty(nodeHash))
				return true;

			//var name = node.Element(NODE_NAME).ValueOrDefault(string.Empty);
			string relationTypeValue = node.Element("Key").ValueOrDefault(string.Empty); // S6 Confirm Key element can be found
			Guid relationTypeKey = relationTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(relationTypeValue); // S6 TODO Handle improper strings better
			if (relationTypeKey.Equals(Guid.Empty))
				return true;

			var item = relationService.GetRelationTypeById(relationTypeKey);
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
			string relationTypeValue = node.Element("Key").ValueOrDefault(string.Empty);
			Guid relationTypeKey = relationTypeValue.IsNullOrWhiteSpace() ? Guid.Empty : new Guid(relationTypeValue); // S6 TODO Handle improper strings better
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
