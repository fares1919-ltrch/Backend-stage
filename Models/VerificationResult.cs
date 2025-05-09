namespace Backend.Models
{
    public class VerificationResult
    {
        public bool Success { get; set; }
        public bool IsMatch { get; set; }
        public double Confidence { get; set; }
        public string CosineDistance { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
        public string CompareResult { get; set; } // Added to store "HIT" or "NO_HIT" from the API
    }
}
