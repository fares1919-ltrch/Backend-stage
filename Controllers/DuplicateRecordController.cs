using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Services;
using Backend.DTOs;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DuplicateRecordController : ControllerBase
    {
        private readonly DuplicateRecordService _duplicateRecordService;
        private readonly IdNormalizationService _idNormalizationService;
        private readonly ApiResponseService _apiResponseService;
        private readonly ILogger<DuplicateRecordController> _logger;

        public DuplicateRecordController(
            DuplicateRecordService duplicateRecordService,
            IdNormalizationService idNormalizationService,
            ApiResponseService apiResponseService,
            ILogger<DuplicateRecordController> logger)
        {
            _duplicateRecordService = duplicateRecordService ?? throw new ArgumentNullException(nameof(duplicateRecordService));
            _idNormalizationService = idNormalizationService ?? throw new ArgumentNullException(nameof(idNormalizationService));
            _apiResponseService = apiResponseService ?? throw new ArgumentNullException(nameof(apiResponseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all duplicate records
        /// </summary>
        /// <returns>List of all duplicate records</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<DuplicateRecordDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetAllRecords()
        {
            try
            {
                _logger.LogInformation("Getting all duplicate records");
                var records = await _duplicateRecordService.GetAllDuplicateRecordsAsync();

                // Convert to DTOs for frontend consumption
                var recordDtos = records.Select(record => new DuplicateRecordDto
                {
                    Id = record.Id,
                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                    ProcessId = record.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                    OriginalFileId = record.OriginalFileId,
                    OriginalFileName = record.OriginalFileName,
                    DetectedDate = record.DetectedDate,
                    Status = record.Status,
                    ConfirmationUser = record.ConfirmationUser,
                    ConfirmationDate = record.ConfirmationDate,
                    Notes = record.Notes,
                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                    {
                        FileId = dup.FileId,
                        FileName = dup.FileName,
                        Confidence = dup.Confidence,
                        PersonId = dup.PersonId
                    }).ToList() ?? new List<DuplicateMatchDto>()
                }).ToList();

                return _apiResponseService.Success(recordDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate records");
                return _apiResponseService.Error("Failed to retrieve duplicate records: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets a specific duplicate record by ID
        /// </summary>
        /// <param name="id">The ID of the duplicate record to retrieve</param>
        /// <returns>The duplicate record</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DuplicateRecordDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetRecord(string id)
        {
            try
            {
                _logger.LogInformation("Getting duplicate record with ID: {RecordId}", id);

                // Normalize the ID to ensure it has the correct prefix
                string normalizedId = _idNormalizationService.NormalizeDuplicateRecordId(id);

                // Try to get the record
                var record = await _duplicateRecordService.GetDuplicateRecordAsync(normalizedId);

                // Convert to DTO for frontend consumption
                var recordDto = new DuplicateRecordDto
                {
                    Id = record.Id,
                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                    ProcessId = record.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                    OriginalFileId = record.OriginalFileId,
                    OriginalFileName = record.OriginalFileName,
                    DetectedDate = record.DetectedDate,
                    Status = record.Status,
                    ConfirmationUser = record.ConfirmationUser,
                    ConfirmationDate = record.ConfirmationDate,
                    Notes = record.Notes,
                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                    {
                        FileId = dup.FileId,
                        FileName = dup.FileName,
                        Confidence = dup.Confidence,
                        PersonId = dup.PersonId
                    }).ToList() ?? new List<DuplicateMatchDto>()
                };

                return _apiResponseService.Success(recordDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate record {RecordId}", id);

                if (ex.Message.Contains("not found"))
                {
                    // Try with different ID formats
                    try
                    {
                        // Get all possible ID variations
                        var idVariations = _idNormalizationService.GetIdVariations(id, "duplicateRecord");

                        // Try each variation
                        foreach (var variation in idVariations.Where(v => v != id))
                        {
                            try
                            {
                                _logger.LogInformation("Trying with ID variation: {VariationId}", variation);
                                var record = await _duplicateRecordService.GetDuplicateRecordAsync(variation);

                                // If we get here, we found the record
                                var recordDto = new DuplicateRecordDto
                                {
                                    Id = record.Id,
                                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                                    ProcessId = record.ProcessId,
                                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                                    OriginalFileId = record.OriginalFileId,
                                    OriginalFileName = record.OriginalFileName,
                                    DetectedDate = record.DetectedDate,
                                    Status = record.Status,
                                    ConfirmationUser = record.ConfirmationUser,
                                    ConfirmationDate = record.ConfirmationDate,
                                    Notes = record.Notes,
                                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                                    {
                                        FileId = dup.FileId,
                                        FileName = dup.FileName,
                                        Confidence = dup.Confidence,
                                        PersonId = dup.PersonId
                                    }).ToList() ?? new List<DuplicateMatchDto>()
                                };

                                return _apiResponseService.Success(recordDto);
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

                    return _apiResponseService.NotFound("Duplicate record not found", "duplicateRecord", id);
                }

                return _apiResponseService.Error("Failed to retrieve duplicate record: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets all duplicate records for a specific process
        /// </summary>
        /// <param name="processId">The ID of the process</param>
        /// <returns>List of duplicate records for the process</returns>
        [HttpGet("process/{processId}")]
        [ProducesResponseType(typeof(List<DuplicateRecordDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetRecordsByProcess(string processId)
        {
            try
            {
                _logger.LogInformation("Getting duplicate records for process: {ProcessId}", processId);

                // Normalize the process ID
                string normalizedProcessId = _idNormalizationService.NormalizeProcessId(processId);

                // Get the records
                var records = await _duplicateRecordService.GetDuplicateRecordsByProcessAsync(normalizedProcessId);

                // Convert to DTOs for frontend consumption
                var recordDtos = records.Select(record => new DuplicateRecordDto
                {
                    Id = record.Id,
                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                    ProcessId = record.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                    OriginalFileId = record.OriginalFileId,
                    OriginalFileName = record.OriginalFileName,
                    DetectedDate = record.DetectedDate,
                    Status = record.Status,
                    ConfirmationUser = record.ConfirmationUser,
                    ConfirmationDate = record.ConfirmationDate,
                    Notes = record.Notes,
                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                    {
                        FileId = dup.FileId,
                        FileName = dup.FileName,
                        Confidence = dup.Confidence,
                        PersonId = dup.PersonId
                    }).ToList() ?? new List<DuplicateMatchDto>()
                }).ToList();

                // If no records were found, return an empty list with a message
                if (recordDtos.Count == 0)
                {
                    _logger.LogInformation("No duplicate records found for process {ProcessId}", processId);
                    return _apiResponseService.Success(recordDtos, $"No duplicate records found for process {processId}");
                }

                return _apiResponseService.Success(recordDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate records for process {ProcessId}", processId);

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
                                return await GetRecordsByProcess(variation);
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

                    // If we get here, we couldn't find any records for any variation of the process ID
                    return _apiResponseService.Success(new List<DuplicateRecordDto>(),
                        $"No duplicate records found for process {processId}");
                }

                return _apiResponseService.Error("Failed to retrieve duplicate records: " + ex.Message);
            }
        }

        /// <summary>
        /// Gets all duplicate records with a specific status
        /// </summary>
        /// <param name="status">The status of the records to retrieve</param>
        /// <returns>List of duplicate records with the specified status</returns>
        [HttpGet("status/{status}")]
        [ProducesResponseType(typeof(List<DuplicateRecordDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetRecordsByStatus(string status)
        {
            try
            {
                _logger.LogInformation("Getting duplicate records with status: {Status}", status);

                // Get the records
                var records = await _duplicateRecordService.GetDuplicatesByStatusAsync(status);

                // Convert to DTOs for frontend consumption
                var recordDtos = records.Select(record => new DuplicateRecordDto
                {
                    Id = record.Id,
                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                    ProcessId = record.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                    OriginalFileId = record.OriginalFileId,
                    OriginalFileName = record.OriginalFileName,
                    DetectedDate = record.DetectedDate,
                    Status = record.Status,
                    ConfirmationUser = record.ConfirmationUser,
                    ConfirmationDate = record.ConfirmationDate,
                    Notes = record.Notes,
                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                    {
                        FileId = dup.FileId,
                        FileName = dup.FileName,
                        Confidence = dup.Confidence,
                        PersonId = dup.PersonId
                    }).ToList() ?? new List<DuplicateMatchDto>()
                }).ToList();

                // If no records were found, return an empty list with a message
                if (recordDtos.Count == 0)
                {
                    _logger.LogInformation("No duplicate records found with status {Status}", status);
                    return _apiResponseService.Success(recordDtos, $"No duplicate records found with status {status}");
                }

                return _apiResponseService.Success(recordDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving duplicate records with status {Status}", status);
                return _apiResponseService.Error("Failed to retrieve duplicate records: " + ex.Message);
            }
        }

        /// <summary>
        /// Confirms a duplicate record
        /// </summary>
        /// <param name="id">The ID of the duplicate record to confirm</param>
        /// <param name="actionDto">Action details including optional notes</param>
        /// <returns>The confirmed duplicate record</returns>
        [HttpPost("{id}/confirm")]
        [ProducesResponseType(typeof(DuplicateRecordDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ConfirmRecord(string id, [FromBody] DuplicateActionDto actionDto = null)
        {
            try
            {
                _logger.LogInformation("Confirming duplicate record with ID: {RecordId}", id);

                // Normalize the ID
                string normalizedId = _idNormalizationService.NormalizeDuplicateRecordId(id);

                // Get the username from the authenticated user
                string username = User.Identity?.Name ?? "system";

                // Get the notes if provided
                string notes = actionDto?.Notes;

                // Confirm the record
                var record = await _duplicateRecordService.ConfirmDuplicateRecordAsync(normalizedId, username, notes);

                // Convert to DTO for frontend consumption
                var recordDto = new DuplicateRecordDto
                {
                    Id = record.Id,
                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                    ProcessId = record.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                    OriginalFileId = record.OriginalFileId,
                    OriginalFileName = record.OriginalFileName,
                    DetectedDate = record.DetectedDate,
                    Status = record.Status,
                    ConfirmationUser = record.ConfirmationUser,
                    ConfirmationDate = record.ConfirmationDate,
                    Notes = record.Notes,
                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                    {
                        FileId = dup.FileId,
                        FileName = dup.FileName,
                        Confidence = dup.Confidence,
                        PersonId = dup.PersonId
                    }).ToList() ?? new List<DuplicateMatchDto>()
                };

                return _apiResponseService.Success(recordDto, "Duplicate record confirmed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming duplicate record {RecordId}", id);

                if (ex.Message.Contains("not found"))
                {
                    // Try with different ID formats
                    try
                    {
                        // Get all possible ID variations
                        var idVariations = _idNormalizationService.GetIdVariations(id, "duplicateRecord");

                        // Try each variation
                        foreach (var variation in idVariations.Where(v => v != id))
                        {
                            try
                            {
                                _logger.LogInformation("Trying with ID variation: {VariationId}", variation);
                                return await ConfirmRecord(variation, actionDto);
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

                    return _apiResponseService.NotFound("Duplicate record not found", "duplicateRecord", id);
                }

                return _apiResponseService.Error("Failed to confirm duplicate record: " + ex.Message);
            }
        }

        /// <summary>
        /// Rejects a duplicate record
        /// </summary>
        /// <param name="id">The ID of the duplicate record to reject</param>
        /// <param name="actionDto">Action details including optional notes</param>
        /// <returns>The rejected duplicate record</returns>
        [HttpPost("{id}/reject")]
        [ProducesResponseType(typeof(DuplicateRecordDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> RejectRecord(string id, [FromBody] DuplicateActionDto actionDto = null)
        {
            try
            {
                _logger.LogInformation("Rejecting duplicate record with ID: {RecordId}", id);

                // Normalize the ID
                string normalizedId = _idNormalizationService.NormalizeDuplicateRecordId(id);

                // Get the username from the authenticated user
                string username = User.Identity?.Name ?? "system";

                // Get the notes if provided
                string notes = actionDto?.Notes;

                // Reject the record
                var record = await _duplicateRecordService.RejectDuplicateRecordAsync(normalizedId, username, notes);

                // Convert to DTO for frontend consumption
                var recordDto = new DuplicateRecordDto
                {
                    Id = record.Id,
                    ShortId = _idNormalizationService.GetShortId(record.Id, "duplicateRecord"),
                    ProcessId = record.ProcessId,
                    ShortProcessId = _idNormalizationService.GetShortId(record.ProcessId, "process"),
                    OriginalFileId = record.OriginalFileId,
                    OriginalFileName = record.OriginalFileName,
                    DetectedDate = record.DetectedDate,
                    Status = record.Status,
                    ConfirmationUser = record.ConfirmationUser,
                    ConfirmationDate = record.ConfirmationDate,
                    Notes = record.Notes,
                    Duplicates = record.Duplicates?.Select(dup => new DuplicateMatchDto
                    {
                        FileId = dup.FileId,
                        FileName = dup.FileName,
                        Confidence = dup.Confidence,
                        PersonId = dup.PersonId
                    }).ToList() ?? new List<DuplicateMatchDto>()
                };

                return _apiResponseService.Success(recordDto, "Duplicate record rejected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting duplicate record {RecordId}", id);

                if (ex.Message.Contains("not found"))
                {
                    // Try with different ID formats
                    try
                    {
                        // Get all possible ID variations
                        var idVariations = _idNormalizationService.GetIdVariations(id, "duplicateRecord");

                        // Try each variation
                        foreach (var variation in idVariations.Where(v => v != id))
                        {
                            try
                            {
                                _logger.LogInformation("Trying with ID variation: {VariationId}", variation);
                                return await RejectRecord(variation, actionDto);
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

                    return _apiResponseService.NotFound("Duplicate record not found", "duplicateRecord", id);
                }

                return _apiResponseService.Error("Failed to reject duplicate record: " + ex.Message);
            }
        }
    }
}
