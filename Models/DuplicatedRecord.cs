using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class DuplicatedRecord
    {
        public string Id { get; set; }
        public string ProcessId { get; set; }
        public string OriginalFileId { get; set; }
        public string OriginalFileName { get; set; }
        public DateTime DetectedDate { get; set; }
        public List<DuplicateMatch> Duplicates { get; set; } = new List<DuplicateMatch>();
        public string Status { get; set; } // "Detected", "Confirmed", "Rejected"
        public string ConfirmationUser { get; set; }
        public DateTime? ConfirmationDate { get; set; }
        public string Notes { get; set; }
    }

    public class DuplicateMatch
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
        public double Confidence { get; set; }
        public string PersonId { get; set; }
    }
}
