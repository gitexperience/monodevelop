﻿using System;
using System.Xml;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Projects;

namespace MonoDevelop.Ide.GettingStarted
{
	public class GettingStartedProjectExtension : ProjectExtension
	{
		GettingStartedNode treeDataObject;

		public GettingStartedNode ProjectPadNode {
			get {
				if (treeDataObject == null) {
					treeDataObject = new GettingStartedNode (this);
				}
				return treeDataObject;
			}
		}

		protected override void OnInitializeFromTemplate (ProjectCreateInformation projectCreateInfo, XmlElement template)
		{
			base.OnInitializeFromTemplate (projectCreateInfo, template);
			if (template.GetAttribute ("HideGettingStarted")?.ToLower () == "true")
				Project.UserProperties.SetValue ("HideGettingStarted", true);
		}
	}
}

