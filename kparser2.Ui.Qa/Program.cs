using System.IO;
using System.Windows;

namespace kparser2.Ui.Qa;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: kparser2.Ui.Qa <frozen capture.ndjson> <new output directory>");
            return 2;
        }
        var output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            Console.Error.WriteLine("Use a new output directory to preserve earlier QA evidence.");
            return 2;
        }
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("KPARSER2_VIEW_SETTINGS_PATH", Path.Combine(output, "view-settings.json"));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var exit = 1;
        app.Startup += async (_, _) =>
        {
            try { exit = await new ReplayQa().RunAsync(Path.GetFullPath(args[0]), output); }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString());
                Console.Error.WriteLine(ex);
            }
            finally { app.Shutdown(exit); }
        };
        app.Run();
        return exit;
    }
}
