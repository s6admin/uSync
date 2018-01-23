using Jumoo.uSync.Core.Mappers;
using Jumoo.uSync.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Models;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

// S6
namespace Jumoo.uSync.ContentMappers
{
	// you can roll your own mappers, by implementing the IContentMapper interface and putting settings in uSyncCore.Config
	class NuPickerContentMapper : IContentMapper
	{
		private string _exportRegex = string.Empty;
		private readonly IDataTypeService dataTypeService;
		private readonly IRelationService relationService;

		public NuPickerContentMapper()
		{			
			_exportRegex = @"\d{4,9}";
			dataTypeService = ApplicationContext.Current.Services.DataTypeService;
			relationService = ApplicationContext.Current.Services.RelationService;
		}

		public virtual string GetExportValue(int dataTypeDefinitionId, string value)
		{

			IEnumerable<IRelation> relations = null;
            PreValue relationMappingPreValue = null;
			string relationAlias = string.Empty;
			
			try
			{
				// Check NuPicker relation mapping to determine if relations should be updated in uSync data directory				
				relationMappingPreValue = dataTypeService.GetPreValuesCollectionByDataTypeId(dataTypeDefinitionId).PreValuesAsDictionary["relationMapping"];
				if(relationMappingPreValue != null && relationMappingPreValue.Value != null)
				{
					relationAlias = JObject.Parse(relationMappingPreValue.Value).GetValue("relationTypeAlias").ToString();
				}				
			}
			catch(Exception ex)
			{
				LogHelper.Error(typeof(NuPickerContentMapper), ex.Message, ex);
			}

			if (!relationAlias.IsNullOrWhiteSpace())
			{
				relations = GetRelationsForNuPicker(dataTypeDefinitionId, relationAlias);
			}

            if (string.IsNullOrWhiteSpace(value))
			{				
				if(value == null)
				{
					// If there is no relation mapping make sure any associated Relation files are deleted from the uSync data directory					
					if (relationAlias.IsNullOrWhiteSpace())
					{
						
					}

					return value;
				}				
			}
				
			LogHelper.Debug<NuPickerContentMapper>(">> Export Value: {0}", () => value);

			Dictionary<string, string> replacements = new Dictionary<string, string>();

			foreach (Match m in Regex.Matches(value, _exportRegex))
			{
				int id;
				if (int.TryParse(m.Value, out id))
				{
					Guid? itemGuid = GetGuidFromId(id);
					if (itemGuid != null && !replacements.ContainsKey(m.Value))
					{
						replacements.Add(m.Value, itemGuid.ToString().ToLower());
					}
				}
			}

			if (!relationAlias.IsNullOrWhiteSpace())
			{
				// relationService.GetEntitiesFromRelations
				// S6 Get all Relations for this property editor and process them through RelationHandler
												
				if(relations != null && relations.Any())
				{
					foreach (IRelation r in relations)
					{
						//relationService.Save(r); // Temporarily disable until we determine if RelationHandler registered Save event fires during nuPicker RelationMappingEvent.Update event
					}
				}				
			}

			foreach (var pair in replacements)
			{
				value = value.Replace(pair.Key, pair.Value);
			}

			LogHelper.Debug<NuPickerContentMapper>("<< Export Value: {0}", () => value);
			return value;

		}

		public virtual string GetImportValue(int dataTypeDefinitionId, string content)
		{
			Dictionary<string, string> replacements = new Dictionary<string, string>();

			string guidRegEx = @"\b[A-Fa-f0-9]{8}(?:-[A-Fa-f0-9]{4}){3}-[A-Fa-f0-9]{12}\b";

			foreach (Match m in Regex.Matches(content, guidRegEx))
			{
				var id = GetIdFromGuid(Guid.Parse(m.Value));

				if ((id != -1) && (!replacements.ContainsKey(m.Value)))
				{
					replacements.Add(m.Value, id.ToString());
				}
			}

			foreach (KeyValuePair<string, string> pair in replacements)
			{
				content = content.Replace(pair.Key, pair.Value);
			}

			return content;
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

		internal IEnumerable<IRelation> GetRelationsForNuPicker(int dataTypeDefinitionId, string relationAlias)
		{
			IRelationType rt = null;
			IEnumerable<IRelation> relations = Enumerable.Empty<IRelation>();

			rt = relationService.GetRelationTypeByAlias(relationAlias);
			if (rt != null)
			{
				relations = relationService.GetAllRelationsByRelationType(rt.Id)
				.Where(x => XElement.Parse(x.Comment).Attribute("DataTypeDefinitionId").ValueOrDefault(string.Empty) == dataTypeDefinitionId.ToString());
			}

			return relations;
		}
	}
}
