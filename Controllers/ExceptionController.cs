using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Backend.Services;
using Backend.Models;
using Backend.DTOs;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ExceptionController : ControllerBase
    {
        private readonly ExceptionService _exceptionService;
        private readonly IdNormalizationService _idNormalizationService;
        private readonly ApiResponseService _apiResponseService;
        private readonly ILogger<ExceptionController> _logger;

        public ExceptionController(
            ExceptionService exceptionService,
            IdNormalizationService idNormalizationService,
            ApiResponseService apiResponseService,
            ILogger<ExceptionController> logger)
        {
            _exceptionService = exceptionService ?? throw new ArgumentNullException(nameof(exceptionService));
            _idNormalizationService = idNormalizationService ?? throw new ArgumentNullException(nameof(idNormalizationService));
            _apiResponseService = apiResponseService ?? throw new ArgumentNullException(nameof(apiResponseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all exceptions for a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>List of exceptions</returns>
        [HttpGet("process/{processId}")]
        [ProducesResponseType(typeof(List<ExceptionDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetExceptionsByProcess(string processId)
        {
            try
            {
                _logger.LogInformation("Getting exceptions for process: {ProcessId}", processId);

                // Normalize the process ID
                string normalizedProcessId = _idNormalizationService.NormalizeProcessId(processId);

                // Get the exceptions
                var exceptions = await _exceptionService.GetExceptionsByProcessIdAsync(normalizedProcessId);

                // Convert to DTOs for frontend consumption
                var exceptionDtos = exceptions.Select(exception => new ExceptionDto
                {
                    Id = exception.Id,
                    ShortId = _idNormalizationService.GetShortId(exception.Id, "exception"),
                    ProcessId = exception.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(exception.ProcessId, "process"),
                    FileName = exception.FileName,
                    CandidateFileNames = exception.CandidateFileNames,
                    ComparisonScore = exception.ComparisonScore,
                    CreatedAt = exception.CreatedAt,
                    UpdatedAt = exception.UpdatedAt,
                    Status = exception.Status,
                    Metadata = exception.Metadata
                }).ToList();

                // If no exceptions were found, return an empty list with a message
                if (exceptionDtos.Count == 0)
                {
                    _logger.LogInformation("No exceptions found for process {ProcessId}", processId);
                    return _apiResponseService.Success(exceptionDtos, $"No exceptions found for process {processId}");
                }

                return _apiResponseService.Success(exceptionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving exceptions for process {ProcessId}", processId);

                // Try with different process ID formats if the error is about not finding the process
                if (ex.Message.Contains("not found"))
                {
                    try
                    {
                        // Get all possible ID variations
                        var idVariations = _idNormalizationService.GetIdVariations(processId, "process");

                        // Try each variation
                        foreach (var variation in idVariations.Where(v => v != processId))
                        {
                            try
                            {
                                _logger.LogInformation("Trying with process ID variation: {VariationId}", variation);
                                return await GetExceptionsByProcess(variation);
                            }
                            catch
                            {
                                // Continue to the next variation
                            }
                        }
                    }
                    catch
                    {
                        // If all variations fail, return an empty list
                    }

                    // If we get here, we couldn't find any exceptions for any variation of the process ID
                    return _apiResponseService.Success(new List<ExceptionDto>(),
                        $"No exceptions found for process {processId}");
                }

                return _apiResponseService.Error("Failed to retrieve exceptions: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets all exceptions in the system
        /// </summary>
        /// <returns>List of all exceptions</returns>
        [HttpGet("all")]
        [ProducesResponseType(typeof(List<ExceptionDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetAllExceptions()
        {
            try
            {
                _logger.LogInformation("Getting all exceptions");
                var exceptions = await _exceptionService.GetAllExceptionsAsync();

                // Convert to DTOs for frontend consumption
                var exceptionDtos = exceptions.Select(exception => new ExceptionDto
                {
                    Id = exception.Id,
                    ShortId = _idNormalizationService.GetShortId(exception.Id, "exception"),
                    ProcessId = exception.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(exception.ProcessId, "process"),
                    FileName = exception.FileName,
                    CandidateFileNames = exception.CandidateFileNames,
                    ComparisonScore = exception.ComparisonScore,
                    CreatedAt = exception.CreatedAt,
                    UpdatedAt = exception.UpdatedAt,
                    Status = exception.Status,
                    Metadata = exception.Metadata
                }).ToList();

                return _apiResponseService.Success(exceptionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all exceptions");
                return _apiResponseService.Error("Failed to retrieve exceptions: " + ex.Message);
            }
        }

        /// <summary>
        /// Updates the status of an exception
        /// </summary>
        /// <param name="exceptionId">The exception ID</param>
        /// <param name="status">The new status</param>
        /// <returns>Success message</returns>
        [HttpPut("status/{exceptionId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateExceptionStatus(string exceptionId, [FromBody] ExceptionStatusDto status)
        {
            try
            {
                _logger.LogInformation("Updating exception status for exception ID: {ExceptionId} to {Status}",
                    exceptionId, status?.Status);

                if (status == null || string.IsNullOrEmpty(status.Status))
                {
                    return _apiResponseService.Error("Status is required");
                }

                // Normalize the exception ID
                string normalizedId = _idNormalizationService.NormalizeExceptionId(exceptionId);

                // Update the status
                await _exceptionService.UpdateExceptionStatusAsync(normalizedId, status.Status);

                return _apiResponseService.Success($"Exception status updated to {status.Status} successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating exception status for exception {ExceptionId}", exceptionId);

                // Try with different exception ID formats if the error is about not finding the exception
                if (ex.Message.Contains("not found"))
                {
                    try
                    {
                        // Get all possible ID variations
                        var idVariations = _idNormalizationService.GetIdVariations(exceptionId, "exception");

                        // Try each variation
                        foreach (var variation in idVariations.Where(v => v != exceptionId))
                        {
                            try
                            {
                                _logger.LogInformation("Trying with exception ID variation: {VariationId}", variation);
                                return await UpdateExceptionStatus(variation, status);
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

                    return _apiResponseService.NotFound("Exception not found", "exception", exceptionId);
                }

                return _apiResponseService.Error("Failed to update exception status: " + ex.Message);
            }
        }
    }
}
