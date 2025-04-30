using System;

namespace Backend.Models
{
    public class DeduplicationFile
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Base64String { get; set; }
        public string Status { get; set; } = "Uploaded";
        public DateTime CreatedAt { get; set; }
        public string FaceId { get; set; } = "";
        public DateTime ProcessStartDate { get; set; } = DateTime.MinValue;
        public string ProcessStatus { get; set; } = "Pending";
        public bool Photodeduplique { get; set; } = false;
    }
}
