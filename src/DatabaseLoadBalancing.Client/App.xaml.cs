using System.IO;
using System.Windows;
using log4net;
using log4net.Config;

namespace DatabaseLoadBalancing.Client;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "log4net.config");
        if (File.Exists(path))
        {
            XmlConfigurator.ConfigureAndWatch(LogManager.GetRepository(typeof(App).Assembly), new FileInfo(path));
        }

        base.OnStartup(e);
    }
}

