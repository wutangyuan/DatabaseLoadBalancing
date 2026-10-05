using DatabaseLoadBalancing.Server.Hosting;
using log4net.Core;
using log4net.Layout;

namespace DatabaseLoadBalancing.Server.UnitTests.Hosting;

public sealed class ColoredConsoleAppenderTests
{
    [Fact]
    public void LevelColors_UseTheContractMapping()
    {
        Assert.Equal(ConsoleColor.White, ColoredConsoleAppender.ColorFor(Level.Info));
        Assert.Equal(ConsoleColor.Yellow, ColoredConsoleAppender.ColorFor(Level.Warn));
        Assert.Equal(ConsoleColor.Red, ColoredConsoleAppender.ColorFor(Level.Error));
    }

    [Fact]
    public void CreateConsoleAppender_UsesTheExpectedLayoutAndLevel()
    {
        var appender = Log4NetConsoleConfiguration.CreateConsoleAppender();

        var layout = Assert.IsType<PatternLayout>(appender.Layout);
        Assert.Equal(Log4NetConsoleConfiguration.ConsolePattern, layout.ConversionPattern);
        Assert.Equal(Level.Info, appender.Threshold);
    }
}

