using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dedup.Models;
using Dedup.DTOs;
using Process.DTOs;

namespace Dedup.Interfaces
{
    public interface IDeduplicationService
    {
    
        Task<DeduplicationProcess> StartDeduplicationProcessAsync();
        Task<DeduplicationProcess> StartProcessAsync(DeduplicationProcessDto request);

        Task<List<ProcessDTO>> GetAllProcesses();

    }
}