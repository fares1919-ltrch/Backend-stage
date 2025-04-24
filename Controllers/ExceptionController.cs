using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ExceptionController(ExceptionService exceptionService)
        {
            _exceptionService = exceptionService ?? throw new ArgumentNullException(nameof(exceptionService));
        }

        /// <summary>
        /// Gets all exceptions for a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>List of exceptions</returns>
        [HttpGet("process/{processId}")]
        [ProducesResponseType(typeof(List<DeduplicationException>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetExceptionsByProcess(string processId)
        {
            try
            {
                var exceptions = await _exceptionService.GetExceptionsByProcessIdAsync(processId);
                return Ok(exceptions);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
        public async Task<IActionResult> UpdateExceptionStatus(string exceptionId, [FromBody] ExceptionStatusDto status)
        {
            try
            {
                await _exceptionService.UpdateExceptionStatusAsync(exceptionId, status.Status);
                return Ok(new { message = "Exception status updated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ExceptionStatusDto
    {
        public string Status { get; set; }
    }
}
