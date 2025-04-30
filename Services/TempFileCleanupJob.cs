using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Backend.Utilities;

namespace Backend.Services
{
    /// <summary>
    /// Background service that periodically cleans up temporary files
    /// </summary>
    public class TempFileCleanupJob : IHostedService, IDisposable
    {
        private readonly ILogger<TempFileCleanupJob> _logger;
        private readonly string _tempFilesPath;
        private readonly int _cleanupIntervalHours;
        private readonly int _maxAgeInDays;
        private Timer _timer;

        /// <summary>
        /// Initializes a new instance of the TempFileCleanupJob
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        /// <param name="logger">Logger</param>
        public TempFileCleanupJob(IConfiguration configuration, ILogger<TempFileCleanupJob> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tempFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles");

            // Read configuration values or use defaults
            _cleanupIntervalHours = configuration.GetValue<int>("TempFiles:CleanupIntervalHours", 1);
            _maxAgeInDays = configuration.GetValue<int>("TempFiles:MaxAgeInDays", 1);

            _logger.LogInformation("TempFileCleanupJob initialized with cleanup interval of {Hours} hours and max age of {Days} days",
                _cleanupIntervalHours, _maxAgeInDays);
        }

        /// <summary>
        /// Starts the cleanup job
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Temp File Cleanup Job is starting");

            // Run cleanup immediately and then at the configured interval
            _timer = new Timer(DoCleanup, null, TimeSpan.Zero, TimeSpan.FromHours(_cleanupIntervalHours));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs the cleanup operation
        /// </summary>
        /// <param name="state">State object (not used)</param>
        private void DoCleanup(object state)
        {
            try
            {
                _logger.LogInformation("Starting temporary file cleanup");

                if (!Directory.Exists(_tempFilesPath))
                {
                    _logger.LogInformation("Temp files directory does not exist: {Path}", _tempFilesPath);
                    return;
                }

                var cutoffDate = DateTime.Now.AddDays(-_maxAgeInDays);
                var oldDirectories = Directory.GetDirectories(_tempFilesPath)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => d.CreationTime < cutoffDate)
                    .ToList();

                _logger.LogInformation("Found {Count} directories older than {Days} days",
                    oldDirectories.Count, _maxAgeInDays);

                int successCount = 0;
                int errorCount = 0;

                foreach (var dir in oldDirectories)
                {
                    if (ResourceUtility.SafeDeleteDirectory(dir.FullName, true, _logger))
                    {
                        _logger.LogInformation("Deleted old temporary directory: {DirectoryName}", dir.Name);
                        successCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }

                _logger.LogInformation("Temporary file cleanup completed. Deleted: {SuccessCount}, Errors: {ErrorCount}",
                    successCount, errorCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temporary file cleanup");
            }
        }

        /// <summary>
        /// Stops the cleanup job
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Temp File Cleanup Job is stopping");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
