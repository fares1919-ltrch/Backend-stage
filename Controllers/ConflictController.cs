using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Services;
using Backend.Models;
using Backend.DTOs;
using Microsoft.Extensions.Logging;

namespace Backend.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  [Authorize]
  public class ConflictController : ControllerBase
  {
    private readonly ConflictService _conflictService;
    private readonly IdNormalizationService _idNormalizationService;
    private readonly ApiResponseService _apiResponseService;
    private readonly ILogger<ConflictController>? _logger;

    public ConflictController(
        ConflictService conflictService,
        IdNormalizationService idNormalizationService,
        ApiResponseService apiResponseService,
        ILogger<ConflictController>? logger = null)
    {
      _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));
      _idNormalizationService = idNormalizationService ?? throw new ArgumentNullException(nameof(idNormalizationService));
      _apiResponseService = apiResponseService ?? throw new ArgumentNullException(nameof(apiResponseService));
      _logger = logger;
    }

    /// <summary>
    /// Gets a specific conflict by ID
    /// </summary>
    /// <param name="id">The conflict ID</param>
    /// <returns>The conflict details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConflictDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetConflict(string id)
    {
      try
      {
        _logger?.LogInformation("Getting conflict with ID: {ConflictId}", id);

        // Normalize the ID to ensure it has the correct prefix
        string normalizedId = _idNormalizationService.NormalizeConflictId(id);

        // Try to get the conflict
        var conflict = await _conflictService.GetConflictAsync(normalizedId);

        // Convert to DTO for frontend consumption
        var conflictDto = new ConflictDto
        {
          Id = conflict.Id,
          ShortId = _idNormalizationService.GetShortId(conflict.Id, "conflict"),
          ProcessId = conflict.ProcessId,
          ShortProcessId = _idNormalizationService.GetShortId(conflict.ProcessId, "process"),
          FileName = conflict.FileName,
          MatchedFileName = conflict.MatchedFileName,
          Confidence = conflict.Confidence,
          Status = conflict.Status,
          CreatedAt = conflict.CreatedAt,
          ResolvedBy = conflict.ResolvedBy,
          ResolvedAt = conflict.ResolvedAt,
          Resolution = conflict.Resolution
        };

        return _apiResponseService.Success(conflictDto);
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error retrieving conflict {ConflictId}", id);

        if (ex.Message.Contains("not found"))
        {
          // Try with different ID formats
          try
          {
            // Get all possible ID variations
            var idVariations = _idNormalizationService.GetIdVariations(id, "conflict");

            // Try each variation
            foreach (var variation in idVariations.Where(v => v != id))
            {
              try
              {
                _logger?.LogInformation("Trying with ID variation: {VariationId}", variation);
                var conflict = await _conflictService.GetConflictAsync(variation);

                // If we get here, we found the conflict
                var conflictDto = new ConflictDto
                {
                  Id = conflict.Id,
                  ShortId = _idNormalizationService.GetShortId(conflict.Id, "conflict"),
                  ProcessId = conflict.ProcessId,
                  ShortProcessId = _idNormalizationService.GetShortId(conflict.ProcessId, "process"),
                  FileName = conflict.FileName,
                  MatchedFileName = conflict.MatchedFileName,
                  Confidence = conflict.Confidence,
                  Status = conflict.Status,
                  CreatedAt = conflict.CreatedAt,
                  ResolvedBy = conflict.ResolvedBy,
                  ResolvedAt = conflict.ResolvedAt,
                  Resolution = conflict.Resolution
                };

                return _apiResponseService.Success(conflictDto);
              }
              catch
              {
                // Continue to the next variation
              }
            }
          }
          catch
          {
            // If all variations fail, return not found
          }

          return _apiResponseService.NotFound("Conflict not found", "conflict", id);
        }

        return _apiResponseService.Error(ex.Message);
      }
    }



    /// <summary>
    /// Gets all conflicts in the system (for debugging)
    /// </summary>
    /// <returns>List of all conflicts</returns>
    [HttpGet("all")]
    [ProducesResponseType(typeof(List<Conflict>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetAllConflicts()
    {
      try
      {
        _logger?.LogInformation("Getting all conflicts");
        var conflicts = await _conflictService.GetAllConflictsAsync();
        return Ok(conflicts);
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error retrieving all conflicts");
        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Gets all conflicts for a process
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <returns>List of conflicts</returns>
    [HttpGet("process/{processId}")]
    [ProducesResponseType(typeof(List<Conflict>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetConflictsByProcess(string processId)
    {
      try
      {
        _logger?.LogInformation("Getting conflicts for process: {ProcessId}", processId);
        var conflicts = await _conflictService.GetConflictsByProcessIdAsync(processId);

        // Log the number of conflicts found
        _logger?.LogInformation("Found {ConflictCount} conflicts for process {ProcessId}",
            conflicts?.Count ?? 0, processId);

        // If no conflicts were found, check if the process exists
        if (conflicts == null || conflicts.Count == 0)
        {
          _logger?.LogWarning("No conflicts found for process {ProcessId}", processId);

          // Return an empty array with a message
          return Ok(new
          {
            message = $"No conflicts found for process {processId}",
            conflicts = new List<Conflict>()
          });
        }

        return Ok(conflicts);
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error retrieving conflicts for process {ProcessId}", processId);
        return BadRequest(new { message = ex.Message });
      }
    }



    /// <summary>
    /// Resolves a conflict
    /// </summary>
    /// <param name="conflictId">The conflict ID</param>
    /// <param name="resolution">The resolution details</param>
    /// <returns>Success message</returns>
    [HttpPost("resolve/{conflictId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResolveConflict(string conflictId, [FromBody] ConflictResolutionDto resolution)
    {
      try
      {
        _logger?.LogInformation("Resolving conflict {ConflictId} with resolution: {Resolution}",
            conflictId, resolution?.Resolution);

        if (resolution == null)
        {
          return BadRequest(new { message = "Resolution details are required" });
        }

        // Try to get the conflict first to verify it exists
        try
        {
          var conflict = await _conflictService.GetConflictAsync(conflictId);
          _logger?.LogInformation("Found conflict {ConflictId} with status {Status}",
              conflict.Id, conflict.Status);

          if (conflict.Status == "Resolved")
          {
            return Ok(new
            {
              message = "Conflict is already resolved",
              conflict
            });
          }
        }
        catch (Exception ex)
        {
          _logger?.LogError(ex, "Error finding conflict {ConflictId}", conflictId);

          if (ex.Message.Contains("not found"))
          {
            // Try with different ID formats
            if (!conflictId.StartsWith("Conflicts/"))
            {
              _logger?.LogInformation("Trying with Conflicts/ prefix");
              return await ResolveConflict($"Conflicts/{conflictId}", resolution);
            }
            else if (conflictId.StartsWith("Conflicts/"))
            {
              _logger?.LogInformation("Trying without Conflicts/ prefix");
              return await ResolveConflict(conflictId.Substring("Conflicts/".Length), resolution);
            }

            return NotFound(new { message = ex.Message });
          }
        }

        var resolvedConflict = await _conflictService.ResolveConflictAsync(
            conflictId,
            resolution.Resolution ?? "No resolution provided",
            resolution.ResolvedBy ?? "System");

        return Ok(new
        {
          message = "Conflict resolved successfully",
          conflict = resolvedConflict
        });
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error resolving conflict {ConflictId}", conflictId);

        if (ex.Message.Contains("not found"))
        {
          return NotFound(new { message = ex.Message });
        }

        return BadRequest(new { message = ex.Message });
      }
    }

    /// <summary>
    /// Auto-resolves high-confidence conflicts for a process
    /// </summary>
    /// <param name="processId">The process ID</param>
    /// <param name="threshold">Confidence threshold for auto-resolution (default: 0.95)</param>
    /// <returns>Summary of auto-resolved conflicts</returns>
    [HttpGet("auto-resolve/{processId}")]
    [HttpPost("auto-resolve/{processId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AutoResolveConflicts(string processId, [FromQuery] double threshold = 0.95)
    {
      try
      {
        _logger?.LogInformation("Auto-resolving conflicts for process {ProcessId} with threshold {Threshold}",
            processId, threshold);

        // Get all unresolved conflicts for this process
        var conflicts = await _conflictService.GetConflictsByProcessIdAsync(processId);

        if (conflicts == null || conflicts.Count == 0)
        {
          _logger?.LogWarning("No conflicts found for process {ProcessId}", processId);
          return Ok(new
          {
            success = true,
            message = $"No conflicts found for process {processId}",
            totalConflicts = 0,
            autoResolvedCount = 0,
            remainingConflicts = 0
          });
        }

        var unresolvedConflicts = conflicts.Where(c => c.Status == "Unresolved").ToList();

        if (unresolvedConflicts.Count == 0)
        {
          _logger?.LogInformation("No unresolved conflicts found for process {ProcessId}", processId);
          return Ok(new
          {
            success = true,
            message = $"No unresolved conflicts found for process {processId}",
            totalConflicts = conflicts.Count,
            resolvedConflicts = conflicts.Count,
            autoResolvedCount = 0,
            remainingConflicts = 0
          });
        }

        int autoResolvedCount = 0;

        foreach (var conflict in unresolvedConflicts)
        {
          // Auto-resolve high-confidence matches
          if (conflict.Confidence >= threshold)
          {
            try
            {
              await _conflictService.ResolveConflictAsync(
                  conflict.Id,
                  "Auto-resolved due to high confidence match",
                  "System");

              autoResolvedCount++;
              _logger?.LogInformation("Auto-resolved conflict {ConflictId} with confidence {Confidence}",
                  conflict.Id, conflict.Confidence);
            }
            catch (Exception ex)
            {
              _logger?.LogError(ex, "Error auto-resolving conflict {ConflictId}", conflict.Id);
              // Continue with other conflicts even if one fails
            }
          }
        }

        return Ok(new
        {
          success = true,
          message = $"Auto-resolved {autoResolvedCount} conflicts with confidence >= {threshold}",
          totalConflicts = unresolvedConflicts.Count,
          autoResolvedCount,
          remainingConflicts = unresolvedConflicts.Count - autoResolvedCount
        });
      }
      catch (Exception ex)
      {
        _logger?.LogError(ex, "Error auto-resolving conflicts for process {ProcessId}", processId);

        if (ex.Message.Contains("not found"))
        {
          return NotFound(new { message = ex.Message });
        }

        return BadRequest(new { message = ex.Message });
      }
    }
  }

  public class ConflictResolutionDto
  {
    public string? Resolution { get; set; }
    public string? ResolvedBy { get; set; }
  }
}
