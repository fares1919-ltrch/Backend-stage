using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Backend.DTOs
{
    public class DeduplicationProcessDto
    {
        public string Name { get; set; }
        public List<string> FileIds { get; set; }
    }
}
