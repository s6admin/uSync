using Jumoo.uSync.Core.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// S6
namespace Jumoo.uSync.ContentMappers
{
	// you can roll your own mappers, by implementing the IContentMapper interface and putting settings in uSyncCore.Config
	class NuPickerContentMapper : IContentMapper
	{
		public string GetExportValue(int dataTypeDefinitionId, string value)
		{
			/*
				Existing output from Core:
				<Picker>
					<Picked Key="####"><![CDATA[Picked Item Name]]></Picked>
					...
				</Picker>
			*/
			throw new NotImplementedException();
		}

		public string GetImportValue(int dataTypeDefinitionId, string content)
		{
			throw new NotImplementedException();
		}
	}
}
