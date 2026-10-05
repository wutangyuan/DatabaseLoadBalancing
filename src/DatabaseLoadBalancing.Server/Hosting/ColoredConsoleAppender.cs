using log4net.Appender;
using log4net.Core;

namespace DatabaseLoadBalancing.Server.Hosting;

public sealed class ColoredConsoleAppender : AppenderSkeleton
{
    public static ConsoleColor ColorFor(Level? level) => level is not null && level >= Level.Error
        ? ConsoleColor.Red
        : level is not null && level >= Level.Warn ? ConsoleColor.Yellow : ConsoleColor.White;

    protected override void Append(LoggingEvent loggingEvent)
    {
        var text = RenderLoggingEvent(loggingEvent);
        var original = Console.ForegroundColor;
        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.ForegroundColor = ColorFor(loggingEvent.Level);
            }

            Console.Write(text);
        }
        finally
        {
            if (!Console.IsOutputRedirected)
            {
                Console.ForegroundColor = original;
            }
        }
    }
}

