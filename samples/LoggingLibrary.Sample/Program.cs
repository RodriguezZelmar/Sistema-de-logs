using LoggingLibrary;
using Microsoft.Extensions.Configuration;

// 1. Configure the logger from appsettings.json.
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json")
    .Build();

var options = LoggerOptions.FromConfiguration(configuration.GetSection("LoggingLibrary"));

// Keep the logs next to the executable instead of the current directory.
options.FilePath = Path.GetFullPath(options.FilePath, AppContext.BaseDirectory);

using (var logger = new Logger(options))
{
    // 2. Entries of different levels. Debug is below the configured minimum
    //    level (Info), so it is discarded.
    logger.Debug("Not written: Debug is below MinimumLevel");
    logger.Info("Sample application started");

    // 3. A warning.
    logger.Warning("Disk space is below 10%");

    // 4. An error with an exception.
    try
    {
        ConnectToDatabase();
    }
    catch (Exception ex)
    {
        logger.Error("Could not connect to the database", ex);
    }

    logger.Info("Sample application finished");
}

// 5. Show where the logs are stored, and what was written.
Console.WriteLine($"Logs written to: {options.FilePath}");
Console.WriteLine();
foreach (var line in File.ReadLines(options.FilePath).TakeLast(4))
{
    Console.WriteLine(line);
}

static void ConnectToDatabase() =>
    throw new InvalidOperationException(
        "Database unavailable",
        new TimeoutException("The server did not respond within 5 seconds."));
