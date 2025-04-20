using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Photo.DTOs
{
    public class PhotoDto
    {
        public required string filePath { get; set; } // Path to the photo file
    }

    public class Retrieve
    {
        public required string Id { get; set; }
    }
}