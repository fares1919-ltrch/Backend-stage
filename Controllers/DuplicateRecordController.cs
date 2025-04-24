using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Services;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DuplicateRecordController : ControllerBase
    {
        private readonly DuplicateRecordService _duplicateRecordService;
        private readonly ILogger<DuplicateRecordController> _logger;

        public DuplicateRecordController(
            DuplicateRecordService duplicateRecordService,
            ILogger<DuplicateRecordController> logger)
        {
            _duplicateRecordService = duplicateRecordService ?? throw new ArgumentNullException(nameof(duplicateRecordService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all duplicate records
        /// </summary>
        /// <returns>List of all duplicate records</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<DuplicatedRecord>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetAllRecords()
        {
            try
            {
                var records = await _duplicateRecordService.GetAllDuplicateRecordsAsync();
                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate records");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets a specific duplicate record by ID
        /// </summary>
        /// <param name="id">The ID of the duplicate record to retrieve</param>
        /// <returns>The duplicate record</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DuplicatedRecord), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetRecord(string id)
        {
            try
            {
                var record = await _duplicateRecordService.GetDuplicateRecordAsync(id);
                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate record {RecordId}", id);

                if (ex.Message.Contains("not found"))
                {
                    return NotFound(new { message = ex.Message });
                }

                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets all duplicate records for a specific process
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <returns>List of duplicate records for the process</returns>
        [HttpGet("process/{processId}")]
        [ProducesResponseType(typeof(List<DuplicatedRecord>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetRecordsByProcess(string processId)
        {
            try
            {
                var records = await _duplicateRecordService.GetDuplicateRecordsByProcessAsync(processId);
                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate records for process {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets all duplicate records with a specific status
        /// </summary>
        /// <param name="status">The status of the records to retrieve</param>
        /// <returns>List of duplicate records with the specified status</returns>
        [HttpGet("status/{status}")]
        [ProducesResponseType(typeof(List<DuplicatedRecord>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetRecordsByStatus(string status)
        {
            try
            {
                var records = await _duplicateRecordService.GetDuplicatesByStatusAsync(status);
                return Ok(records);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate records with status {Status}", status);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Confirms a duplicate record
        /// </summary>
        /// <param name="id">The ID of the duplicate record to confirm</param>
        /// <param name="notes">Optional notes about the confirmation</param>
        /// <returns>The confirmed duplicate record</returns>
        [HttpPost("{id}/confirm")]
        [ProducesResponseType(typeof(DuplicatedRecord), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ConfirmRecord(string id, [FromBody] string notes = null)
        {
            try
            {
                // Get the username from the authenticated user (in a real system)
                string username = User.Identity.Name ?? "system";

                var record = await _duplicateRecordService.ConfirmDuplicateRecordAsync(id, username, notes);
                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming duplicate record {RecordId}", id);

                if (ex.Message.Contains("not found"))
                {
                    return NotFound(new { message = ex.Message });
                }

                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Rejects a duplicate record
        /// </summary>
        /// <param name="id">The ID of the duplicate record to reject</param>
        /// <param name="notes">Optional notes about the rejection</param>
        /// <returns>The rejected duplicate record</returns>
        [HttpPost("{id}/reject")]
        [ProducesResponseType(typeof(DuplicatedRecord), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> RejectRecord(string id, [FromBody] string notes = null)
        {
            try
            {
                // Get the username from the authenticated user (in a real system)
                string username = User.Identity.Name ?? "system";

                var record = await _duplicateRecordService.RejectDuplicateRecordAsync(id, username, notes);
                return Ok(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting duplicate record {RecordId}", id);

                if (ex.Message.Contains("not found"))
                {
                    return NotFound(new { message = ex.Message });
                }

                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
