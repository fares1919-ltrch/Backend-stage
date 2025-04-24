using System;
using System.Collections.Generic;
using Backend.Models;

namespace Backend.Models
{
    public class DeduplicationProcess
    {
        public string Id { get; set; } // ID unique du processus
        public string Name { get; set; } // ID unique du processus
        public string Username { get; set; } // Utilisateur associé au processus
        public string Status { get; set; } // "Ready to Start", "In Processing", "Completed", "Paused", "Error"
        public DateTime CreatedAt { get; set; } // Date de création du processus
        public DateTime? CompletedAt { get; set; }
        public DateTime? ProcessStartDate { get; set; }
        public DateTime? ProcessEndDate { get; set; }
        public string CleanupUsername { get; set; }
        public DateTime? CleanupDate { get; set; }
        public List<DeduplicationFile> Files { get; set; } // Liste des fichiers de déduplication
        public List<ProcessStep> Steps { get; set; } = new List<ProcessStep>();
        public List<string> FileIds { get; set; } = new List<string>();
        public string CreatedBy { get; set; }
        public int FileCount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string CurrentStage { get; set; }
        public string CompletionNotes { get; set; }
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
