﻿using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using VVVV.Core;
using VVVV.Core.Logging;
using VVVV.Core.Model;
using VVVV.Core.Model.CS;
using VVVV.Core.View;
using VVVV.Core.View.Table;
using VVVV.HDE.CodeEditor.ErrorView;
using VVVV.PluginInterfaces.V2;
using Dom = ICSharpCode.SharpDevelop.Dom;

namespace VVVV.HDE.CodeEditor
{
	/// <summary>
	/// Sharpdevelop texteditor needs a Form as one of its parent controls
	/// in order to work properly.
	/// </summary>
	public partial class CodeEditorForm : Form, IParseInfoProvider
	{
		private Dictionary<ITextDocument, TabPage> FOpenedDocuments;
		private IHDEHost FHDEHost;
		private INodeSelectionListener FNodeSelectionListener;
		private Dom.ProjectContentRegistry FPCRegistry;
		private Dictionary<IProject, Dom.DefaultProjectContent> FProjects;
		private Dictionary<ITextDocument, Dom.ParseInformation> FParseInfos;
		private BackgroundParser FBGParser;
		private ISolution FSolution;
		
		public CodeEditorForm(IHDEHost host, ISolution solution, ILogger logger)
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			var resources = new ComponentResourceManager(typeof(CodeEditorForm));
			FImageList.TransparentColor = System.Drawing.Color.Transparent;
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.Class"));
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.Method"));
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.Property"));
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.Field"));
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.Enum"));
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.NameSpace"));
			FImageList.Images.Add((System.Drawing.Bitmap) resources.GetObject("Icons.16x16.Event"));
			
			FSolution = solution;
			FNodeSelectionListener = new NodeSelectionListener(this);
			FProjects = new Dictionary<IProject, Dom.DefaultProjectContent>();
			FParseInfos = new Dictionary<ITextDocument, Dom.ParseInformation>();
			
			FHDEHost = host;
			FHDEHost.AddListener(FNodeSelectionListener);
			
			FOpenedDocuments = new Dictionary<ITextDocument, TabPage>();
			
			var registry = new MappingRegistry();
			registry.RegisterDefaultMapping<IEnumerable<IColumn>, ErrorCollectionColumnProvider>();
			registry.RegisterMapping<CompilerError, IEnumerable<ICell>, ErrorCellProvider>();
			registry.RegisterMapping<IEditableIDList<IReference>, ReferencesViewProvider>();
			
			FErrorTableViewer.Registry = registry;
			FErrorTableViewer.Input = Empty.Enumerable;
			// Start to parse the solution.
			FPCRegistry = new Dom.ProjectContentRegistry(); // Default .NET 2.0 registry
			// Persistence lets SharpDevelop.Dom create a cache file on disk so that
			// future starts are faster.
			// It also caches XML documentation files in an on-disk hash table, thus
			// reducing memory usage.
			FPCRegistry.ActivatePersistence(Path.Combine(Path.GetTempPath(), "VVVVCodeEditor"));
			
			// Create the background assembly parser
			FBGParser = new BackgroundParser(FPCRegistry, this, FStatusLabel);
			
			FSolution.Projects.Added += Project_Added;
			FSolution.Projects.Removed += Project_Removed;

			foreach (var project in FSolution.Projects)
				SetupProject(project);
		}
		
		/// <summary>
		/// Opens the specified ITextDocument in a new editor or if already opened brings editor to top.
		/// </summary>
		/// <param name="doc">The ITextDocument to open.</param>
		public void Open(ITextDocument doc)
		{
			if (!FOpenedDocuments.ContainsKey(doc))
			{
				var editor = new CodeEditor(this, doc, FImageList);
				var tabPage = new TabPage(GetTabPageName(doc));
				
				FTabControl.SuspendLayout();
				
				editor.Dock = DockStyle.Fill;
				FTabControl.Controls.Add(tabPage);
				tabPage.Controls.Add(editor);
				
				FTabControl.ResumeLayout();
				
				FOpenedDocuments[doc] = tabPage;
				doc.ContentChanged += DocumentContentChangedCB;
				doc.Saved += DocumentSavedCB;
			}
			FTabControl.SelectTab(FOpenedDocuments[doc]);
		}
		
		public void Close(ITextDocument doc)
		{
			if (FOpenedDocuments.ContainsKey(doc))
			{
				var tabPage = FOpenedDocuments[doc];
				FTabControl.Controls.Remove(tabPage);
				FOpenedDocuments.Remove(doc);
			}
		}

		protected string GetTabPageName(ITextDocument document)
		{
			var name = document.Mapper.Map<INamed>().Name;
			
			if (document.IsDirty)
				return name + "*";
			else
				return name;
		}
		
		void Project_CompileCompleted(IProject project, CompilerResults results)
		{
			if (results.Errors.Count > 0)
			{
				FErrorTableViewer.Input = results.Errors;
				// TODO: Find better way to calculate splitter distance
				FSplitContainer.SplitterDistance = FSplitContainer.Height - (FErrorTableViewer.RowCount + 2) * (FErrorTableViewer.RowHeight + 2);
				FSplitContainer.Panel2Collapsed = false;
			}
			else
			{
				FSplitContainer.Panel2Collapsed = true;
			}
		}
		
		void DocumentContentChangedCB(ITextDocument document, string content)
		{
			FOpenedDocuments[document].Text = GetTabPageName(document);
		}
		
		void DocumentSavedCB(object sender, EventArgs args)
		{
			var document = sender as ITextDocument;
			FOpenedDocuments[document].Text = GetTabPageName(document);
		}
		
		void Document_Removed(IViewableCollection<IDocument> collection, IDocument item)
		{
			if (item is ITextDocument)
			{
				var textDoc = item as ITextDocument;
				Close(textDoc);
			}
		}
		
		protected void SetupProject(IProject project)
		{
			project.CompileCompleted += Project_CompileCompleted;
			project.Documents.Removed += Document_Removed;
			
			if (project is CSProject)
			{
				project.References.Added += References_Changed;
				project.References.Removed += References_Changed;
			}
			
			FBGParser.Parse(project);
		}

		protected void TearDownProject(IProject project)
		{
			project.CompileCompleted -= Project_CompileCompleted;
			
			foreach (var doc in project.Documents)
			{
				var textDoc = doc as ITextDocument;
				if (textDoc != null)
					Close(textDoc);
			}
			
			if (project is CSProject)
			{
				project.References.Added += References_Changed;
				project.References.Removed += References_Changed;
			}
		}

		void Project_Added(IViewableCollection<IProject> collection, IProject project)
		{
			SetupProject(project);
		}
		
		void Project_Removed(IViewableCollection<IProject> collection, IProject project)
		{
			TearDownProject(project);
		}

		void References_Changed(IViewableCollection<IReference> collection, IReference item)
		{
			// Tell the background parser to reparse this project
			FBGParser.Parse(item.Project);
		}
		
		#region IParseInfoProvider Members
		
		/// <summary>
		/// Returns the project content (contains parse information) for an IProject.
		/// </summary>
		public Dom.DefaultProjectContent GetProjectContent(IProject project)
		{
			if (!FProjects.ContainsKey(project))
			{
				var projectContent = new Dom.DefaultProjectContent();
				FProjects.Add(project, projectContent);
			}
			
			return FProjects[project];
		}
		
		/// <summary>
		/// Returns parse information for an ITextDocument.
		/// </summary>
		public Dom.ParseInformation GetParseInfo(ITextDocument doc)
		{
			if (!FParseInfos.ContainsKey(doc))
			{
				var parseInfo = new Dom.ParseInformation();
				FParseInfos.Add(doc, parseInfo);
			}
			
			return FParseInfos[doc];
		}
		
		#endregion
		
		#region IDisposable
		private bool FDisposed = false;
		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be disposed.
		protected override void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if(!FDisposed)
			{
				if(disposing)
				{
					// Dispose managed resources.
					if (FProjects != null)
					{
						foreach (var project in FProjects.Keys)
						{
							project.References.Added -= References_Changed;
							project.References.Removed -= References_Changed;
							project.Documents.Removed -= Document_Removed;
						}
						FProjects.Clear();
					}
					
					if (FBGParser != null)
						FBGParser.CancelAsync();
					
					if (FHDEHost != null)
					{
						if (FSolution != null)
							FSolution.Projects.Removed -= Project_Removed;
						
						FHDEHost.RemoveListener(FNodeSelectionListener);
					}
					FNodeSelectionListener = null;
					
					foreach (var entry in FOpenedDocuments)
					{
						entry.Key.ContentChanged -= DocumentContentChangedCB;
						entry.Key.Saved -= DocumentSavedCB;
					}
					
					FOpenedDocuments = null;
					
					if (components != null) {
						components.Dispose();
					}
					components = null;
				}
				// Release unmanaged resources. If disposing is false,
				// only the following code is executed.
			}
			FDisposed = true;
			
			base.Dispose(disposing);
		}
		#endregion IDisposable
	}
}