using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Dedup.DTOs
{
    public class DeduplicationProcessDto
    {
         public string Username { get; set; }
        public List<string> Files { get; set; }
    }

}