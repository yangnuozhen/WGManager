using System.Diagnostics;
namespace PortManager.Client.Worker;
public static class LinuxShell
{
    public static void Run(string file, string args)
    {
        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }
        };

        p.Start();
        p.WaitForExit();
    }
}