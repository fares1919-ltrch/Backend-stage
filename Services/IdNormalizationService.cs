using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Backend.Services
{
    /// <summary>
    /// Service for normalizing and handling document IDs across different collections
    /// </summary>
    public class IdNormalizationService
    {
        private readonly ILogger<IdNormalizationService> _logger;
        
        // Define known prefixes for different document types
        private static readonly Dictionary<string, string> DocumentPrefixes = new Dictionary<string, string>
        {
            { "process", "processes/" },
            { "conflict", "Conflicts/" },
            { "exception", "Exceptions/" },
            { "duplicateRecord", "DuplicatedRecords/" }
        };
        
        public IdNormalizationService(ILogger<IdNormalizationService> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Normalizes a process ID to ensure it has the correct prefix
        /// </summary>
        /// <param name="processId">The process ID to normalize</param>
        /// <returns>Normalized process ID</returns>
        public string NormalizeProcessId(string processId)
        {
            return NormalizeId(processId, "process");
        }
        
        /// <summary>
        /// Normalizes a conflict ID to ensure it has the correct prefix
        /// </summary>
        /// <param name="conflictId">The conflict ID to normalize</param>
        /// <returns>Normalized conflict ID</returns>
        public string NormalizeConflictId(string conflictId)
        {
            return NormalizeId(conflictId, "conflict");
        }
        
        /// <summary>
        /// Normalizes an exception ID to ensure it has the correct prefix
        /// </summary>
        /// <param name="exceptionId">The exception ID to normalize</param>
        /// <returns>Normalized exception ID</returns>
        public string NormalizeExceptionId(string exceptionId)
        {
            return NormalizeId(exceptionId, "exception");
        }
        
        /// <summary>
        /// Normalizes a duplicate record ID to ensure it has the correct prefix
        /// </summary>
        /// <param name="duplicateRecordId">The duplicate record ID to normalize</param>
        /// <returns>Normalized duplicate record ID</returns>
        public string NormalizeDuplicateRecordId(string duplicateRecordId)
        {
            return NormalizeId(duplicateRecordId, "duplicateRecord");
        }
        
        /// <summary>
        /// Normalizes an ID to ensure it has the correct prefix
        /// </summary>
        /// <param name="id">The ID to normalize</param>
        /// <param name="documentType">The type of document (process, conflict, exception, duplicateRecord)</param>
        /// <returns>Normalized ID</returns>
        public string NormalizeId(string id, string documentType)
        {
            if (string.IsNullOrEmpty(id))
            {
                _logger.LogWarning("Attempted to normalize a null or empty ID for document type {DocumentType}", documentType);
                return id;
            }
            
            if (!DocumentPrefixes.TryGetValue(documentType, out string prefix))
            {
                _logger.LogWarning("Unknown document type {DocumentType} for ID normalization", documentType);
                return id;
            }
            
            // If the ID already has the correct prefix, return it as is
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
            
            // Otherwise, add the prefix
            string normalizedId = $"{prefix}{id}";
            _logger.LogInformation("Normalized {DocumentType} ID from {OriginalId} to {NormalizedId}", 
                documentType, id, normalizedId);
            
            return normalizedId;
        }
        
        /// <summary>
        /// Extracts the short ID (without prefix) from a full document ID
        /// </summary>
        /// <param name="id">The full document ID</param>
        /// <param name="documentType">The type of document (process, conflict, exception, duplicateRecord)</param>
        /// <returns>Short ID without prefix</returns>
        public string GetShortId(string id, string documentType)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }
            
            if (!DocumentPrefixes.TryGetValue(documentType, out string prefix))
            {
                _logger.LogWarning("Unknown document type {DocumentType} for ID shortening", documentType);
                return id;
            }
            
            // If the ID has the prefix, remove it
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return id.Substring(prefix.Length);
            }
            
            // Otherwise, return the original ID
            return id;
        }
        
        /// <summary>
        /// Gets all possible variations of an ID (with and without prefix)
        /// </summary>
        /// <param name="id">The ID to get variations for</param>
        /// <param name="documentType">The type of document</param>
        /// <returns>List of possible ID variations</returns>
        public List<string> GetIdVariations(string id, string documentType)
        {
            var variations = new List<string>();
            
            if (string.IsNullOrEmpty(id))
            {
                return variations;
            }
            
            // Add the original ID
            variations.Add(id);
            
            if (!DocumentPrefixes.TryGetValue(documentType, out string prefix))
            {
                return variations;
            }
            
            // Add the ID with prefix if it doesn't have it
            if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                variations.Add($"{prefix}{id}");
            }
            // Add the ID without prefix if it has it
            else if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                variations.Add(id.Substring(prefix.Length));
            }
            
            return variations;
        }
    }
}
