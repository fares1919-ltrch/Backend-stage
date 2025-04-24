using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class Conflict
    {
        public string Id { get; set; }
        public string ProcessId { get; set; }
        public string FileName { get; set; }
        public string MatchedFileName { get; set; }
        public double Confidence { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ResolvedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string Resolution { get; set; }
    }
}
