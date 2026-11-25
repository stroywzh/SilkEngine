using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository;
using log4net.Repository.Hierarchy;
using SilkEngine.Core.LogSystem;

namespace SilkEngine;

/// <summary>Severity of a log record; larger values are more severe.</summary>
public enum LogLevel : byte
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// The single logging facade. Records are written through log4net; the default destination is the console and
/// <see cref="EnableFileLog"/> adds the project's file log.
/// </summary>
/// <remarks>
/// Output follows the tModLoader style: <c>[HH:mm:ss] [Thread/LEVEL] [Category] message</c>. The category is the
/// caller's source file, so the line stays short while still identifying the subsystem.
/// </remarks>
public static class Log
{
    private const string DefaultCategory = "SilkEngine";

    private static readonly ILoggerRepository s_repository = LogManager.GetRepository(
        typeof(Log).Assembly
    );
    private static readonly Hierarchy s_hierarchy = (Hierarchy)s_repository;
    private static readonly ConcurrentDictionary<string, ILog> s_loggers = new();
    private static readonly PatternLayout s_layout = new()
    {
        ConversionPattern = "[%date{HH:mm:ss}] [%thread/%level] [%logger] %message%newline",
    };

    private static LogLevel s_minLevel = LogLevel.Debug;

    static Log()
    {
        s_layout.ActivateOptions();
        ConfigureConsole();
    }

    /// <summary>Minimum level that is written; records below it are dropped.</summary>
    public static LogLevel MinLevel
    {
        get => s_minLevel;
        set
        {
            s_minLevel = value;
            s_hierarchy.Root.Level = ToLevel(value);
        }
    }

    /// <summary>Replaces the log destination with a custom sink.</summary>
    /// <param name="sink">New sink, or <c>null</c> to restore the default console log.</param>
    public static void SetSink(ILogSink? sink)
    {
        if (sink is null)
        {
            ConfigureConsole();
            return;
        }

        ApplyAppenders(new LogSinkAppender(sink));
    }

    /// <summary>Writes records to the console and to the dated file log; returns the file receiving them.</summary>
    /// <param name="directory">Log directory; the application's <c>Log</c> folder is used when omitted.</param>
    /// <returns>Absolute path of the log file that receives new records.</returns>
    public static string EnableFileLog(string? directory = null)
    {
        string path = string.IsNullOrEmpty(directory) ? LogPaths.GetLogPath() : directory;
        Directory.CreateDirectory(path);

        var file = new RollingFileAppender
        {
            File = Path.Combine(path, "Log.log"),
            AppendToFile = true,
            RollingStyle = RollingFileAppender.RollingMode.Date,
            DatePattern = "yyyy-MM-dd",
            StaticLogFileName = false,
            PreserveLogFileNameExtension = true,
            ImmediateFlush = true,
            Layout = s_layout,
        };
        file.ActivateOptions();

        ApplyAppenders(CreateConsoleAppender(), file);
        return Path.Combine(path, $"Log{DateTime.Now:yyyy-MM-dd}.log");
    }

    /// <summary>Stops file logging and restores the console destination.</summary>
    public static void DisableFileLog() => ConfigureConsole();

    /// <summary>Writes buffered records to their destination.</summary>
    public static void Flush()
    {
        foreach (IAppender appender in s_hierarchy.Root.Appenders)
        {
            switch (appender)
            {
                case BufferingAppenderSkeleton buffered:
                    buffered.Flush();
                    break;
                case LogSinkAppender custom:
                    custom.FlushSink();
                    break;
            }
        }
    }

    /*
     * The APIs below deliberately carry no XML documentation: their behaviour is the agreed contract.
     *
     */
    public static void Debug(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    )
    {
        if (Application.LogSetting.EnableLog)
        {
            Write(LogLevel.Debug, message, callerName, callerFile);
        }
    }

    public static void Info(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Info, message, callerName, callerFile);

    public static void Warning(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Warning, message, callerName, callerFile);

    public static void Error(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Error, message, callerName, callerFile);

    public static void Exception(
        Exception exception,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Error, exception?.ToString() ?? "null", callerName, callerFile);

    internal static Level ToLevel(LogLevel level) =>
        level switch
        {
            LogLevel.Debug => Level.Debug,
            LogLevel.Info => Level.Info,
            LogLevel.Warning => Level.Warn,
            _ => Level.Error,
        };

    internal static LogLevel FromLevel(Level level)
    {
        if (level == Level.Debug)
            return LogLevel.Debug;
        if (level == Level.Warn)
            return LogLevel.Warning;
        if (level == Level.Error || level == Level.Fatal)
            return LogLevel.Error;
        return LogLevel.Info;
    }

    private static void Write(LogLevel level, object? message, string callerName, string callerFile)
    {
        if (level < s_minLevel)
            return;

        ThreadContext.Properties["Caller"] = callerName;
        ThreadContext.Properties["CallerFile"] = callerFile;
        ILog logger = GetLogger(callerFile);
        string text = message?.ToString() ?? "null";

        switch (level)
        {
            case LogLevel.Debug:
                logger.Debug(text);
                break;
            case LogLevel.Info:
                logger.Info(text);
                break;
            case LogLevel.Warning:
                logger.Warn(text);
                break;
            default:
                logger.Error(text);
                break;
        }
    }

    private static ILog GetLogger(string callerFile)
    {
        string category = string.IsNullOrEmpty(callerFile)
            ? DefaultCategory
            : Path.GetFileNameWithoutExtension(callerFile);

        return s_loggers.GetOrAdd(
            category,
            static name => LogManager.GetLogger(typeof(Log).Assembly, name)
        );
    }

    private static void ConfigureConsole()
    {
        ApplyAppenders(CreateConsoleAppender());
    }

    private static ConsoleAppender CreateConsoleAppender()
    {
        var appender = new ConsoleAppender { Layout = s_layout };
        appender.ActivateOptions();
        return appender;
    }

    private static void ApplyAppenders(params IAppender[] appenders)
    {
        // Replace as a whole: a replaced file appender must close its writer before the new one opens.
        foreach (IAppender existing in s_hierarchy.Root.Appenders)
        {
            try
            {
                existing.Close();
            }
            catch
            {
                // Logging is best effort; a failing close must not block the new destination.
            }
        }

        s_hierarchy.Root.RemoveAllAppenders();
        foreach (IAppender appender in appenders)
            s_hierarchy.Root.AddAppender(appender);

        s_hierarchy.Root.Level = ToLevel(s_minLevel);
        s_hierarchy.Configured = true;
    }
}
