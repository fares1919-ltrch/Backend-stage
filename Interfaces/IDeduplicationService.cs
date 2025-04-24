using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Models;
using Backend.DTOs;

namespace Backend.Interfaces
{
    public interface IDeduplicationService
    {
        Task<DeduplicationProcess> StartDeduplicationProcessAsync();
        Task<DeduplicationProcess> StartProcessAsync(DeduplicationProcessDto request);
        Task<List<ProcessDTO>> GetAllProcesses();
        Task<DeduplicationProcess> GetProcessAsync(string processId);
        Task ProcessDeduplicationAsync(string processId);
        Task PauseProcessAsync(string processId);
        Task ResumeProcessAsync(string processId);
        Task CleanupProcessAsync(string processId);
    }
}
