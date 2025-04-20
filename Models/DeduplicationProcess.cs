using System;
using System.Collections.Generic;
using Dedup.Models;

namespace Dedup.Models
{
    public class DeduplicationProcess
    {
    public string Id { get; set; } // ID unique du processus
    public string Name { get; set; } // ID unique du processus
    public string Username { get; set; } // Utilisateur associé au processus
    public string Status { get; set; } // Statut du processus (ex: "uploading", "completed")
    public DateTime CreatedAt { get; set; } // Date de création du processus
    public List<DeduplcationFile> Files { get; set; } // Liste des fichiers de déduplication
    }
}