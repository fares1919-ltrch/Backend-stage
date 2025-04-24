using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Backend.Data;
using System;
using System.Threading.Tasks;
using Raven.Client.Documents;
using System.Collections.Generic;
using System.Linq;
using Raven.Client.Exceptions.Database;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;

namespace Backend.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class CleanupController : ControllerBase
  {
    private readonly RavenDbContext _context;
    private readonly ILogger<CleanupController> _logger;

    public CleanupController(RavenDbContext context, ILogger<CleanupController> logger)
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Deletes all records from all databases except the users database
    /// </summary>
    /// <returns>A summary of deleted records</returns>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAllRecords()
    {
      try
      {
        var result = new Dictionary<string, int>();

        // List of databases to clean up
        var databases = new[] { "Files", "processes", "Exceptions", "deduplicated", "Conflicts" };

        foreach (var db in databases)
        {
          try
          {
            var count = await CleanupDatabaseAsync(db);
            result.Add(db, count);
          }
          catch (DatabaseDoesNotExistException)
          {
            _logger.LogWarning("Database {Database} does not exist, skipping", db);
            result.Add(db, 0);
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error cleaning up database {Database}", db);
            result.Add(db, -1);
          }
        }

        _logger.LogInformation("All databases cleaned up successfully");
        return Ok(new { message = "All databases cleaned up successfully", deletedCounts = result });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cleaning up databases");
        return StatusCode(500, new { message = "Error cleaning up databases", error = ex.Message });
      }
    }

    private async Task<int> CleanupDatabaseAsync(string databaseName)
    {
      try
      {
        _logger.LogInformation("Cleaning up {Database} database", databaseName);

        // Get the document store for the database
        var store = _context.GetDocumentStore(databaseName);

        // Use a more direct approach - delete all documents in the database
        int deletedCount = 0;

        // First, count the documents
        using (var session = store.OpenSession())
        {
          deletedCount = session.Advanced.RawQuery<object>("from @all_docs").Count();
        }

        if (deletedCount > 0)
        {
          // Then delete them all
          var operation = await store.Operations.SendAsync(new DeleteByQueryOperation(new IndexQuery
          {
            Query = "from @all_docs"
          }));

          // Wait for the operation to complete
          await operation.WaitForCompletionAsync();
        }

        _logger.LogInformation("Deleted {Count} documents from {Database} database", deletedCount, databaseName);
        return deletedCount;
      }
      catch (DatabaseDoesNotExistException ex)
      {
        _logger.LogWarning(ex, "Database {Database} does not exist", databaseName);
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error cleaning up {Database} database", databaseName);
        return -1; // Return -1 to indicate an error
      }
    }
  }
}
