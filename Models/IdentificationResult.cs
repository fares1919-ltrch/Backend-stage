using System.Collections.Generic;

namespace Backend.Models
{
    public class IdentificationResult
    {
        public bool Success { get; set; }
        public bool HasMatches { get; set; }
        public List<IdentificationMatch> Matches { get; set; }
        public string ErrorMessage { get; set; }
        public string RawResponse { get; set; }
    }

    public class IdentificationMatch
    {
        public string PersonId { get; set; }
        public string FaceId { get; set; }
        public double Confidence { get; set; }
        public string Name { get; set; }
    }
}
