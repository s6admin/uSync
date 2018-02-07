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
			IEnumerable<IRelation> allRelations = _relationService.GetAllRelations();
			IEnumerable<IRelation> keylessRelations = allRelations.Where(x => x.Comment.Length == 0 || 
			(XElement.Parse(x.Comment).Attribute("RelationKey").ValueOrDefault(Guid.Empty)).Equals(Guid.Empty));

			// Ensure all Relations have a custom Key before they are exported
			uSyncEvents.Paused = true;
			foreach(IRelation item in keylessRelations)
			{
				if (!CreateRelationKey(item).Equals(Guid.Empty))
				{
					_relationService.Save(item); // This will naturally Export each item after it is saved, which is why uSyncEvents are temporarily paused during this loop
				} else
				{
					// Could not create a key on this Relation (ie. bad Comment data)
					actions.Add(uSyncAction.Fail("Relation " + GetRelationLabel(item), typeof(IRelation), ChangeType.Export, "Could not create a Relation Key for item " + item.Id + ". Relation will not be exported."));

					// Exclude invalid key Relation from total list so it is not processed during the export
					allRelations = allRelations.Except(item.AsEnumerableOfOne());
				}				
			}
			uSyncEvents.Paused = false;

            foreach (var item in allRelations)
			{
				if (item != null)
				{
					// If Relation being exported already has a custom Key, process it in the actions queue as usual
					if(HasRelationKey(item))
					{
						actions.Add(ExportToDisk(item, folder));
					} else
					{
						// TODO or throw?						
						actions.Add(uSyncAction.Fail("Relation " + GetRelationLabel(item), typeof(IRelation), ChangeType.Export, "Relation does not have a custom RelationKey and will not be exported."));
					}					
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
				// Ensure Relation has a custom Relation Key before it is Serialized, otherwise it is skipped and reported as a failure
				if (HasRelationKey(item)) { 					
					var attempt = uSyncCoreContext.Instance.RelationSerializer.Serialize(item);				
					var filePath = string.Empty;

					if (attempt.Success)
					{		
						filePath = uSyncIOHelper.SavePath(folder, SyncFolder, fileName);
						uSyncIOHelper.SaveNode(attempt.Item, filePath);
					}
					return uSyncActionHelper<XElement>.SetAction(attempt, filePath);
				} else
				{
					return uSyncAction.Fail("Relation " + GetRelationLabel(item), typeof(IRelation), ChangeType.Export, "Relation does not have a custom RelationKey.");
				}
			}
			catch (Exception ex)
			{
				return uSyncAction.Fail("Relation " + GetRelationLabel(item), item.GetType(), ChangeType.Export, ex);
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
				if (!HasRelationKey(item))
				{
					if (CreateRelationKey(item).Equals(Guid.Empty))
					{
						// Could not create custom Relation key on item. Report, but allow Save to continue
						LogHelper.Warn<RelationHandler>("Could not create Relation Key for item " + item.Id + " during Saving event.");
					}
				}			
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

		/// <summary>
		/// Creates a RelationKey for the specified Relation. If a valid RelationKey already exists a new Key will not be generated.		
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="saveIfNewKeyIsCreated">if set to <c>true</c> [save if key is created].</param>
		/// <returns></returns>
		private Guid CreateRelationKey(IRelation item)
		{
			Guid key = Guid.Empty;
			
			// Some Relation Types don't generate any Comment data so we may have to add some boilerplate before applying our custom Key
			XElement comment = item.Comment.Length == 0 ? new XElement("RelationMapping") : XElement.Parse(item.Comment);
			
			key = comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty);

			if (key.Equals(Guid.Empty))
			{
				key = Guid.NewGuid();
				comment.SetAttributeValue("RelationKey", key);
				item.Comment = comment.ToString();				
			}

			return key;
		}

		private bool HasRelationKey(IRelation item)
		{
			XElement comment = item.Comment.Length > 0 ? XElement.Parse(item.Comment) : null;
			return comment != null && !comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty).Equals(Guid.Empty);
        }

		public override uSyncAction ReportItem(string file)
		{
			var node = XElement.Load(file);

			string itemName = "Relation " + file.Substring(file.LastIndexOf("\\") + 1);			            
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
			XElement comment = relation.Comment.Length > 0 ? XElement.Parse(relation.Comment) : null;
			Guid relationKey = comment != null ? comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty) : Guid.Empty;
			string fileNameKeyString = relationKey.Equals(Guid.Empty) ? relation.Id.ToString() : relationKey.ToString();
			string fileName = relation.RelationType.Alias + "_" + fileNameKeyString;
						
			return fileName;
		}

		private string GetRelationLabel(IRelation relation)
		{
			XElement comment = relation.Comment.Length > 0 ? XElement.Parse(relation.Comment) : null;
			Guid relationKey = comment != null ? comment.Attribute("RelationKey").ValueOrDefault(Guid.Empty) : Guid.Empty;

			string label = "Relation ";
			label += !relationKey.Equals(Guid.Empty) ? relationKey.ToString() : relation.Id.ToString();

			return label;
		}
	}
}
