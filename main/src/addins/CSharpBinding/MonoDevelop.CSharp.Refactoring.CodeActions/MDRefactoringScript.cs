// 
// MDRefactoringScript.cs
//  
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
// 
// Copyright (c) 2012 Xamarin Inc. (http://xamarin.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Linq;
using ICSharpCode.NRefactory.CSharp.Refactoring;
using MonoDevelop.Ide.Gui;
using ICSharpCode.NRefactory.CSharp;
using Mono.TextEditor;
using MonoDevelop.TypeSystem;
using MonoDevelop.Core;
using System.Collections.Generic;
using ICSharpCode.NRefactory.TypeSystem;
using MonoDevelop.Refactoring.Rename;
using ICSharpCode.NRefactory.CSharp.Resolver;
using System.IO;
using MonoDevelop.CSharp.Formatting;

namespace MonoDevelop.CSharp.Refactoring.CodeActions
{
	public class MDRefactoringScript : DocumentScript
	{
		readonly MDRefactoringContext context;
		readonly Document document;
		readonly IDisposable undoGroup;

		public MDRefactoringScript (MDRefactoringContext context, Document document, CSharpFormattingOptions formattingOptions) : base(document.Editor.Document, formattingOptions)
		{
			this.context = context;
			this.document = document;
			undoGroup  = this.document.Editor.OpenUndoGroup ();
		}

		public override void Select (AstNode node)
		{
			document.Editor.SelectionRange = new TextSegment (GetSegment (node));
		}

		public override void InsertWithCursor (string operation, AstNode node, InsertPosition defaultPosition)
		{
			var editor = document.Editor;
			DocumentLocation loc = document.Editor.Caret.Location;
			var mode = new InsertionCursorEditMode (editor.Parent, CodeGenerationService.GetInsertionPoints (document, document.ParsedDocument.GetInnermostTypeDefinition (loc)));
			var helpWindow = new Mono.TextEditor.PopupWindow.ModeHelpWindow ();
			helpWindow.TransientFor = MonoDevelop.Ide.IdeApp.Workbench.RootWindow;
			helpWindow.TitleText = string.Format (GettextCatalog.GetString ("<b>{0} -- Targeting</b>"), operation);
			helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Key</b>"), GettextCatalog.GetString ("<b>Behavior</b>")));
			helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Up</b>"), GettextCatalog.GetString ("Move to <b>previous</b> target point.")));
			helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Down</b>"), GettextCatalog.GetString ("Move to <b>next</b> target point.")));
			helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Enter</b>"), GettextCatalog.GetString ("<b>Accept</b> target point.")));
			helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Esc</b>"), GettextCatalog.GetString ("<b>Cancel</b> this operation.")));
			mode.HelpWindow = helpWindow;
			
			switch (defaultPosition) {
			case InsertPosition.Start:
				mode.CurIndex = 0;
				break;
			case InsertPosition.End:
				mode.CurIndex = mode.InsertionPoints.Count - 1;
				break;
			case InsertPosition.Before:
				for (int i = 0; i < mode.InsertionPoints.Count; i++) {
					if (mode.InsertionPoints [i].Location < loc)
						mode.CurIndex = i;
				}
				break;
			case InsertPosition.After:
				for (int i = 0; i < mode.InsertionPoints.Count; i++) {
					if (mode.InsertionPoints [i].Location > loc) {
						mode.CurIndex = i;
						break;
					}
				}
				break;
			}
			
			mode.StartMode ();
			mode.Exited += delegate(object s, InsertionCursorEventArgs iCArgs) {
				if (iCArgs.Success) {
					var output = OutputNode (CodeGenerationService.CalculateBodyIndentLevel (document.ParsedDocument.GetInnermostTypeDefinition (loc)), node);
					output.RegisterTrackedSegments (this, document.Editor.LocationToOffset (iCArgs.InsertionPoint.Location));
					iCArgs.InsertionPoint.Insert (editor, output.Text);
				}
			};
		}

		public override void InsertWithCursor (string operation, AstNode node, ITypeDefinition parentType)
		{
			var part = parentType.Parts.FirstOrDefault ();
			if (part == null)
				return;

			var loadedDocument = Ide.IdeApp.Workbench.OpenDocument (part.Region.FileName);

			loadedDocument.RunWhenLoaded (delegate {
				var editor = loadedDocument.Editor;
				var loc = part.Region.Begin;
				var parsedDocument = loadedDocument.UpdateParseDocument ();
				var mode = new InsertionCursorEditMode (editor.Parent, CodeGenerationService.GetInsertionPoints (loadedDocument, parsedDocument.GetInnermostTypeDefinition (loc)));
				var helpWindow = new Mono.TextEditor.PopupWindow.ModeHelpWindow ();
				helpWindow.TransientFor = MonoDevelop.Ide.IdeApp.Workbench.RootWindow;
				helpWindow.TitleText = string.Format (GettextCatalog.GetString ("<b>{0} -- Targeting</b>"), operation);
				helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Key</b>"), GettextCatalog.GetString ("<b>Behavior</b>")));
				helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Up</b>"), GettextCatalog.GetString ("Move to <b>previous</b> target point.")));
				helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Down</b>"), GettextCatalog.GetString ("Move to <b>next</b> target point.")));
				helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Enter</b>"), GettextCatalog.GetString ("<b>Accept</b> target point.")));
				helpWindow.Items.Add (new KeyValuePair<string, string> (GettextCatalog.GetString ("<b>Esc</b>"), GettextCatalog.GetString ("<b>Cancel</b> this operation.")));
				mode.HelpWindow = helpWindow;
				
				mode.CurIndex = 0;

				mode.StartMode ();
				mode.Exited += delegate(object s, InsertionCursorEventArgs iCArgs) {
					if (iCArgs.Success) {
						var output = OutputNode (CodeGenerationService.CalculateBodyIndentLevel (parsedDocument.GetInnermostTypeDefinition (loc)), node);
						output.RegisterTrackedSegments (this, loadedDocument.Editor.LocationToOffset (iCArgs.InsertionPoint.Location));
						iCArgs.InsertionPoint.Insert (editor, output.Text);
					}
				};
			});
		}

		public override void Link (params AstNode[] nodes)
		{
			var segments = new List<TextSegment> (nodes.Select (node => new TextSegment (GetSegment (node))).OrderBy (s => s.Offset));

			var link = new TextLink ("name");
			segments.ForEach (s => link.AddLink (s));
			var links = new List<TextLink> ();
			links.Add (link);
			var tle = new TextLinkEditMode (document.Editor.Parent, 0, links);
			tle.SetCaretPosition = false;
			if (tle.ShouldStartTextLinkMode) {
				document.Editor.Caret.Offset = segments [0].EndOffset;
				tle.OldMode = document.Editor.CurrentMode;
				tle.StartMode ();
				document.Editor.CurrentMode = tle;
			}
		}
		public override void Replace (int offset, int length, string newText)
		{
			var editor = document.Editor;
			var caretOffset = editor.Caret.Offset;
			var version = editor.Document.Version;
			base.Replace (offset, length, newText);
			editor.Caret.Offset = version.MoveOffsetTo (editor.Document.Version, caretOffset);
		}
		public override void Dispose ()
		{
			undoGroup.Dispose ();
			base.Dispose ();
		}

		public override void Rename (IEntity entity, string name)
		{
			RenameRefactoring.Rename (entity, name);
		}

		public override void Rename (IVariable variable, string name)
		{
			RenameRefactoring.RenameVariable (variable, name);
		}

		public override void RenameTypeParameter (IType typeParameter, string name = null)
		{
			RenameRefactoring.RenameTypeParameter ((ITypeParameter)typeParameter, name);
		}

		public override void CreateNewType (TypeDeclaration newType, NewTypeContext ntctx)
		{
			if (newType == null)
				throw new System.ArgumentNullException ("newType");
			var correctFileName = MoveTypeToFile.GetCorrectFileName (context, newType);
			
			var content = context.Document.Editor.Text;
			
			var types = new List<TypeDeclaration> (context.Unit.GetTypes ());
			types.Sort ((x, y) => y.StartLocation.CompareTo (x.StartLocation));

			foreach (var removeType in types) {
				var start = context.GetOffset (removeType.StartLocation);
				var end = context.GetOffset (removeType.EndLocation);
				content = content.Remove (start, end - start);
			}
			
			var insertLocation = context.GetOffset (types.First ().StartLocation);
			var formattingPolicy = this.document.GetFormattingPolicy ();
			content = content.Substring (0, insertLocation) + newType.GetText (formattingPolicy.CreateOptions ()) + content.Substring (insertLocation);

			var formatter = new CSharpFormatter ();
			content = formatter.FormatText (formattingPolicy, null, CSharpFormatter.MimeType, content, 0, content.Length);

			File.WriteAllText (correctFileName, content);
			document.Project.AddFile (correctFileName);
			MonoDevelop.Ide.IdeApp.ProjectOperations.Save (document.Project);
			MonoDevelop.Ide.IdeApp.Workbench.OpenDocument (correctFileName);
		}

	}
}

