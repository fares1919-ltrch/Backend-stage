namespace Backend.Models
{
    public class DetectionResult
    {
        public bool Success { get; set; }
        public int FaceCount { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
    }
}
