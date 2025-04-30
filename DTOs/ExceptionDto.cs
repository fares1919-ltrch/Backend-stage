using System;
using System.Collections.Generic;

namespace Backend.DTOs
{
    /// <summary>
    /// Data transfer object for exception information to be consumed by the frontend
    /// </summary>
    public class ExceptionDto
    {
        /// <summary>
        /// Unique identifier for the exception
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Original ID without prefix, for frontend compatibility
        /// </summary>
        public string ShortId { get; set; }
        
        /// <summary>
        /// ID of the process this exception belongs to
        /// </summary>
        public string ProcessId { get; set; }
        
        /// <summary>
        /// Short process ID without prefix, for frontend compatibility
        /// </summary>
        public string ShortProcessId { get; set; }
        
        /// <summary>
        /// Name of the file that caused the exception
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// List of candidate file names that might be related to the exception
        /// </summary>
        public List<string> CandidateFileNames { get; set; }
        
        /// <summary>
        /// Comparison score related to the exception (if applicable)
        /// </summary>
        public double ComparisonScore { get; set; }
        
        /// <summary>
        /// When the exception was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// When the exception was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
        
        /// <summary>
        /// Current status of the exception (Pending, Resolved, Ignored)
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// Additional metadata about the exception
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Data transfer object for updating an exception's status
    /// </summary>
    public class ExceptionStatusDto
    {
        /// <summary>
        /// New status for the exception (Pending, Resolved, Ignored)
        /// </summary>
        public string Status { get; set; }
    }
}
