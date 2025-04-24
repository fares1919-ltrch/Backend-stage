using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Services;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ConflictController : ControllerBase
    {
        private readonly ConflictService _conflictService;

        public ConflictController(ConflictService conflictService)
        {
            _conflictService = conflictService ?? throw new ArgumentNullException(nameof(conflictService));
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
                var conflicts = await _conflictService.GetConflictsByProcessIdAsync(processId);
                return Ok(conflicts);
            }
            catch (Exception ex)
            {
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
        public async Task<IActionResult> ResolveConflict(string conflictId, [FromBody] ConflictResolutionDto resolution)
        {
            try
            {
                await _conflictService.ResolveConflictAsync(conflictId, resolution.Resolution, resolution.ResolvedBy);
                return Ok(new { message = "Conflict resolved successfully" });
            }
            catch (Exception ex)
            {
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
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> AutoResolveConflicts(string processId, [FromQuery] double threshold = 0.95)
        {
            try
            {
                // Get all unresolved conflicts for this process
                var conflicts = await _conflictService.GetConflictsByProcessIdAsync(processId);
                var unresolvedConflicts = conflicts.Where(c => c.Status == "Unresolved").ToList();

                int autoResolvedCount = 0;

                foreach (var conflict in unresolvedConflicts)
                {
                    // Auto-resolve high-confidence matches
                    if (conflict.Confidence >= threshold)
                    {
                        await _conflictService.ResolveConflictAsync(
                            conflict.Id,
                            "Auto-resolved due to high confidence match",
                            "System");

                        autoResolvedCount++;
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"Auto-resolved {autoResolvedCount} conflicts with confidence >= {threshold}",
                    totalConflicts = unresolvedConflicts.Count,
                    autoResolvedCount = autoResolvedCount,
                    remainingConflicts = unresolvedConflicts.Count - autoResolvedCount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ConflictResolutionDto
    {
        public string Resolution { get; set; }
        public string ResolvedBy { get; set; }
    }
}
