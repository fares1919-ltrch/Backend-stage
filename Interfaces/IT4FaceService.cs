using System.Threading.Tasks;
using Backend.Models;
using Backend.Services;

namespace Backend.Interfaces
{
  public interface IT4FaceService
  {

    Task<VerificationResult> VerifyFaceAgainstPersonAsync(string base64Image, string personName);

    Task<IdentificationResult> IdentifyFaceAsync(string base64Image);
    Task<RegisterFaceResult> RegisterFaceAsync(string name, string base64Image);
  }
}
