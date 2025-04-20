namespace Dedup.Models
{
    public class DeduplcationFile
    {
        public DeduplcationFile()
        {
            Id = string.Empty;
            ProcessId = string.Empty;
            FilePath = string.Empty;
            Status = "uploaded"; // Default status
        }

        public required string Id { get; set; } // ID unique du fichier
        public required string ProcessId { get; set; } // ID du processus associé
        public required string FilePath { get; set; } // Chemin du fichier temporaire
        public required string Status { get; set; } // Statut du fichier (ex: "uploaded", "processed")
    }
}