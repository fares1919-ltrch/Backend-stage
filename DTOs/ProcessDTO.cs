using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Process.DTOs
{
    public class ProcessDTO
    {
        public required string ProcessId { get; set; }
        public required string ProcessDate { get; set; }
        public required string ProcessName { get; set; }
        public int ProcessFiles { get; set; }
    }
}

