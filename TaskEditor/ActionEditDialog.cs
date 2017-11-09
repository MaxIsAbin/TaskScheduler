﻿using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace Microsoft.Win32.TaskScheduler
{
	/// <summary>Defines the type of actions available to a user interface element.</summary>
	[Flags]
	public enum AvailableActions
	{
		/// <summary>This action fires a handler.</summary>
		ComHandler = 0x2,

		/// <summary>
		/// This action performs a command-line operation. For example, the action can run a script, launch an executable, or, if the name of a document is
		/// provided, find its associated application and launch the application with the document.
		/// </summary>
		Execute = 0x1,

		/// <summary>This action sends and e-mail.</summary>
		SendEmail = 0x4,

		/// <summary>This action shows a message box.</summary>
		ShowMessage = 0x8,

		/// <summary>All actions are available.</summary>
		AllActions = 0xF
	}

	/// <summary>An editor that handles all Task actions.</summary>
	[ToolboxItem(true), ToolboxItemFilter("System.Windows.Forms.Control.TopLevel"), Description("Dialog allowing the editing of a task action.")]
	[Designer("System.ComponentModel.Design.ComponentDesigner, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
	[DesignTimeVisible(true)]
	[System.Drawing.ToolboxBitmap(typeof(TaskEditDialog), "TaskDialog")]
	public partial class ActionEditDialog :
#if DEBUG
		Form
#else
		DialogBase
#endif
	{
		private Action action;
		private bool allowRun;
		private AvailableActions availableActions = AvailableActions.AllActions;
		private UIComponents.IActionHandler curHandler;
		private bool isV2 = true;
		private bool onAssignment;
		private bool useUnifiedSchedulingEngine;

		/// <summary>Initializes a new instance of the <see cref="ActionEditDialog"/> class.</summary>
		public ActionEditDialog()
		{
			InitializeComponent();
			ResetCombo();
		}

		/// <summary>Initializes a new instance of the <see cref="ActionEditDialog"/> class with the provided action.</summary>
		/// <param name="action">The action.</param>
		public ActionEditDialog(Action action) : this()
		{
			Action = action;
		}

		/// <summary>Gets or sets the action.</summary>
		/// <value>The action.</value>
		[DefaultValue(null), Browsable(false)]
		public Action Action
		{
			get => action;
			set
			{
				onAssignment = true;
				action = value;
				actionsCombo.SelectedIndex = actionsCombo.Items.IndexOf((long)(action?.ActionType ?? TaskActionType.Execute));
				actionIdText.Text = action?.Id;
				curHandler.Action = action;
				onAssignment = false;
			}
		}

		/// <summary>Gets or sets a value indicating whether to show a button allowing the action to be run in real time.</summary>
		/// <value><c>true</c> if allow run; otherwise, <c>false</c>.</value>
		[DefaultValue(false), Category("Behavior")]
		public bool AllowRun
		{
			get => allowRun;
			set { allowRun = value; runActionBtn.Visible = value; }
		}

		/// <summary>Gets or sets the available actions.</summary>
		/// <value>The available actions.</value>
		[DefaultValue(typeof(AvailableActions), nameof(AvailableActions.AllActions)), Category("Appearance")]
		public AvailableActions AvailableActions
		{
			get => availableActions;
			set
			{
				if (availableActions == value) return;
				availableActions = value;
				ResetCombo();
			}
		}

		/// <summary>Gets or sets the prompt text at the top of the dialog.</summary>
		/// <value>The text to use as a prompt.</value>
		[DefaultValue("You must specify what action this task will perform."), Category("Appearance")]
		public string Prompt
		{
			get => promptLabel.Text;
			set => promptLabel.Text = value;
		}

		/// <summary>Gets or sets a value indicating whether this editor only supports V1 actions.</summary>
		/// <value><c>true</c> if supports V1 only; otherwise, <c>false</c>.</value>
		[DefaultValue(false), Category("Behavior")]
		public bool SupportV1Only
		{
			get => !isV2;
			set
			{
				if (value != isV2) return;
				isV2 = !value;
				ResetCombo();
			}
		}

		/// <summary>Gets or sets a value indicating whether dialog should restrict items to those available when using the Unified Scheduling Engine.</summary>
		/// <value><c>true</c> if using the Unified Scheduling Engine; otherwise, <c>false</c>.</value>
		[DefaultValue(false), Category("Behavior")]
		public bool UseUnifiedSchedulingEngine
		{
			get => useUnifiedSchedulingEngine;
			set
			{
				if (!isV2 && value)
					throw new NotSupportedException("Version 1.0 of the Task Scheduler library cannot use the Unified Scheduling Engine.");
				if (value == useUnifiedSchedulingEngine) return;
				useUnifiedSchedulingEngine = value;
				ResetCombo();
			}
		}

		private void actionIdText_TextChanged(object sender, EventArgs e)
		{
			if (!onAssignment)
				action.Id = actionIdText.TextLength == 0 ? null : actionIdText.Text;
		}

		private void actionsCombo_SelectedIndexChanged(object sender, EventArgs e)
		{
			switch (GetComboItemActionType(actionsCombo.SelectedItem))
			{
				case TaskActionType.ComHandler:
					settingsTabs.SelectedTab = comTab;
					curHandler = comHandlerActionUI1;
					break;

				case TaskActionType.SendEmail:
					settingsTabs.SelectedTab = emailTab;
					curHandler = emailActionUI1;
					break;

				case TaskActionType.ShowMessage:
					settingsTabs.SelectedTab = messageTab;
					curHandler = showMessageActionUI1;
					break;

				case TaskActionType.Execute:
					settingsTabs.SelectedTab = execTab;
					curHandler = execActionUI1;
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
			DetermineIfCanValidate();
		}

		private void cancelBtn_Click(object sender, EventArgs e) { Close(); }

		private void DetermineIfCanValidate() { okBtn.Enabled = runActionBtn.Enabled = curHandler.CanValidate; }

		private TaskActionType GetComboItemActionType(object comboItem) => (TaskActionType)Convert.ToInt32(((ListControlItem)comboItem).Value);

		private void keyField_TextChanged(object sender, EventArgs e) { DetermineIfCanValidate(); }

		private void okBtn_Click(object sender, EventArgs e)
		{
			if (!curHandler.ValidateFields()) return;
			UpdateAction();
			Close();
		}

		private void ResetCombo()
		{
			var curType = actionsCombo.SelectedIndex == -1 ? -1 : (int)GetComboItemActionType(actionsCombo.SelectedItem);
			actionsCombo.BeginUpdate();
			actionsCombo.Items.Clear();
			ComboBoxExtension.InitializeFromEnum(actionsCombo.Items, typeof(TaskActionType), EditorProperties.Resources.ResourceManager, "ActionType", out _);
			for (var i = actionsCombo.Items.Count - 1; i >= 0; i--)
			{
				bool remove;
				switch (GetComboItemActionType(actionsCombo.Items[i]))
				{
					case TaskActionType.ComHandler:
						remove = !isV2 || (availableActions & AvailableActions.ComHandler) == 0;
						break;

					case TaskActionType.Execute:
						remove = (availableActions & AvailableActions.Execute) == 0;
						break;

					case TaskActionType.SendEmail:
						remove = !isV2 || useUnifiedSchedulingEngine || (availableActions & AvailableActions.SendEmail) == 0;
						break;

					case TaskActionType.ShowMessage:
						remove = !isV2 || useUnifiedSchedulingEngine || (availableActions & AvailableActions.ShowMessage) == 0;
						break;

					default:
						throw new ArgumentOutOfRangeException();
				}
				if (remove) actionsCombo.Items.RemoveAt(i);
			}
			if (actionsCombo.Items.Count == 0) throw new InvalidOperationException("No actions are available to display given the current settings.");
			if (curType > -1)
				curType = actionsCombo.Items.IndexOf((long)curType);
			if (curType == -1) curType = 0;
			actionsCombo.SelectedIndex = curType;
			actionsCombo.EndUpdate();
			runActionBtn.Visible = AllowRun;
		}

		private void runActionBtn_Click(object sender, EventArgs e) { curHandler.Run(); }

		private void UpdateAction()
		{
			action = curHandler.Action;
			actionIdText_TextChanged(null, EventArgs.Empty);
		}
	}
}