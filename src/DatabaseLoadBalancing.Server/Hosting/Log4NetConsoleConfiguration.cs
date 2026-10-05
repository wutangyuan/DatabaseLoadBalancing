using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;

namespace DatabaseLoadBalancing.Server.Hosting;

public static class Log4NetConsoleConfiguration
{
    public const string ConsolePattern = "%date %-5level %message%newline";

    public static void Configure(ILoggerRepository repository)
    {
        if (repository is Hierarchy hierarchy)
        {
            hierarchy.ResetConfiguration();
        }

        BasicConfigurator.Configure(repository, CreateConsoleAppender());
    }

    public static ColoredConsoleAppender CreateConsoleAppender()
    {
        var appender = new ColoredConsoleAppender
        {
            Threshold = Level.Info,
            Layout = new PatternLayout(ConsolePattern)
        };
        appender.ActivateOptions();
        return appender;
    }
}
