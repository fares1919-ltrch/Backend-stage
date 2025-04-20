using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Files.Models
{
    public class FileModel
    {
        public string Id { get; set; }
        public string Base64String { get; set; }

        public bool Photodeduplique { get; set; } = false; // Statut de déduplication

        public string Status { get; set; }

        public string FileName { get; set; }   

        public string? FaceId { get; set; }

        public DateTime CreatedAt { get; set; } // Date de création du fichier

        public DateTime? ProcessStartDate { get; set; } // Date de début du traitement

        public string ProcessStatus { get; set; } = "Pending"; // Statut du traitement (Pending, Processing, Completed, Failed)
    }
}