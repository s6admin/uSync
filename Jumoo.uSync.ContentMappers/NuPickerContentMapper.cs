using Jumoo.uSync.Core.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core;

// S6
namespace Jumoo.uSync.ContentMappers
{
	// you can roll your own mappers, by implementing the IContentMapper interface and putting settings in uSyncCore.Config
	class NuPickerContentMapper : IContentMapper
	{
		private string _exportRegex = string.Empty;
		public NuPickerContentMapper()
		{			
			_exportRegex = @"\d{4,9}";
		}

		public virtual string GetExportValue(int dataTypeDefinitionId, string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return value;

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
	}
}
