using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Raven.Data;
using Dedup.Interfaces;
using Dedup.Models;
using Dedup.DTOs;
using Files.Models;
using Microsoft.EntityFrameworkCore;
using Process.DTOs;
using Raven.Client.Documents.Session;
using Dedup.Models;

namespace Dedup.Services
{
    public class DeduplicationService : IDeduplicationService
    {
        private readonly RavenDbContext _context;
        private readonly string _tempFilePath; // Chemin du dossier temporaire pour stocker les fichiers

        public DeduplicationService(RavenDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _tempFilePath = Path.Combine(Directory.GetCurrentDirectory(), "TempFiles"); // Dossier temporaire
            Directory.CreateDirectory(_tempFilePath); // Créer le dossier s'il n'existe pas
        }

        public async Task<DeduplicationProcess> StartDeduplicationProcessAsync()
        {
            Console.WriteLine("=== DÉBUT DU PROCESSUS DE DÉDUPLICATION ===");
            
            // Créer un nouveau processus de déduplication
            var process = new DeduplicationProcess
            {
                Id = $"DeduplicationProcesses/{Guid.NewGuid()}",
                Name = $"Process-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                Username = "tass",
                Status = "Processing",
                CreatedAt = DateTime.UtcNow,
                Files = new List<DeduplcationFile>()
            };

            Console.WriteLine($"✅ Processus créé - ID: {process.Id}");
            Console.WriteLine($"📝 Nom du processus: {process.Name}");

            string tempImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "TempImages");
            Console.WriteLine($"📁 Dossier temporaire: {tempImagesPath}");

            try
            {
                // Vérifier si le dossier TempImages existe
                if (!Directory.Exists(tempImagesPath))
                {
                    throw new DirectoryNotFoundException("❌ Le dossier TempImages n'existe pas");
                }

                var files = Directory.GetFiles(tempImagesPath);
                Console.WriteLine($"📊 Nombre de fichiers à traiter: {files.Length}");

                // Première étape : Sauvegarder les fichiers dans la base Files
                using (var filesSession = _context.OpenAsyncSession(database: "Files"))
                {
                    foreach (var filePath in files)
                    {
                        try
                        {
                            Console.WriteLine($"\n🔄 Traitement du fichier: {Path.GetFileName(filePath)}");
                            
                            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                            string base64String = Convert.ToBase64String(fileBytes);
                            string fileName = Path.GetFileName(filePath);

                            var fileModel = new FileModel
                            {
                                Id = $"Files/{Guid.NewGuid()}",
                                Base64String = base64String,
                                Photodeduplique = false,
                                Status = "Inserted",
                                FileName = Path.GetFileNameWithoutExtension(fileName),
                                CreatedAt = DateTime.UtcNow,
                                ProcessStartDate = DateTime.UtcNow,
                                ProcessStatus = "Processing"
                            };

                            await filesSession.StoreAsync(fileModel);
                            Console.WriteLine($"💾 FileModel sauvegardé - ID: {fileModel.Id}");

                            // Créer la référence pour le processus
                            var dedupFile = new DeduplcationFile
                            {
                                Id = $"DeduplcationFiles/{Guid.NewGuid()}",
                                ProcessId = process.Id,
                                FilePath = filePath,
                                Status = "Processing"
                            };

                            process.Files.Add(dedupFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Erreur lors du traitement du fichier {filePath}:");
                            Console.WriteLine($"   Message: {ex.Message}");
                            throw;
                        }
                    }

                    await filesSession.SaveChangesAsync();
                    Console.WriteLine("✅ Tous les fichiers ont été sauvegardés dans la base Files");
                }

                // Deuxième étape : Sauvegarder le processus et ses références dans DeduplicationProcess
                using (var dedupSession = _context.OpenAsyncSession(database: "DeduplicationProcess"))
                {
                    await dedupSession.StoreAsync(process);
                    
                    // Sauvegarder chaque DeduplcationFile
                    foreach (var dedupFile in process.Files)
                    {
                        await dedupSession.StoreAsync(dedupFile);
                    }

                    await dedupSession.SaveChangesAsync();
                    Console.WriteLine("✅ Processus et références sauvegardés dans DeduplicationProcess");

                    // Nettoyer les fichiers temporaires
                    foreach (var filePath in files)
                    {
                        File.Delete(filePath);
                    }
                    Console.WriteLine("🗑️ Fichiers temporaires supprimés");
                }

                Console.WriteLine("\n=== PROCESSUS TERMINÉ AVEC SUCCÈS ===");
                return process;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERREUR CRITIQUE DANS LE PROCESSUS:");
                Console.WriteLine($"   Message: {ex.Message}");
                throw;
            }
        }

        public Task<DeduplicationProcess> StartProcessAsync(DeduplicationProcessDto request)
        {
            throw new NotImplementedException();
        }

        public async Task<List<ProcessDTO>> GetAllProcesses()
        {
            try
            {
                using (var session = _context.OpenSession(database: "DeduplicationProcess"))
                {
                    // Query all processes
                    var processes = session.Query<DeduplicationProcess>()
                                        .Select(p => new ProcessDTO
                                        {
                                            ProcessId = p.Id,
                                            ProcessDate = p.CreatedAt.ToString("yyyy-MM-dd"),
                                            ProcessName = p.Name,
                                            ProcessFiles = p.Files != null ? p.Files.Count : 0 // Safe null handling without null propagation
                                        })
                                        .ToList();

                    return processes;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving processes: {ex.Message}");
                throw new ApplicationException("Failed to retrieve deduplication processes", ex);
            }
        }
    }
}

