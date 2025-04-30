using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Services
{
    /// <summary>
    /// Service for creating standardized API responses
    /// </summary>
    public class ApiResponseService
    {
        /// <summary>
        /// Creates a successful response with data
        /// </summary>
        /// <typeparam name="T">Type of data</typeparam>
        /// <param name="data">Data to include in the response</param>
        /// <param name="message">Optional success message</param>
        /// <returns>ActionResult with standardized response format</returns>
        public ActionResult Success<T>(T data, string message = "Operation completed successfully")
        {
            var response = new
            {
                success = true,
                message,
                data
            };
            
            return new OkObjectResult(response);
        }
        
        /// <summary>
        /// Creates a successful response without data
        /// </summary>
        /// <param name="message">Success message</param>
        /// <returns>ActionResult with standardized response format</returns>
        public ActionResult Success(string message = "Operation completed successfully")
        {
            var response = new
            {
                success = true,
                message
            };
            
            return new OkObjectResult(response);
        }
        
        /// <summary>
        /// Creates an error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="statusCode">HTTP status code</param>
        /// <param name="errors">Optional list of specific errors</param>
        /// <returns>ActionResult with standardized error format</returns>
        public ActionResult Error(string message, int statusCode = 400, List<string> errors = null)
        {
            var response = new
            {
                success = false,
                message,
                errors
            };
            
            return new ObjectResult(response)
            {
                StatusCode = statusCode
            };
        }
        
        /// <summary>
        /// Creates a not found response
        /// </summary>
        /// <param name="message">Not found message</param>
        /// <param name="resourceType">Type of resource that wasn't found</param>
        /// <param name="resourceId">ID of the resource that wasn't found</param>
        /// <returns>ActionResult with standardized not found format</returns>
        public ActionResult NotFound(string message = "Resource not found", string resourceType = null, string resourceId = null)
        {
            var response = new
            {
                success = false,
                message,
                resourceType,
                resourceId
            };
            
            return new NotFoundObjectResult(response);
        }
        
        /// <summary>
        /// Creates a conflict response
        /// </summary>
        /// <param name="message">Conflict message</param>
        /// <param name="conflictDetails">Optional details about the conflict</param>
        /// <returns>ActionResult with standardized conflict format</returns>
        public ActionResult Conflict(string message, object conflictDetails = null)
        {
            var response = new
            {
                success = false,
                message,
                conflictDetails
            };
            
            return new ConflictObjectResult(response);
        }
    }
}
