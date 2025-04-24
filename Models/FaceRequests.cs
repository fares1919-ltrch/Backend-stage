using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public class VerifyFacesRequest
    {
        [Required]
        public string Base64Image1 { get; set; }

        [Required]
        public string Base64Image2 { get; set; }
    }

    public class DetectFacesRequest
    {
        [Required]
        public string Base64Image { get; set; }
    }

    public class IdentifyFaceRequest
    {
        [Required]
        public string Base64Image { get; set; }
    }
}
