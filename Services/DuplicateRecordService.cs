using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Backend.Data;
using Backend.Models;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Backend.Services
{
    public class DuplicateRecordService
    {
        private readonly RavenDbContext _context;
        private readonly ILogger<DuplicateRecordService> _logger;

        public DuplicateRecordService(RavenDbContext context, ILogger<DuplicateRecordService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<DuplicatedRecord>> GetAllDuplicateRecordsAsync()
        {
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);
            var records = await session.Query<DuplicatedRecord>().ToListAsync();
            return records;
        }

        public async Task<DuplicatedRecord> GetDuplicateRecordAsync(string recordId)
        {
            if (string.IsNullOrEmpty(recordId))
            {
                _logger.LogError("Record ID is null or empty");
                throw new ArgumentException("Record ID cannot be null or empty");
            }

            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Try multiple ID formats to be more flexible
            var possibleIds = new List<string>();

            // Format 1: As provided
            possibleIds.Add(recordId);

            // Format 2: With DuplicatedRecords/ prefix
            if (!recordId.StartsWith("DuplicatedRecords/"))
            {
                possibleIds.Add($"DuplicatedRecords/{recordId}");
            }

            // Format 3: Without DuplicatedRecords/ prefix
            if (recordId.StartsWith("DuplicatedRecords/"))
            {
                possibleIds.Add(recordId.Substring("DuplicatedRecords/".Length));
            }

            // Format 4: With DuplicatedRecords/ prefix and without dashes
            if (recordId.Contains("-"))
            {
                var noDashes = recordId.Replace("-", "");
                possibleIds.Add($"DuplicatedRecords/{noDashes}");
            }

            _logger.LogInformation("Trying to find duplicate record with ID {RecordId} using {Count} possible formats",
                recordId, possibleIds.Count);

            // Try each possible ID format
            DuplicatedRecord record = null;
            foreach (var id in possibleIds)
            {
                _logger.LogDebug("Trying to load record with ID: {Id}", id);
                record = await session.LoadAsync<DuplicatedRecord>(id);
                if (record != null)
                {
                    _logger.LogInformation("Found record with ID format: {Id}", id);
                    break;
                }
            }

            // If still not found, try a query approach
            if (record == null)
            {
                _logger.LogInformation("Record not found with direct loading, trying query approach");

                // Extract GUID part if it exists
                string guidPart = recordId;
                if (recordId.Contains("/"))
                {
                    guidPart = recordId.Split('/').Last();
                }

                // Try to query by ID ending with the GUID part
                var results = await session.Query<DuplicatedRecord>()
                    .Where(r => r.Id.EndsWith(guidPart))
                    .ToListAsync();

                if (results.Any())
                {
                    record = results.First();
                    _logger.LogInformation("Found record by query: {RecordId}", record.Id);
                }
            }

            if (record == null)
            {
                _logger.LogWarning("Duplicate record with ID {RecordId} not found after trying multiple formats", recordId);
                throw new Exception($"Duplicate record with ID {recordId} not found. Please check if the ID is correct and try again.");
            }

            return record;
        }

        public async Task<List<DuplicatedRecord>> GetDuplicateRecordsByProcessAsync(string processId)
        {
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Handle process IDs with or without the "processes/" prefix
            var normalizedProcessId = processId;
            if (!processId.StartsWith("processes/"))
            {
                normalizedProcessId = $"processes/{processId}";
            }

            _logger.LogInformation("Looking for duplicate records with ProcessId: {ProcessId} or {NormalizedProcessId}",
                processId, normalizedProcessId);

            // Query for records with either the original or normalized process ID
            var records = await session
                .Query<DuplicatedRecord>()
                .Where(r => r.ProcessId == processId || r.ProcessId == normalizedProcessId)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} duplicate records for process {ProcessId}", records.Count, processId);

            return records;
        }

        public async Task<DuplicatedRecord> ConfirmDuplicateRecordAsync(string recordId, string username, string notes = null)
        {
            // Use our improved GetDuplicateRecordAsync method to find the record
            var record = await GetDuplicateRecordAsync(recordId);

            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Load the record in this session
            var sessionRecord = await session.LoadAsync<DuplicatedRecord>(record.Id);

            if (sessionRecord == null)
            {
                _logger.LogError("Failed to load record {RecordId} in confirmation session", record.Id);
                throw new Exception($"Failed to load record {record.Id} for confirmation");
            }

            sessionRecord.Status = "Confirmed";
            sessionRecord.ConfirmationUser = username;
            sessionRecord.ConfirmationDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(notes))
            {
                sessionRecord.Notes = notes;
            }

            await session.SaveChangesAsync();
            _logger.LogInformation("Duplicate record {RecordId} confirmed by {Username}", record.Id, username);

            return sessionRecord;
        }

        public async Task<DuplicatedRecord> RejectDuplicateRecordAsync(string recordId, string username, string notes = null)
        {
            // Use our improved GetDuplicateRecordAsync method to find the record
            var record = await GetDuplicateRecordAsync(recordId);

            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Load the record in this session
            var sessionRecord = await session.LoadAsync<DuplicatedRecord>(record.Id);

            if (sessionRecord == null)
            {
                _logger.LogError("Failed to load record {RecordId} in rejection session", record.Id);
                throw new Exception($"Failed to load record {record.Id} for rejection");
            }

            sessionRecord.Status = "Rejected";
            sessionRecord.ConfirmationUser = username;
            sessionRecord.ConfirmationDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(notes))
            {
                sessionRecord.Notes = notes;
            }

            await session.SaveChangesAsync();
            _logger.LogInformation("Duplicate record {RecordId} rejected by {Username}", record.Id, username);

            return sessionRecord;
        }

        public async Task<List<DuplicatedRecord>> GetDuplicatesByStatusAsync(string status)
        {
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);
            var records = await session
                .Query<DuplicatedRecord>()
                .Where(r => r.Status == status)
                .ToListAsync();

            return records;
        }

        public async Task<DuplicatedRecord> CreateDuplicateRecordAsync(
            string processId,
            string originalFileId,
            string originalFileName,
            List<DuplicateMatch> duplicates)
        {
            // Ensure processId has the correct prefix
            var normalizedProcessId = processId;
            if (!string.IsNullOrEmpty(processId) && !processId.StartsWith("processes/"))
            {
                normalizedProcessId = $"processes/{processId}";
                _logger.LogInformation("Normalized process ID from {ProcessId} to {NormalizedProcessId} during duplicate record creation",
                    processId, normalizedProcessId);
            }

            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            var duplicatedRecord = new DuplicatedRecord
            {
                Id = $"DuplicatedRecords/{Guid.NewGuid()}",
                ProcessId = normalizedProcessId,
                OriginalFileId = originalFileId,
                OriginalFileName = originalFileName,
                DetectedDate = DateTime.UtcNow,
                Status = "Detected",
                ConfirmationUser = string.Empty,
                ConfirmationDate = null,
                Notes = null,
                Duplicates = duplicates
            };

            await session.StoreAsync(duplicatedRecord);
            await session.SaveChangesAsync();

            _logger.LogInformation("Created duplicate record {RecordId} for file {FileName} with {DuplicateCount} duplicates",
                duplicatedRecord.Id, originalFileName, duplicates.Count);

            return duplicatedRecord;
        }
    }
}
