using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace VehicleManagement.Logging;

public static class FileLogging
{
    public static ILoggingBuilder AddErrorFile(this ILoggingBuilder logging,
        IConfiguration configuration, string contentRoot)
    {
        // Create the sink through DI so the host owns its lifetime and tests can replace it.
        logging.Services.AddSingleton<ILoggerProvider>(_ =>
        {
            var settings = configuration.GetSection("FileLogging");
            var directory = settings["Directory"] ?? "Logs";
            var path = Path.GetFullPath(directory, contentRoot);
            var limit = settings.GetValue<long>("FileSizeLimitBytes", 10_000_000);
            var retained = settings.GetValue<int>("RetainedFileCountLimit", 14);
            
            if (limit <= 0 || retained <= 0)
                throw new InvalidOperationException("File logging size and retention limits must be positive.");
            
            Directory.CreateDirectory(path);

            var logger = new LoggerConfiguration()
                .MinimumLevel.Warning()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .MinimumLevel.Override("System", LogEventLevel.Error)
                .Enrich.FromLogContext()
                .WriteTo.File(Path.Combine(path, "vehiclemanagement-.log"),
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: limit,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: retained,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [RequestId: {RequestId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            return new SerilogLoggerProvider(logger, dispose: true);
        });

        return logging;
    }
}
