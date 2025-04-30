using System;
using System.Collections.Generic;
using System.IO;
using Files.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Storage;
using System.Buffers.Text;
using Microsoft.VisualBasic;
using Backend.Data;

namespace Upload.Services

{
    public class UploadService
    {

        private readonly RavenDbContext _dbContext;
        public UploadService(RavenDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        // Individual image processing has been removed
        // This service now only supports tar.gz file processing through the UploadingController
    }
}

