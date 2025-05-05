using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Backend.Models;
using Backend.Data;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents;
namespace Backend.Services
{
    public class ExceptionService
    {
        private readonly RavenDbContext _context;
        private readonly ILogger<ExceptionService> _logger;

        public ExceptionService(RavenDbContext context, ILogger<ExceptionService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<Backend.Models.DeduplicationException>> GetExceptionsByProcessIdAsync(string processId)
        {
            // Explicitly use the exceptions database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);

            // Handle process IDs with or without the "processes/" prefix
            var normalizedProcessId = processId;
            if (!processId.StartsWith("processes/"))
            {
                normalizedProcessId = $"processes/{processId}";
            }

            _logger.LogInformation("Looking for exceptions with ProcessId: {ProcessId} or {NormalizedProcessId}",
                processId, normalizedProcessId);

            // Query for exceptions with either the original or normalized process ID
            var exceptions = await session.Query<Backend.Models.DeduplicationException>()
                .Where(e => e.ProcessId == processId || e.ProcessId == normalizedProcessId)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} exceptions for process {ProcessId}", exceptions.Count, processId);
            return exceptions;
        }

        public async Task<Backend.Models.DeduplicationException> CreateExceptionAsync(
            string processId,
            string fileName,
            List<string> candidateFileNames,
            double comparisonScore,
            Dictionary<string, object> metadata = null)
        {
            // Ensure processId has the correct prefix
            var normalizedProcessId = processId;
            if (!string.IsNullOrEmpty(processId) && !processId.StartsWith("processes/"))
            {
                normalizedProcessId = $"processes/{processId}";
                _logger.LogInformation("Normalized process ID from {ProcessId} to {NormalizedProcessId} during exception creation",
                    processId, normalizedProcessId);
            }

            // Explicitly use the exceptions database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);
            var exception = new Backend.Models.DeduplicationException
            {
                Id = $"Exceptions/{Guid.NewGuid()}",
                ProcessId = normalizedProcessId,
                FileName = fileName,
                CandidateFileNames = candidateFileNames,
                ComparisonScore = comparisonScore,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Metadata = metadata ?? new Dictionary<string, object>()
            };

            await session.StoreAsync(exception);
            await session.SaveChangesAsync();

            _logger.LogInformation("Created exception {ExceptionId} for file {FileName} with {CandidateCount} candidates",
                exception.Id, fileName, candidateFileNames.Count);

            return exception;
        }

        public async Task<Backend.Models.DeduplicationException> UpdateExceptionStatusAsync(
            string exceptionId,
            string status,
            Dictionary<string, object> additionalMetadata = null)
        {
            // Explicitly use the exceptions database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);

            // Handle exception IDs with or without the "Exceptions/" prefix
            var normalizedExceptionId = exceptionId;
            if (!exceptionId.StartsWith("Exceptions/"))
            {
                normalizedExceptionId = $"Exceptions/{exceptionId}";
                _logger.LogInformation("Normalized exception ID from {ExceptionId} to {NormalizedExceptionId}",
                    exceptionId, normalizedExceptionId);
            }

            // Try to load with the normalized ID first
            var exception = await session.LoadAsync<Backend.Models.DeduplicationException>(normalizedExceptionId);

            // If not found, try with the original ID
            if (exception == null && normalizedExceptionId != exceptionId)
            {
                _logger.LogInformation("Exception not found with normalized ID, trying original ID: {ExceptionId}", exceptionId);
                exception = await session.LoadAsync<Backend.Models.DeduplicationException>(exceptionId);
            }

            if (exception == null)
            {
                _logger.LogWarning("Exception with ID {ExceptionId} not found", exceptionId);
                throw new ArgumentException($"Exception with ID {exceptionId} not found");
            }

            exception.Status = status;
            exception.UpdatedAt = DateTime.UtcNow;

            // Add additional metadata if provided
            if (additionalMetadata != null)
            {
                if (exception.Metadata == null)
                    exception.Metadata = new Dictionary<string, object>();

                foreach (var kvp in additionalMetadata)
                {
                    exception.Metadata[kvp.Key] = kvp.Value;
                }
            }

            await session.SaveChangesAsync();

            _logger.LogInformation("Updated exception {ExceptionId} status to {Status}", exceptionId, status);

            return exception;
        }

        public async Task<List<Backend.Models.DeduplicationException>> GetExceptionsByScoreThresholdAsync(double threshold)
        {
            // Explicitly use the exceptions database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);
            var exceptions = await session.Query<Backend.Models.DeduplicationException>()
                .Where(e => e.ComparisonScore >= threshold)
                .OrderByDescending(e => e.ComparisonScore)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} exceptions with score >= {Threshold}",
                exceptions.Count, threshold);

            return exceptions;
        }

        public async Task<Dictionary<string, int>> GetExceptionStatisticsAsync()
        {
            // Explicitly use the exceptions database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);
            var allExceptions = await session.Query<Backend.Models.DeduplicationException>().ToListAsync();

            var statistics = new Dictionary<string, int>
            {
                ["total"] = allExceptions.Count,
                ["pending"] = allExceptions.Count(e => e.Status == "Pending"),
                ["reviewed"] = allExceptions.Count(e => e.Status == "Reviewed"),
                ["confirmed"] = allExceptions.Count(e => e.Status == "Confirmed"),
                ["rejected"] = allExceptions.Count(e => e.Status == "Rejected"),
                ["highConfidence"] = allExceptions.Count(e => e.ComparisonScore >= 0.9),
                ["mediumConfidence"] = allExceptions.Count(e => e.ComparisonScore >= 0.8 && e.ComparisonScore < 0.9),
                ["lowConfidence"] = allExceptions.Count(e => e.ComparisonScore < 0.8)
            };

            return statistics;
        }

        public async Task<List<Backend.Models.DeduplicationException>> GetAllExceptionsAsync()
        {
            // Explicitly use the exceptions database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Exceptions);

            // Query for all exceptions
            var exceptions = await session.Query<Backend.Models.DeduplicationException>()
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} exceptions from the database", exceptions.Count);
            return exceptions;
        }
    }
}
