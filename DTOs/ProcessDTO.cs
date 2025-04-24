using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.DTOs
{
    public class ProcessDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Username { get; set; }
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
    }
}

