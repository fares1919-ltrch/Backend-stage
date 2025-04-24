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
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Handle record IDs with or without the "DuplicatedRecords/" prefix
            var normalizedRecordId = recordId;
            if (!recordId.StartsWith("DuplicatedRecords/"))
            {
                normalizedRecordId = $"DuplicatedRecords/{recordId}";
                _logger.LogInformation("Normalized record ID from {RecordId} to {NormalizedRecordId}",
                    recordId, normalizedRecordId);
            }

            // Try to load with the normalized ID first
            var record = await session.LoadAsync<DuplicatedRecord>(normalizedRecordId);

            // If not found, try with the original ID
            if (record == null && normalizedRecordId != recordId)
            {
                _logger.LogInformation("Record not found with normalized ID, trying original ID: {RecordId}", recordId);
                record = await session.LoadAsync<DuplicatedRecord>(recordId);
            }

            if (record == null)
            {
                _logger.LogWarning("Duplicate record with ID {RecordId} not found", recordId);
                throw new Exception($"Duplicate record with ID {recordId} not found");
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
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Handle record IDs with or without the "DuplicatedRecords/" prefix
            var normalizedRecordId = recordId;
            if (!recordId.StartsWith("DuplicatedRecords/"))
            {
                normalizedRecordId = $"DuplicatedRecords/{recordId}";
                _logger.LogInformation("Normalized record ID from {RecordId} to {NormalizedRecordId}",
                    recordId, normalizedRecordId);
            }

            // Try to load with the normalized ID first
            var record = await session.LoadAsync<DuplicatedRecord>(normalizedRecordId);

            // If not found, try with the original ID
            if (record == null && normalizedRecordId != recordId)
            {
                _logger.LogInformation("Record not found with normalized ID, trying original ID: {RecordId}", recordId);
                record = await session.LoadAsync<DuplicatedRecord>(recordId);
            }

            if (record == null)
            {
                _logger.LogWarning("Duplicate record with ID {RecordId} not found", recordId);
                throw new Exception($"Duplicate record with ID {recordId} not found");
            }

            record.Status = "Confirmed";
            record.ConfirmationUser = username;
            record.ConfirmationDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(notes))
            {
                record.Notes = notes;
            }

            await session.SaveChangesAsync();
            _logger.LogInformation("Duplicate record {RecordId} confirmed by {Username}", recordId, username);

            return record;
        }

        public async Task<DuplicatedRecord> RejectDuplicateRecordAsync(string recordId, string username, string notes = null)
        {
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Deduplicated);

            // Handle record IDs with or without the "DuplicatedRecords/" prefix
            var normalizedRecordId = recordId;
            if (!recordId.StartsWith("DuplicatedRecords/"))
            {
                normalizedRecordId = $"DuplicatedRecords/{recordId}";
                _logger.LogInformation("Normalized record ID from {RecordId} to {NormalizedRecordId}",
                    recordId, normalizedRecordId);
            }

            // Try to load with the normalized ID first
            var record = await session.LoadAsync<DuplicatedRecord>(normalizedRecordId);

            // If not found, try with the original ID
            if (record == null && normalizedRecordId != recordId)
            {
                _logger.LogInformation("Record not found with normalized ID, trying original ID: {RecordId}", recordId);
                record = await session.LoadAsync<DuplicatedRecord>(recordId);
            }

            if (record == null)
            {
                _logger.LogWarning("Duplicate record with ID {RecordId} not found", recordId);
                throw new Exception($"Duplicate record with ID {recordId} not found");
            }

            record.Status = "Rejected";
            record.ConfirmationUser = username;
            record.ConfirmationDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(notes))
            {
                record.Notes = notes;
            }

            await session.SaveChangesAsync();
            _logger.LogInformation("Duplicate record {RecordId} rejected by {Username}", recordId, username);

            return record;
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
