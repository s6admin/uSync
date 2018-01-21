using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jumoo.uSync.Core;
using Umbraco.Core.Models;
using Umbraco.Core;
using Umbraco.Core.Services;
using Umbraco.Core.Logging;
using System.IO;
using System.Xml.Linq;
using Jumoo.uSync.BackOffice.Helpers;
using Jumoo.uSync.Core.Extensions;

// S6
namespace Jumoo.uSync.BackOffice.Handlers
{
	class RelationTypeHandler : uSyncBaseHandler<IRelationType>, ISyncHandler, ISyncPostImportHandler
	{
		public string Name { get { return "uSync: RelationTypeHandler"; } }
		public int Priority { get { return uSyncConstants.Priority.RelationTypes; } }
		public string SyncFolder { get { return uSyncConstants.Serailization.RelationType; } }

		readonly IRelationService _relationService;
		readonly IEntityService _entityService;

		public RelationTypeHandler()
		{
			_relationService = ApplicationContext.Current.Services.RelationService;
			_entityService = ApplicationContext.Current.Services.EntityService;

			RequiresPostProcessing = true; // S6 Applicable?
		}

		public override SyncAttempt<IRelationType> Import(string filePath, bool force = false)
		{
			LogHelper.Debug<IRelationType>(">> Import: {0}", () => filePath);

			if (!System.IO.File.Exists(filePath))
				throw new FileNotFoundException(filePath);

			var node = XElement.Load(filePath);

			return uSyncCoreContext.Instance.RelationTypeSerializer.DeSerialize(node, force);
		}

		public override uSyncAction DeleteItem(Guid key, string keyString)
		{
			IRelationType item = null;
			if (!key.Equals(Guid.Empty))
			{
				item = _relationService.GetRelationTypeById(key);
			}

			if(item != null)
			{
				LogHelper.Info<RelationTypeHandler>("Deleting RelationType: {0}", () => item.Name);
				_relationService.Delete(item);
				return uSyncAction.SetAction(true, keyString, typeof(IRelationType), ChangeType.Delete, "Removed");
			}

			return uSyncAction.Fail(keyString, typeof(IRelationType), ChangeType.Delete, "RelationType " + keyString + " not found.");			
		}

		public IEnumerable<uSyncAction> ExportAll(string folder)
		{
			LogHelper.Info<RelationTypeHandler>("Exporting all RelationTypes.");
			List<uSyncAction> actions = new List<uSyncAction>();

			foreach(var item in _relationService.GetAllRelationTypes())
			{
				if(item != null)
				{
					actions.Add(ExportToDisk(item, folder));
				}
			}

			return actions;
		}

		private uSyncAction ExportToDisk(IRelationType item, string folder)
		{		

			if(item == null)
			{
				return uSyncAction.Fail(Path.GetFileName(folder), typeof(IRelationType), "item not set");
			}
			try
			{
				var attempt = uSyncCoreContext.Instance.RelationTypeSerializer.Serialize(item);
				var filename = string.Empty;

				if (attempt.Success)
				{
					filename = uSyncIOHelper.SavePath(folder, SyncFolder, item.Alias.ToSafeAlias()); // S6 Name/Alias or Key? Also used in registered event methods below
					uSyncIOHelper.SaveNode(attempt.Item, filename);
				}
				return uSyncActionHelper<XElement>.SetAction(attempt, filename);

			}
			catch (Exception ex)
			{
				return uSyncAction.Fail(item.Alias, item.GetType(), ChangeType.Export, ex);
			}
		}

		public void RegisterEvents()
		{
			RelationService.SavedRelationType += RelationService_SavedRelationType;
			RelationService.DeletedRelationType += RelationService_DeletedRelationType;
		}

		private void RelationService_DeletedRelationType(IRelationService sender, Umbraco.Core.Events.DeleteEventArgs<IRelationType> e)
		{
			if (uSyncEvents.Paused)
				return;

			foreach (var item in e.DeletedEntities)
			{
				LogHelper.Info<RelationTypeHandler>("Delete: Deleting uSync File for item: {0}", () => item.Alias);
				uSyncIOHelper.ArchiveRelativeFile(SyncFolder, item.Alias.ToSafeAlias());

				uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, item.Alias, typeof(IRelationType));
			}
		}

		private void RelationService_SavedRelationType(IRelationService sender, Umbraco.Core.Events.SaveEventArgs<IRelationType> e)
		{
			if (uSyncEvents.Paused)
				return;

			foreach (var item in e.SavedEntities)
			{
				LogHelper.Info<RelationTypeHandler>("Save: Saving uSync file for item: {0}", () => item.Alias);
				ExportToDisk(item, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);

				uSyncBackOfficeContext.Instance.Tracker.RemoveActions(item.Alias, typeof(IRelationType));
			}
		}

		public override uSyncAction ReportItem(string file)
		{
			var node = XElement.Load(file);
			var update = uSyncCoreContext.Instance.RelationTypeSerializer.IsUpdate(node);
			var action = uSyncActionHelper<IRelationType>.ReportAction(update, node.NameFromNode());
			if (action.Change > ChangeType.NoChange)
				action.Details = ((ISyncChangeDetail)uSyncCoreContext.Instance.RelationTypeSerializer).GetChanges(node);

			return action;
		}

		public IEnumerable<uSyncAction> ProcessPostImport(string folder, IEnumerable<uSyncAction> actions)
		{
			if (actions == null || !actions.Any())
				return null;

			// we get passed actions that need a second pass.
			var relationTypes = actions.Where(x => x.ItemType == typeof(IRelationType));
			if (relationTypes == null || !relationTypes.Any())
				return null;

			foreach (var action in relationTypes)
			{
				LogHelper.Debug<RelationTypeHandler>("Post Processing: {0} {1}", () => action.Name, () => action.FileName);
				var attempt = Import(action.FileName);
				if (attempt.Success)
				{
					ImportSecondPass(action.FileName, attempt.Item);
				}
			}

			return actions; //CleanEmptyContainers(folder, -1);
		}		
	}
}
