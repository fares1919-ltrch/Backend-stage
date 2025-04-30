using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Models
{
    public class DeduplicationProcess
    {
        public string Id { get; set; } // Unique process ID
        public string Name { get; set; } // Process name
        public string Username { get; set; } // Associated user
        public string Status { get; set; } // "Ready to Start", "In Processing", "Completed", "Paused", "Error"
        public DateTime CreatedAt { get; set; } // Process creation date
        public DateTime? CompletedAt { get; set; } // When the process was completed
        public DateTime? ProcessStartDate { get; set; } // When processing started
        public DateTime? ProcessEndDate { get; set; } // When processing ended
        public string CleanupUsername { get; set; } // User who performed cleanup
        public DateTime? CleanupDate { get; set; } // When cleanup was performed
        public List<DeduplicationFile> Files { get; set; } // List of deduplication files
        public List<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
        public List<string> FileIds { get; set; } = new List<string>();
        public string CreatedBy { get; set; } // Email of user who created the process
        public int FileCount { get; set; } // Total number of files
        public int ProcessedFiles { get; set; } // Count of processed files
        public string CurrentStage { get; set; } // Current stage of the process
        public string CompletionNotes { get; set; } // Notes about process completion
    }

    public class ProcessStep
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProcessId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
        public List<string> ProcessedFiles { get; set; } = new List<string>();
    }
}
