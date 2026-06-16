namespace InterviewPrepAPI.Logging;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string logDirectory, LogLevel minimumLevel = LogLevel.Warning)
    {
        builder.AddProvider(new FileLoggerProvider(logDirectory, minimumLevel));
        return builder;
    }
}
