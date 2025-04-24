using System.Threading.Tasks;
using Backend.Models;
using Backend.Services;

namespace Backend.Interfaces
{
    public interface IT4FaceService
    {
        Task<VerificationResult> VerifyFacesAsync(string base64Image1, string base64Image2);
        Task<DetectionResult> DetectFacesAsync(string base64Image);
        Task<IdentificationResult> IdentifyFaceAsync(string base64Image);
        Task<RegisterFaceResult> RegisterFaceAsync(string name, string base64Image);
    }
}
