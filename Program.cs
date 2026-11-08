using NetLogViewer.Forms;
using System.Globalization;

namespace NetLogViewer;

static class Program
{
    /// <summary>
    ///  The main entry point for the NetLog Viewer application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Enforce en-US culture for all UI text, number and date formatting as requested
        var culture = new CultureInfo("en-US");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Initialize modern WinForms configuration (High DPI, default fonts)
        ApplicationConfiguration.Initialize();
        Application.SetColorMode(SystemColorMode.Dark);

        var mainForm = new MainForm();

        // If file argument is provided on command line, queue it for loading
        if (args.Length > 0 && File.Exists(args[0]))
        {
            mainForm.Shown += async (s, e) =>
            {
                // Trigger file open via reflection or method
                var method = typeof(MainForm).GetMethod("LoadFileAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    var task = (System.Threading.Tasks.Task?)method.Invoke(mainForm, [args[0]]);
                    if (task != null) await task;
                }
            };
        }

        Application.Run(mainForm);
    }
}
