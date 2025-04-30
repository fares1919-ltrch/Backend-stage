using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Models;
using Backend.DTOs;

namespace Backend.Interfaces
{
    public interface IDeduplicationService
    {
        Task<DeduplicationProcess> StartDeduplicationProcessAsync(string username = null);
        Task<DeduplicationProcess> StartProcessAsync(DeduplicationProcessDto request, string username = null);
        Task<List<ProcessDTO>> GetAllProcesses();
        Task<DeduplicationProcess> GetProcessAsync(string processId);
        Task ProcessDeduplicationAsync(string processId);
        Task PauseProcessAsync(string processId);
        Task ResumeProcessAsync(string processId);
        Task CleanupProcessAsync(string processId, string username = null);
    }
}
