using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class RegisterFaceResult
    {
        public bool Success { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
        public string ErrorMessage { get; set; }
    }
}
