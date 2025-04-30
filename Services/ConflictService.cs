using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Backend.Models;
using Backend.Data;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents;

namespace Backend.Services
{
    public class ConflictService
    {
        private readonly RavenDbContext _context;

        public ConflictService(RavenDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Conflict>> GetAllConflictsAsync()
        {
            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);

            // Get all conflicts (limit to 1000 to avoid performance issues)
            var conflicts = await session.Query<Conflict>()
                .Take(1000)
                .ToListAsync();

            return conflicts;
        }

        public async Task<List<Conflict>> GetConflictsByProcessIdAsync(string processId)
        {
            if (string.IsNullOrEmpty(processId))
            {
                throw new ArgumentException("Process ID cannot be null or empty");
            }

            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);

            // Get all conflicts first (this is more efficient than trying to use complex queries with RavenDB)
            var allConflicts = await session.Query<Conflict>()
                .ToListAsync();

            // Now filter in memory with the different possible ID formats
            var normalizedProcessId = processId;
            var withoutPrefix = processId;

            // Normalize the process ID formats
            if (processId.StartsWith("processes/"))
            {
                normalizedProcessId = processId;
                withoutPrefix = processId.Substring("processes/".Length);
            }
            else
            {
                normalizedProcessId = $"processes/{processId}";
                withoutPrefix = processId;
            }

            // Filter the conflicts in memory
            var conflicts = allConflicts.Where(c =>
                c.ProcessId == normalizedProcessId ||
                c.ProcessId == withoutPrefix ||
                c.ProcessId == processId).ToList();

            return conflicts;
        }

        public async Task<Conflict> GetConflictAsync(string conflictId)
        {
            if (string.IsNullOrEmpty(conflictId))
            {
                throw new ArgumentException("Conflict ID cannot be null or empty");
            }

            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);

            // Try multiple ID formats to be more flexible
            var possibleIds = new List<string>();

            // Format 1: As provided
            possibleIds.Add(conflictId);

            // Format 2: With Conflicts/ prefix
            if (!conflictId.StartsWith("Conflicts/"))
            {
                possibleIds.Add($"Conflicts/{conflictId}");
            }

            // Format 3: Without Conflicts/ prefix
            if (conflictId.StartsWith("Conflicts/"))
            {
                possibleIds.Add(conflictId.Substring("Conflicts/".Length));
            }

            // Try each possible ID format
            Conflict conflict = null;
            foreach (var id in possibleIds)
            {
                conflict = await session.LoadAsync<Conflict>(id);
                if (conflict != null)
                {
                    break;
                }
            }

            if (conflict == null)
            {
                throw new Exception($"Conflict with ID {conflictId} not found");
            }

            return conflict;
        }

        public async Task<Conflict> CreateConflictAsync(string processId, string fileName, string matchedFileName, double confidence)
        {
            if (string.IsNullOrEmpty(processId))
            {
                throw new ArgumentException("Process ID cannot be null or empty");
            }

            // Normalize the process ID format
            string normalizedProcessId = processId;
            if (!processId.StartsWith("processes/") && !processId.StartsWith("DeduplicationProcesses/"))
            {
                normalizedProcessId = $"processes/{processId}";
            }

            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);
            var conflict = new Conflict
            {
                Id = $"Conflicts/{Guid.NewGuid()}",
                ProcessId = normalizedProcessId,
                FileName = fileName,
                MatchedFileName = matchedFileName,
                Confidence = confidence,
                Status = "Unresolved",
                CreatedAt = DateTime.UtcNow
            };

            await session.StoreAsync(conflict);
            await session.SaveChangesAsync();
            return conflict;
        }

        public async Task<Conflict> ResolveConflictAsync(string conflictId, string resolution, string resolvedBy)
        {
            try
            {
                // Get the conflict using the flexible ID method
                var conflict = await GetConflictAsync(conflictId);

                // Explicitly use the conflicts database
                using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);

                // Load the conflict with the correct ID from the database
                var conflictToUpdate = await session.LoadAsync<Conflict>(conflict.Id);

                conflictToUpdate.Status = "Resolved";
                conflictToUpdate.Resolution = resolution;
                conflictToUpdate.ResolvedBy = resolvedBy;
                conflictToUpdate.ResolvedAt = DateTime.UtcNow;

                await session.SaveChangesAsync();
                return conflictToUpdate;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error resolving conflict: {ex.Message}", ex);
            }
        }
    }
}
