namespace Vim.Editor
{
	public static class VimEditorDefines
	{
		public enum EditorMode
		{
			Vim,
			Neovim
		}

		public static class UILabels
		{
			public static string EDITOR_NAME = "Vim";

			public static string EDITOR_MODE = "Editor";
			public static string VIM_PATH = "Vim Executable Path";
			public static string NVIM_PATH = "Neovim Executable Path";
			public static string NEOVIDE_PATH = "Neovide Executable Path";
			public static string FILENAME_EXTENSIONS = "Code Filename Extensions";
		}

		public static class Keys
		{
			public static string EDITOR_MODE = "VimEditorMode";
			public static string VIM_PATH = "VimExecutablePath";
			public static string NVIM_PATH = "VimNeovimExecutablePath";
			public static string NEOVIDE_PATH = "VimNeovideExecutablePath";
			public static string FILENAME_EXTENSIONS = "VimFilenameExtensions";
		}

		public static class Defaults
		{
			public static string VIM_PATH = "/opt/homebrew/bin/mvim";
			public static string NVIM_PATH = "/opt/homebrew/bin/nvim";
			public static string NEOVIDE_PATH = "/opt/homebrew/bin/neovide";
			public static string FILENAME_EXTENSIONS = ".cs,.shader,.h,.m,.c,.cpp,.txt,.md,.json";
		}
	}
}
