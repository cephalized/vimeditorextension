using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

		// Remote calls to a Neovim stuck at a prompt never return, so don't let them hang Unity
		const int NEOVIM_TIMEOUT_MS = 1000;

		private static EditorMode CurrentMode => (EditorMode)EditorPrefs.GetInt(Keys.EDITOR_MODE, (int)EditorMode.Vim);

		public void OnGUI()
		{
			EditorModePopup();
			if (CurrentMode == EditorMode.Neovim)
			{
				NeovimPathTextFields();
			}
			else
			{
				VimPathTextField();
			}
			CodeAssetExtensionTextField();
			ProjectGenerationToggles();
		}

		private void EditorModePopup()
		{
			var currentMode = CurrentMode;
			var newMode = (EditorMode)EditorGUILayout.EnumPopup(UILabels.EDITOR_MODE, currentMode);
			if (newMode != currentMode)
			{
				EditorPrefs.SetInt(Keys.EDITOR_MODE, (int)newMode);
			}
		}

		private void NeovimPathTextFields()
		{
			PathTextField(UILabels.NVIM_PATH, Keys.NVIM_PATH, Defaults.NVIM_PATH);
			PathTextField(UILabels.NEOVIDE_PATH, Keys.NEOVIDE_PATH, Defaults.NEOVIDE_PATH);
		}

		private void PathTextField(string label, string key, string defaultPath)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(label, GUILayout.Width(150));
			EditorGUILayout.EndHorizontal();

			var currentPath = EditorPrefs.GetString(key, defaultPath);
			var newPath = EditorGUILayout.TextField(currentPath);
			if (newPath != currentPath)
			{
				EditorPrefs.SetString(key, newPath);
			}
		}

		private void ProjectGenerationToggles()
		{
			EditorGUILayout.LabelField("Generate .csproj files for:");
			EditorGUI.indentLevel++;
			ProjectGenerationToggle(ProjectGenerationFlag.Embedded, "Embedded packages");
			ProjectGenerationToggle(ProjectGenerationFlag.Local, "Local packages");
			ProjectGenerationToggle(ProjectGenerationFlag.Registry, "Registry packages");
			ProjectGenerationToggle(ProjectGenerationFlag.Git, "Git packages");
			ProjectGenerationToggle(ProjectGenerationFlag.BuiltIn, "Built-in packages");
			ProjectGenerationToggle(ProjectGenerationFlag.LocalTarBall, "Local tarball");
			ProjectGenerationToggle(ProjectGenerationFlag.Unknown, "Packages from unknown sources");
			EditorGUI.indentLevel--;

			if (GUILayout.Button("Regenerate project files"))
			{
				RegenerateVisualStudioSolution();
			}
		}

		private void ProjectGenerationToggle(ProjectGenerationFlag flag, string label)
		{
			var provider = projectGenerator.AssemblyNameProvider;
			var enabled = provider.ProjectGenerationFlag.HasFlag(flag);
			if (EditorGUILayout.Toggle(label, enabled) != enabled)
			{
				provider.ToggleProjectGeneration(flag);
			}
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

		private string GetProjectServerName()
		{
			var projectDir = Directory.GetParent(Application.dataPath)?.Name ?? "Unity";
			var safe = System.Text.RegularExpressions.Regex.Replace(projectDir, @"[^a-zA-Z0-9]", "_");
			return "UNITY_" + safe.ToUpperInvariant();
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

			if (CurrentMode == EditorMode.Neovim)
			{
				return OpenInNeovim(filePath, Math.Max(line, 0), Math.Max(column, 0));
			}

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
				var serverName = GetProjectServerName();
				var process = new Process();
				process.StartInfo.FileName = vimPath;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = false;
				process.StartInfo.Arguments = $"--servername {serverName} --remote-silent +\"call cursor({line},{column})\" {path} \"{filePath}\"";
				process.Start();
				return true;
			}
			catch (System.Exception ex)
			{
				UnityEngine.Debug.LogError($"Failed to open file in Vim: {ex.Message}");
				return false;
			}
		}

		private string GetNeovimSocket()
		{
			// Kept short: macOS limits socket paths to 104 characters
			return $"/tmp/nvim-{GetProjectServerName().ToLowerInvariant()}.sock";
		}

		private bool OpenInNeovim(string filePath, int line, int column)
		{
			var nvimPath = EditorPrefs.GetString(Keys.NVIM_PATH, Defaults.NVIM_PATH);
			var neovidePath = EditorPrefs.GetString(Keys.NEOVIDE_PATH, Defaults.NEOVIDE_PATH);

			if (string.IsNullOrEmpty(nvimPath) || !File.Exists(nvimPath))
			{
				UnityEngine.Debug.LogError($"Neovim executable not found at '{nvimPath}'. Please set the correct path in Unity Preferences.");
				return false;
			}

			if (!File.Exists(filePath))
			{
				UnityEngine.Debug.LogError($"File '{filePath}' does not exist.");
				return false;
			}

			var socket = GetNeovimSocket();
			var cursor = $"call cursor({line},{column})";

			try
			{
				var reply = RunNeovimCommand(nvimPath, $"--server \"{socket}\" --remote-expr 1");
				if (reply == null)
				{
					UnityEngine.Debug.LogWarning("Neovim didn't respond. It may be waiting at a prompt.");
					return false;
				}

				if (reply.Trim() == "1")
				{
					RunNeovimCommand(nvimPath, $"--server \"{socket}\" --remote \"{filePath}\"");
					RunNeovimCommand(nvimPath, $"--server \"{socket}\" --remote-send \"<C-\\><C-N>:{cursor}<CR>\"");
					Process.Start("open", "-a Neovide");
					return true;
				}

				if (string.IsNullOrEmpty(neovidePath) || !File.Exists(neovidePath))
				{
					UnityEngine.Debug.LogError($"Neovide executable not found at '{neovidePath}'. Please set the correct path in Unity Preferences.");
					return false;
				}

				// Left behind if Neovim crashed, and would stop the new instance listening
				File.Delete(socket);

				var projectDir = Directory.GetParent(Application.dataPath).FullName;
				var process = new Process();
				process.StartInfo.FileName = neovidePath;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.Arguments = $"--chdir \"{projectDir}\" -- --listen \"{socket}\" \"+set path+={Application.dataPath}/**\" \"+{cursor}\" \"{filePath}\"";
				process.Start();
				return true;
			}
			catch (System.Exception ex)
			{
				UnityEngine.Debug.LogError($"Failed to open file in Neovim: {ex.Message}");
				return false;
			}
		}

		private static string RunNeovimCommand(string nvimPath, string arguments)
		{
			using (var process = new Process())
			{
				process.StartInfo.FileName = nvimPath;
				process.StartInfo.Arguments = arguments;
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardInput = true;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = true;
				process.Start();

				if (!process.WaitForExit(NEOVIM_TIMEOUT_MS))
				{
					process.Kill();
					return null;
				}

				return process.StandardOutput.ReadToEnd();
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
			(projectGenerator.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
			projectGenerator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(), importedFiles);
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
