using System.Reflection;
using log4net;
using log4net.Config;

namespace DatabaseLoadBalancing.Server.Hosting;

public static class LoggingBootstrap
{
    public static ILog Configure(Assembly assembly)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "log4net.config");
        if (File.Exists(path))
        {
            XmlConfigurator.ConfigureAndWatch(LogManager.GetRepository(assembly), new FileInfo(path));
        }

        return LogManager.GetLogger(assembly, typeof(LoggingBootstrap));
    }
}

