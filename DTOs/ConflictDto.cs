using System;
using System.Collections.Generic;

namespace Backend.DTOs
{
    /// <summary>
    /// Data transfer object for Conflict information to be consumed by the frontend
    /// </summary>
    public class ConflictDto
    {
        /// <summary>
        /// Unique identifier for the conflict
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Original ID without prefix, for frontend compatibility
        /// </summary>
        public string ShortId { get; set; }
        
        /// <summary>
        /// ID of the process this conflict belongs to
        /// </summary>
        public string ProcessId { get; set; }
        
        /// <summary>
        /// Short process ID without prefix, for frontend compatibility
        /// </summary>
        public string ShortProcessId { get; set; }
        
        /// <summary>
        /// Name of the file that has a conflict
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// Name of the file that matched/conflicted with the original file
        /// </summary>
        public string MatchedFileName { get; set; }
        
        /// <summary>
        /// Confidence score of the match (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Current status of the conflict (Unresolved, Resolved)
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// When the conflict was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// User who resolved the conflict (if resolved)
        /// </summary>
        public string ResolvedBy { get; set; }
        
        /// <summary>
        /// When the conflict was resolved (if resolved)
        /// </summary>
        public DateTime? ResolvedAt { get; set; }
        
        /// <summary>
        /// Resolution details or decision made
        /// </summary>
        public string Resolution { get; set; }
    }

    /// <summary>
    /// Data transfer object for conflict resolution request
    /// </summary>
    public class ConflictResolutionDto
    {
        /// <summary>
        /// Resolution details or decision made
        /// </summary>
        public string Resolution { get; set; }
        
        /// <summary>
        /// User who resolved the conflict
        /// </summary>
        public string ResolvedBy { get; set; }
    }
    
    /// <summary>
    /// Response for auto-resolution of conflicts
    /// </summary>
    public class AutoResolveResponseDto
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// Total number of conflicts found
        /// </summary>
        public int TotalConflicts { get; set; }
        
        /// <summary>
        /// Number of conflicts that were auto-resolved
        /// </summary>
        public int AutoResolvedCount { get; set; }
        
        /// <summary>
        /// Number of conflicts that still need manual resolution
        /// </summary>
        public int RemainingConflicts { get; set; }
    }
}
