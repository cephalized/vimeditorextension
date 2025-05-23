namespace Vim.Editor
{
	public static class VimEditorDefines
	{
		public static class UILabels
		{
			public static string EDITOR_NAME = "Vim";

			public static string VIM_PATH = "Vim Executable Path";
			public static string FILENAME_EXTENSIONS = "Code Filename Extensions";
		}

		public static class Keys
		{
			public static string VIM_PATH = "VimExecutablePath";
			public static string FILENAME_EXTENSIONS = "VimFilenameExtensions";
		}

		public static class Defaults
		{
			public static string VIM_PATH = "/opt/homebrew/bin/mvim";
			public static string FILENAME_EXTENSIONS = ".cs,.shader,.h,.m,.c,.cpp,.txt,.md,.json";
		}
	}
}
