using System;
using System.Reflection;
using System.Security.Principal;

namespace OfflineInstaller._managers
{
    public class SystemManager
    {
        public static bool IsRunningAsAdmin()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

		/// <summary>
		/// Query the program to get the current version number of the software that is running.
		/// </summary>
		/// <returns>A string of the version number in the format X.X.X.X</returns>
		public static string? GetVersionNumber()
		{
			Assembly? assembly = Assembly.GetExecutingAssembly();
			if (assembly == null) return "N/A";

			Version? version = assembly.GetName().Version;
			if (version == null) return "N/A";

			// Format the version number as Major.Minor.Build
			return $"{version.Major}.{version.Minor}.{version.Build}";
		}
	}
}
