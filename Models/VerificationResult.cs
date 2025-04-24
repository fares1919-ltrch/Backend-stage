namespace Backend.Models
{
    public class VerificationResult
    {
        public bool Success { get; set; }
        public bool IsMatch { get; set; }
        public double Confidence { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
    }
}
