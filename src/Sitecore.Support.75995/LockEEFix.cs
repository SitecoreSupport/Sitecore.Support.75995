using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Locking;
using Sitecore.Diagnostics;
using System;
using Sitecore.Pipelines.Save;
using Sitecore.Workflows;

namespace Sitecore.Support.Pipelines.Save
{
    public class Lock : Sitecore.Pipelines.Save.Lock
    {
        // Sitecore.Pipelines.Save.Lock
        /// <summary>
        /// Runs the processor.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        public new void Process(SaveArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!args.PolicyBasedLocking && !Settings.AutomaticLockOnSave)
            {
                return;
            }
            SaveArgs.SaveItem[] items = args.Items;
            for (int i = 0; i < items.Length; i++)
            {
                SaveArgs.SaveItem saveItem = items[i];
                Item item = Client.ContentDatabase.Items[saveItem.ID, saveItem.Language, saveItem.Version];
                if (item != null)
                {
                    string name = item.Name;
                    if (!item.Locking.HasLock())
                    {
                        try
                        {
                            if (item.Locking.IsLocked())
                            {
                                if (!Context.User.IsAdministrator)
                                {
                                    item = null;
                                }
                            }
                            else if (!args.PolicyBasedLocking || !Context.User.IsInRole("sitecore\\Sitecore Minimal Page Editor"))
                            {                               
                                if (!Sitecore.Context.PageMode.IsExperienceEditor && Sitecore.Context.RawUrl.Contains("ExperienceEditor.Save.CallServerSavePipeline") && !Context.Workflow.Enabled)
                                {
                                    IWorkflowProvider workflowProvider = item.Database.WorkflowProvider;
                                    if (workflowProvider != null)
                                    {
                                        var workflow = workflowProvider.GetWorkflow(item);
                                        if (workflow != null && !workflow.IsApproved(item, null))
                                        {
                                            item.Locking.Lock();                                        
                                        }
                                    }
                                }
                                if (!item.Locking.IsLocked())
                                {
                                    item = Context.Workflow.StartEditing(item);                                    
                                }
                                if (Settings.AutomaticLockOnSave && !item.Locking.IsLocked())
                                {
                                    item.Locking.Lock();
                                }
                            }
                            else
                            {
                                using (new LockingDisabler())
                                {
                                    item = Context.Workflow.StartEditing(item);
                                }
                            }
                            if (item == null)
                            {
                                args.Error = "Could not lock the item \"" + name + "\"";
                                args.AbortPipeline();
                            }
                            else
                            {
                                SaveArgs.SaveField field = this.GetField(saveItem, FieldIDs.Lock);
                                if (field != null)
                                {
                                    string text = item[FieldIDs.Lock];
                                    if (string.Compare(field.Value, text, System.StringComparison.InvariantCultureIgnoreCase) != 0)
                                    {
                                        field.Value = text;
                                    }
                                }
                                saveItem.Version = item.Version;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            args.Error = ex.Message;
                            args.AbortPipeline();
                        }
                    }
                }
            }
        }
    }
}