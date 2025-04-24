using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Interfaces;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Logging;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DeduplicationController : ControllerBase
    {
        private readonly IDeduplicationService _deduplicationService;
        private readonly ILogger<DeduplicationController> _logger;

        public DeduplicationController(IDeduplicationService deduplicationService, ILogger<DeduplicationController> logger)
        {
            _deduplicationService = deduplicationService ?? throw new ArgumentNullException(nameof(deduplicationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts a new deduplication process
        /// </summary>
        /// <returns>The created deduplication process</returns>
        [HttpPost("start")]
        [ProducesResponseType(typeof(DeduplicationProcess), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> StartDeduplication()
        {
            try
            {
                var process = await _deduplicationService.StartDeduplicationProcessAsync();
                return Ok(process);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting deduplication process");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Starts a new deduplication process with specific files
        /// </summary>
        /// <param name="request">The deduplication process request</param>
        /// <returns>The created deduplication process</returns>
        [HttpPost("start-process")]
        [ProducesResponseType(typeof(DeduplicationProcess), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> StartProcess([FromBody] DeduplicationProcessDto request)
        {
            try
            {
                var process = await _deduplicationService.StartProcessAsync(request);
                return Ok(process);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting specific deduplication process");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets all deduplication processes
        /// </summary>
        /// <returns>List of all processes</returns>
        [HttpGet("processes")]
        [ProducesResponseType(typeof(List<ProcessDTO>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetAllProcesses()
        {
            try
            {
                var processes = await _deduplicationService.GetAllProcesses();
                return Ok(processes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all processes");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Gets details about a specific deduplication process
        /// </summary>
        /// <param name="processId">The ID of the process to retrieve</param>
        /// <returns>The deduplication process details</returns>
        [HttpGet("process/{processId}")]
        [ProducesResponseType(typeof(DeduplicationProcess), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetProcess(string processId)
        {
            try
            {
                _logger.LogInformation("Getting details for process ID: {ProcessId}", processId);

                if (string.IsNullOrEmpty(processId))
                {
                    return BadRequest(new { message = "Process ID is required" });
                }

                var process = await _deduplicationService.GetProcessAsync(processId);

                // Enhance the response with additional information
                var filesCount = process.FileIds?.Count ?? 0;
                var processedFilesCount = process.Steps?
                    .Where(s => s.Name == "Insertion" && s.Status == "Completed")
                    .SelectMany(s => s.ProcessedFiles ?? new List<string>())
                    .Count() ?? 0;

                var response = new
                {
                    process.Id,
                    process.Name,
                    process.Status,
                    process.CreatedAt,
                    process.ProcessStartDate,
                    process.ProcessEndDate,
                    process.CleanupDate,
                    process.Username,
                    TotalFiles = filesCount,
                    ProcessedFiles = processedFilesCount,
                    process.Steps,
                    process.CompletionNotes
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving process {ProcessId}", processId);

                if (ex.Message.Contains("not found"))
                {
                    return NotFound(new { message = ex.Message });
                }

                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Processes a deduplication
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Success message</returns>
        [HttpPost("process/{processId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ProcessDeduplication(string processId)
        {
            _logger.LogInformation("Starting deduplication process for process ID: {ProcessId}", processId);

            if (string.IsNullOrEmpty(processId))
            {
                return BadRequest(new { message = "Process ID is required" });
            }

            try
            {
                // Get the current process to check its status
                var process = await _deduplicationService.GetProcessAsync(processId);

                // Check if the process can be started
                if (process.Status == "In Processing")
                {
                    _logger.LogWarning("Cannot start process {ProcessId} because it is already running", processId);

                    // Create an exception record
                    var exceptionService = HttpContext.RequestServices.GetService<ExceptionService>();
                    if (exceptionService != null)
                    {
                        await exceptionService.CreateExceptionAsync(
                            processId,
                            "Process Status Conflict",
                            new List<string> { "Cannot start a process that is already running" },
                            0.0,
                            new Dictionary<string, object>
                            {
                                ["attemptedAction"] = "Start",
                                ["currentStatus"] = process.Status,
                                ["timestamp"] = DateTime.UtcNow
                            });
                    }

                    return BadRequest(new
                    {
                        success = false,
                        message = "This process is already running. Please wait for it to complete or pause it first.",
                        currentStatus = process.Status
                    });
                }

                if (process.Status == "Completed" || process.Status == "Cleaned")
                {
                    _logger.LogWarning("Cannot start process {ProcessId} because it is already completed", processId);

                    // Create an exception record
                    var exceptionService = HttpContext.RequestServices.GetService<ExceptionService>();
                    if (exceptionService != null)
                    {
                        await exceptionService.CreateExceptionAsync(
                            processId,
                            "Process Status Conflict",
                            new List<string> { "Cannot start a process that is already completed" },
                            0.0,
                            new Dictionary<string, object>
                            {
                                ["attemptedAction"] = "Start",
                                ["currentStatus"] = process.Status,
                                ["timestamp"] = DateTime.UtcNow
                            });
                    }

                    return BadRequest(new
                    {
                        success = false,
                        message = "This process is already completed and cannot be started again.",
                        currentStatus = process.Status
                    });
                }

                // Process the deduplication
                await _deduplicationService.ProcessDeduplicationAsync(processId);
                _logger.LogInformation("Deduplication process started successfully for process ID: {ProcessId}", processId);
                return Ok(new { message = "Deduplication process started successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deduplication for process ID: {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Pauses a deduplication process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Success message</returns>
        [HttpPost("pause/{processId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> PauseProcess(string processId)
        {
            try
            {
                _logger.LogInformation("Starting pause for process ID: {ProcessId}", processId);

                if (string.IsNullOrEmpty(processId))
                {
                    return BadRequest(new { message = "Process ID is required" });
                }

                // Get the current process to check its status
                DeduplicationProcess process;
                try
                {
                    process = await _deduplicationService.GetProcessAsync(processId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process {ProcessId} not found during pause", processId);
                    return NotFound(new { message = $"Process with ID {processId} not found" });
                }

                await _deduplicationService.PauseProcessAsync(processId);
                return Ok(new
                {
                    message = "Process paused successfully",
                    processId = process.Id,
                    status = "Paused"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when pausing process {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pausing process {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Resumes a paused deduplication process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Success message</returns>
        [HttpPost("resume/{processId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ResumeProcess(string processId)
        {
            try
            {
                _logger.LogInformation("Starting resume for process ID: {ProcessId}", processId);

                if (string.IsNullOrEmpty(processId))
                {
                    return BadRequest(new { message = "Process ID is required" });
                }

                // Get the current process to check its status
                DeduplicationProcess process;
                try
                {
                    process = await _deduplicationService.GetProcessAsync(processId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process {ProcessId} not found during resume", processId);
                    return NotFound(new { message = $"Process with ID {processId} not found" });
                }

                await _deduplicationService.ResumeProcessAsync(processId);
                return Ok(new
                {
                    message = "Process resumed successfully",
                    processId = process.Id,
                    status = "In Processing"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation when resuming process {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resuming process {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cleans up a deduplication process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Success message</returns>
        [HttpPost("cleanup/{processId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CleanupProcess(string processId)
        {
            try
            {
                _logger.LogInformation("Starting cleanup for process ID: {ProcessId}", processId);

                if (string.IsNullOrEmpty(processId))
                {
                    return BadRequest(new { message = "Process ID is required" });
                }

                // Get the current process to check its status
                DeduplicationProcess process;
                try
                {
                    process = await _deduplicationService.GetProcessAsync(processId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Process {ProcessId} not found during cleanup", processId);
                    return NotFound(new { message = $"Process with ID {processId} not found" });
                }

                // Check if the process is in a state that can be cleaned up
                if (process.Status == "Cleaning" || process.Status == "Cleaned")
                {
                    return BadRequest(new { message = $"Process is already in {process.Status} state" });
                }

                await _deduplicationService.CleanupProcessAsync(processId);
                return Ok(new
                {
                    message = "Process cleaned up successfully",
                    processId = process.Id,
                    status = "Cleaned"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up process {ProcessId}", processId);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
