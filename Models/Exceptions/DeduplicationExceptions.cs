using System;

namespace Backend.Models.Exceptions
{
    public class DeduplicationException : Exception
    {
        public string ProcessId { get; }
        
        public DeduplicationException(string message, string processId) 
            : base(message)
        {
            ProcessId = processId;
        }
        
        public DeduplicationException(string message, string processId, Exception innerException) 
            : base(message, innerException)
        {
            ProcessId = processId;
        }
    }

    public class T4FaceApiException : Exception
    {
        public string ApiEndpoint { get; }
        public int? StatusCode { get; }
        
        public T4FaceApiException(string message, string apiEndpoint, int? statusCode = null) 
            : base(message)
        {
            ApiEndpoint = apiEndpoint;
            StatusCode = statusCode;
        }
        
        public T4FaceApiException(string message, string apiEndpoint, int? statusCode, Exception innerException) 
            : base(message, innerException)
        {
            ApiEndpoint = apiEndpoint;
            StatusCode = statusCode;
        }
    }

    public class FileConflictException : Exception
    {
        public string FileName { get; }
        public string ConflictingFileId { get; }
        
        public FileConflictException(string message, string fileName, string conflictingFileId) 
            : base(message)
        {
            FileName = fileName;
            ConflictingFileId = conflictingFileId;
        }
    }
}
