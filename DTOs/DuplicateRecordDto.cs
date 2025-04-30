using System;
using System.Collections.Generic;

namespace Backend.DTOs
{
    /// <summary>
    /// Data transfer object for duplicate record information to be consumed by the frontend
    /// </summary>
    public class DuplicateRecordDto
    {
        /// <summary>
        /// Unique identifier for the duplicate record
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Original ID without prefix, for frontend compatibility
        /// </summary>
        public string ShortId { get; set; }
        
        /// <summary>
        /// ID of the process this duplicate record belongs to
        /// </summary>
        public string ProcessId { get; set; }
        
        /// <summary>
        /// Short process ID without prefix, for frontend compatibility
        /// </summary>
        public string ShortProcessId { get; set; }
        
        /// <summary>
        /// ID of the original file that has duplicates
        /// </summary>
        public string OriginalFileId { get; set; }
        
        /// <summary>
        /// Name of the original file that has duplicates
        /// </summary>
        public string OriginalFileName { get; set; }
        
        /// <summary>
        /// When the duplicate was detected
        /// </summary>
        public DateTime DetectedDate { get; set; }
        
        /// <summary>
        /// List of duplicate matches found
        /// </summary>
        public List<DuplicateMatchDto> Duplicates { get; set; } = new List<DuplicateMatchDto>();
        
        /// <summary>
        /// Current status of the duplicate record (Detected, Confirmed, Rejected)
        /// </summary>
        public string Status { get; set; }
        
        /// <summary>
        /// User who confirmed or rejected the duplicate
        /// </summary>
        public string ConfirmationUser { get; set; }
        
        /// <summary>
        /// When the duplicate was confirmed or rejected
        /// </summary>
        public DateTime? ConfirmationDate { get; set; }
        
        /// <summary>
        /// Additional notes about the duplicate
        /// </summary>
        public string Notes { get; set; }
    }

    /// <summary>
    /// Data transfer object for a duplicate match
    /// </summary>
    public class DuplicateMatchDto
    {
        /// <summary>
        /// ID of the duplicate file
        /// </summary>
        public string FileId { get; set; }
        
        /// <summary>
        /// Name of the duplicate file
        /// </summary>
        public string FileName { get; set; }
        
        /// <summary>
        /// Confidence score of the match (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// ID of the person in the T4FACE system
        /// </summary>
        public string PersonId { get; set; }
    }
    
    /// <summary>
    /// Data transfer object for confirming or rejecting a duplicate record
    /// </summary>
    public class DuplicateActionDto
    {
        /// <summary>
        /// Additional notes about the confirmation or rejection
        /// </summary>
        public string Notes { get; set; }
    }
}
