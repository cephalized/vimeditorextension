using System;
using System.Diagnostics;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

using static Vim.Editor.VimEditorDefines;

namespace Vim.Editor
{
	public class VimExternalCodeEditor : IExternalCodeEditor
	{
		IGenerator projectGenerator;

		public VimExternalCodeEditor()
		{
			projectGenerator = new ProjectGeneration(Directory.GetParent(Application.dataPath).FullName);
		}

		public void Initialize(string editorPath) { }

		public void OnGUI()
		{
			VimPathTextField();
			CodeAssetExtensionTextField();
		}

		private void VimPathTextField()
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(UILabels.VIM_PATH, GUILayout.Width(150));
			EditorGUILayout.EndHorizontal();

			var currentPath = EditorPrefs.GetString(Keys.VIM_PATH, Defaults.VIM_PATH);
			var newPath = EditorGUILayout.TextField(currentPath);
			if (newPath != currentPath)
			{
				EditorPrefs.SetString(Keys.VIM_PATH, newPath);
			}
		}

		private void CodeAssetExtensionTextField()
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(UILabels.FILENAME_EXTENSIONS, GUILayout.Width(150));
			EditorGUILayout.EndHorizontal();

			var currentPath = EditorPrefs.GetString(Keys.FILENAME_EXTENSIONS, Defaults.FILENAME_EXTENSIONS);
			var newPath = EditorGUILayout.TextField(currentPath);
			if (newPath != currentPath)
			{
				EditorPrefs.SetString(Keys.FILENAME_EXTENSIONS, newPath);
			}
		}

		public bool OpenProject(string filePath, int line, int column)
		{
			var extensions = EditorPrefs.GetString(Keys.FILENAME_EXTENSIONS, Defaults.FILENAME_EXTENSIONS)
				.Split(',')
				.Select(ext => ext.Trim())
				.ToArray();
			
   			if (extensions.Length > 0)
			{
				var supportedExtension = extensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
				if (!supportedExtension) return false;
			}
   S
			var vimPath = EditorPrefs.GetString(Keys.VIM_PATH, Defaults.VIM_PATH);

			if (string.IsNullOrEmpty(vimPath) || !File.Exists(vimPath))
			{
				UnityEngine.Debug.LogError($"Vim executable not found at '{vimPath}'. Please set the correct path in Unity Preferences.");
				return false;
			}

			if (!File.Exists(filePath))
			{
				UnityEngine.Debug.LogError($"File '{filePath}' does not exist.");
				return false;
			}

			var path = $"+\"set path+={Application.dataPath}/**\"";
			line = Math.Max(line, 0);
			column = Math.Max(column, 0);

			try
			{
				var process = new Process();
				process.StartInfo.FileName = vimPath;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = false;
				process.StartInfo.Arguments = $"--servername Unity --remote-silent +\"call cursor({line},{column})\" {path} \"{filePath}\"";
				process.Start();
				return true;
			}
			catch (System.Exception ex)
			{
				UnityEngine.Debug.LogError($"Failed to open file in Vim: {ex.Message}");
				return false;
			}
		}

		private void RegenerateVisualStudioSolution()
		{
			(projectGenerator.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
			AssetDatabase.Refresh();
			projectGenerator.Sync();
		}

		public void SyncAll()
		{
			RegenerateVisualStudioSolution();
		}

		public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
		{
			var extensions = EditorPrefs.GetString(Keys.FILENAME_EXTENSIONS, Defaults.FILENAME_EXTENSIONS).Split(',');
			if (extensions == null || extensions.Length == 0) return;

			var syncNeeded = false;

			foreach (var extension in extensions)
			{
				foreach (var addedFile in addedFiles)
				{
					if (addedFile.EndsWith(extension)) syncNeeded = true;
				}

				foreach (var movedFile in movedFiles)
				{
					if (movedFile.EndsWith(extension)) syncNeeded = true;
				}
			}

			if (syncNeeded)
			{
				RegenerateVisualStudioSolution();
				UnityEngine.Debug.Log($"[VimExternalEditor] Regenerated Visual Studio solution for {addedFiles.Length} new files, {movedFiles.Length} moved files.");
			}
		}

		public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
		{
			if (File.Exists(editorPath))
			{
				installation = new CodeEditor.Installation
				{
					Name = UILabels.EDITOR_NAME,
					Path = editorPath
				};
				return true;
			}

			installation = default;
			return false;
		}

		public CodeEditor.Installation[] Installations
		{
			get
			{
				return new CodeEditor.Installation[]
				{
				new CodeEditor.Installation
				{
					Name = UILabels.EDITOR_NAME,
					Path = EditorPrefs.GetString(Keys.VIM_PATH, Defaults.VIM_PATH)
				}
				};
			}
		}
	}

	[InitializeOnLoad]
	public class VimExternalCodeEditorInitializer
	{
		static VimExternalCodeEditorInitializer()
		{
			CodeEditor.Register(new VimExternalCodeEditor());
		}
	}
}
