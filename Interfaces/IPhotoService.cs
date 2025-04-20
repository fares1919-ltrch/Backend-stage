using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Photo.Interfaces
{
    public interface IPhotoService
    {
      Task<string> SavePhotoAsBase64Async(string filePath); // Save photo to RavenDB and return its ID
    }
}