using Jumoo.uSync.BackOffice;
using Jumoo.uSync.BackOffice.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Jumoo.uSync.Core;
using Umbraco.Core.Services;
using Umbraco.Core;
using Umbraco.Core.Logging;
using System.IO;
using Jumoo.uSync.BackOffice.Helpers;
using System.Xml.Linq;
using Jumoo.uSync.Core.Extensions;
using Jumoo.uSync.Core.Helpers;

namespace Jumoo.uSync.Content
{
	class RelationHandler : uSyncBaseHandler<IRelation>, ISyncHandler
	{
		public string Name => "uSync: RelationHandler";
		public int Priority => uSyncConstants.Priority.Relations;
		public string SyncFolder => uSyncConstants.Serailization.Relation;

		readonly IRelationService _relationService;
		readonly IEntityService _entityService;
		
		public RelationHandler()
		{
			_relationService = ApplicationContext.Current.Services.RelationService;
			_entityService = ApplicationContext.Current.Services.EntityService;		
		}


		public IEnumerable<uSyncAction> ExportAll(string folder)
		{
			LogHelper.Info<RelationHandler>("Exporting all Relations.");

			List<uSyncAction> actions = new List<uSyncAction>();

			foreach (var item in _relationService.GetAllRelations())
			{
				if (item != null)
				{
					actions.Add(ExportToDisk(item, folder));
				}
			}

			return actions;			
		}

		private uSyncAction ExportToDisk(IRelation item, string folder)
		{
			
			if (item == null)
			{
				return uSyncAction.Fail(Path.GetFileName(folder), typeof(IRelation), "item not set");
			}

			string fileName = GetRelationFilename(item);

			try
			{
				var attempt = uSyncCoreContext.Instance.RelationSerializer.Serialize(item);				
				var filePath = string.Empty;

				if (attempt.Success)
				{		
					filePath = uSyncIOHelper.SavePath(folder, SyncFolder, fileName);
					uSyncIOHelper.SaveNode(attempt.Item, filePath);
				}
				return uSyncActionHelper<XElement>.SetAction(attempt, filePath);

			}
			catch (Exception ex)
			{
				return uSyncAction.Fail("Relation " + fileName, item.GetType(), ChangeType.Export, ex);
			}
		}

		public override SyncAttempt<IRelation> Import(string filePath, bool force = false)
		{

			LogHelper.Debug<IRelation>(">> Import: {0}", () => filePath);

			if (!System.IO.File.Exists(filePath))
				throw new FileNotFoundException(filePath);

			var node = XElement.Load(filePath);

			return uSyncCoreContext.Instance.RelationSerializer.DeSerialize(node, force);
		}

		public void RegisterEvents()
		{
			RelationService.SavingRelation += RelationService_SavingRelation;
			RelationService.SavedRelation += RelationService_SavedRelation;
			RelationService.DeletedRelation += RelationService_DeletedRelation;			
		}

		private void RelationService_SavingRelation(IRelationService sender, Umbraco.Core.Events.SaveEventArgs<IRelation> e)
		{
			if (uSyncEvents.Paused)
			{
				return;
			}

			// Ensure each Relation has a custom Key before it is saved
			foreach (IRelation item in e.SavedEntities)
			{
				XElement comment = XElement.Parse(item.Comment);
				if (comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty).Equals(Guid.Empty))
				{
					comment.SetAttributeValue("RelationKey", Guid.NewGuid());
					item.Comment = comment.ToString();
				}
			}
		}

		private void RelationService_DeletedRelation(IRelationService sender, Umbraco.Core.Events.DeleteEventArgs<IRelation> e)
		{
			if (uSyncEvents.Paused)
				return;

			foreach (var item in e.DeletedEntities)
			{
				string relationName = GetRelationFilename(item);
				LogHelper.Info<RelationHandler>("Delete: Deleting uSync File for item: {0}", () => relationName);
				uSyncIOHelper.ArchiveRelativeFile(SyncFolder, relationName);

				uSyncBackOfficeContext.Instance.Tracker.AddAction(SyncActionType.Delete, relationName, typeof(IRelation)); // S6 keyNameValue may be an issue here since we don't have an Alias
			}
		}

		private void RelationService_SavedRelation(IRelationService sender, Umbraco.Core.Events.SaveEventArgs<IRelation> e)
		{
			if (uSyncEvents.Paused)
				return;

			foreach (var item in e.SavedEntities)
			{
				string relationName = GetRelationFilename(item);
				LogHelper.Info<RelationHandler>("Save: Saving uSync file for item: {0}", () => relationName);
				ExportToDisk(item, uSyncBackOfficeContext.Instance.Configuration.Settings.Folder);

				uSyncBackOfficeContext.Instance.Tracker.RemoveActions(relationName, typeof(IRelation));
			}
		}

		public override uSyncAction ReportItem(string file)
		{
			var node = XElement.Load(file);
			string itemName = GetRelationFilename(node);
			bool update = update = uSyncCoreContext.Instance.RelationSerializer.IsUpdate(node);			
			var action = uSyncActionHelper<IRelation>.ReportAction(update, itemName);
			if (action.Change > ChangeType.NoChange)
				action.Details = ((ISyncChangeDetail)uSyncCoreContext.Instance.RelationSerializer).GetChanges(node);

			return action;
		}

		/// <summary>
		/// Assembles a unique uSync filename for the specified IRelation
		/// </summary>
		/// <param name="relation">The relation.</param>
		/// <returns></returns>
		private string GetRelationFilename(IRelation relation)
		{
			XElement comment = XElement.Parse(relation.Comment);
			Guid relationKey = comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty);
			string fileNameKeyString = relationKey.Equals(Guid.Empty) ? Guid.NewGuid().ToString() + "_TEMP" : relationKey.ToString();
			string fileName = relation.RelationType.Alias + "_" + fileNameKeyString;

			//string fileName = relation.RelationType.Alias + "_" + relation.ParentId + "_" + relation.ChildId;
			//string fileName = relation.RelationType.Alias + "" + relation.Id;
			
			return fileName;
		}

		private string GetRelationFilename(XElement node)
		{
			
			Guid relationKey = node.Element("Comment").Attribute("RelationKey").ValueOrDefault(Guid.Empty);
			string fileNameKeyString = relationKey.Equals(Guid.Empty) ? Guid.NewGuid().ToString() + "_TEMP" : relationKey.ToString();
			
			string fileName = node.Element("Comment").Attribute("PropertyTypeAlias").ValueOrDefault(string.Empty) + "_" +
				fileNameKeyString;
				//node.Element("Id").ValueOrDefault(string.Empty);
				//"Parent: " + node.Element("ParentId").ValueOrDefault(string.Empty) +
				//" Child: " + node.Element("ChildId").ValueOrDefault(string.Empty);

			return "Relation " + fileName;						
		}
	}
}
