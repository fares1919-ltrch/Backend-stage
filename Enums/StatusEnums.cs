using System;
using System.ComponentModel;

namespace Backend.Enums
{
    /// <summary>
    /// Represents the status of a deduplication process
    /// </summary>
    public enum ProcessStatus
    {
        [Description("Ready to Start")]
        ReadyToStart,
        
        [Description("In Processing")]
        InProcessing,
        
        [Description("Completed")]
        Completed,
        
        [Description("Paused")]
        Paused,
        
        [Description("Error")]
        Error,
        
        [Description("Conflict Detected")]
        ConflictDetected,
        
        [Description("Cleaning")]
        Cleaning,
        
        [Description("Cleaned")]
        Cleaned
    }

    /// <summary>
    /// Represents the status of a file
    /// </summary>
    public enum FileStatus
    {
        [Description("Uploaded")]
        Uploaded,
        
        [Description("Inserted")]
        Inserted,
        
        [Description("Conflict")]
        Conflict,
        
        [Description("Deleted")]
        Deleted
    }

    /// <summary>
    /// Represents the processing status of a file
    /// </summary>
    public enum FileProcessStatus
    {
        [Description("Pending")]
        Pending,
        
        [Description("Processing")]
        Processing,
        
        [Description("Completed")]
        Completed,
        
        [Description("Failed")]
        Failed
    }

    /// <summary>
    /// Represents the status of a duplicate record
    /// </summary>
    public enum DuplicateRecordStatus
    {
        [Description("Detected")]
        Detected,
        
        [Description("Confirmed")]
        Confirmed,
        
        [Description("Rejected")]
        Rejected
    }

    /// <summary>
    /// Represents the status of an exception
    /// </summary>
    public enum ExceptionStatus
    {
        [Description("Pending")]
        Pending,
        
        [Description("Reviewed")]
        Reviewed,
        
        [Description("Confirmed")]
        Confirmed,
        
        [Description("Rejected")]
        Rejected,
        
        [Description("Resolved")]
        Resolved,
        
        [Description("Ignored")]
        Ignored
    }

    /// <summary>
    /// Represents the status of a conflict
    /// </summary>
    public enum ConflictStatus
    {
        [Description("Unresolved")]
        Unresolved,
        
        [Description("Resolved")]
        Resolved
    }
}
