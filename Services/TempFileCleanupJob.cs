using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Backend.Services
{
    public class TempFileCleanupJob : IHostedService, IDisposable
    {
        private readonly ILogger<TempFileCleanupJob> _logger;
        private readonly string _tempFilesPath;
        private Timer _timer;

        public TempFileCleanupJob(IConfiguration configuration, ILogger<TempFileCleanupJob> logger)
        {
            _logger = logger;
            _tempFilesPath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Temp File Cleanup Job is starting.");

            // Run cleanup every hour
            _timer = new Timer(DoCleanup, null, TimeSpan.Zero, TimeSpan.FromHours(1));

            return Task.CompletedTask;
        }

        private void DoCleanup(object state)
        {
            try
            {
                _logger.LogInformation("Starting temporary file cleanup");

                if (Directory.Exists(_tempFilesPath))
                {
                    var oldDirectories = Directory.GetDirectories(_tempFilesPath)
                        .Select(d => new DirectoryInfo(d))
                        .Where(d => d.CreationTime < DateTime.Now.AddDays(-1)) // Older than 1 day
                        .ToList();

                    foreach (var dir in oldDirectories)
                    {
                        try
                        {
                            Directory.Delete(dir.FullName, true);
                            _logger.LogInformation("Deleted old temporary directory: {DirectoryName}", dir.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error deleting temporary directory: {DirectoryName}", dir.Name);
                        }
                    }

                    _logger.LogInformation("Cleaned up {Count} old temporary directories", oldDirectories.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during temporary file cleanup");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Temp File Cleanup Job is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
