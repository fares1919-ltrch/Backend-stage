using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Upload.Services;

namespace upp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UploadingController : ControllerBase
    {
        private readonly UploadService _uploadService;

        public UploadingController(UploadService uploadService)
        {
            _uploadService = uploadService;
        }

       

        [HttpPost("clear-temp")]
        public IActionResult ClearTempFolder()
        {
            string tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "TempImages");

            if (Directory.Exists(tempFolderPath))
            {
                foreach (var file in Directory.GetFiles(tempFolderPath))
                {
                    System.IO.File.Delete(file);
                }
            }

            return Ok(new { message = "Temp folder cleared successfully." });
        }



        [HttpPost("upload")]
        public async Task<IActionResult> UploadImage(IFormFile image)
            {
            

                try
            {
                var base64 = await _uploadService.ProcessImageAsync(image);
                return Ok(new { 
                    success = true,
                    base64 = base64,
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new {
                    success = false,
                    message = ex.Message
                });
            }
            }


    }
}
