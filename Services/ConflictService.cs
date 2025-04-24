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

        public async Task<List<Conflict>> GetConflictsByProcessIdAsync(string processId)
        {
            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);
            var conflicts = await session.Query<Conflict>()
                .Where(c => c.ProcessId == processId)
                .ToListAsync();

            return conflicts;
        }

        public async Task<Conflict> CreateConflictAsync(string processId, string fileName, string matchedFileName, double confidence)
        {
            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);
            var conflict = new Conflict
            {
                Id = $"Conflicts/{Guid.NewGuid()}",
                ProcessId = processId,
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
            // Explicitly use the conflicts database
            using var session = _context.OpenAsyncSession(RavenDbContext.DatabaseType.Conflicts);
            var conflict = await session.LoadAsync<Conflict>(conflictId);
            if (conflict == null)
            {
                throw new Exception($"Conflict with ID {conflictId} not found");
            }

            conflict.Status = "Resolved";
            conflict.Resolution = resolution;
            conflict.ResolvedBy = resolvedBy;
            conflict.ResolvedAt = DateTime.UtcNow;

            await session.SaveChangesAsync();
            return conflict;
        }
    }
}
