using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class DeduplicationException
    {
        public string Id { get; set; }
        public string ProcessId { get; set; }
        public string FileName { get; set; }
        public List<string> CandidateFileNames { get; set; }
        public double ComparisonScore { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Status { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        // Helper method to get a typed value from metadata
        public T GetMetadataValue<T>(string key, T defaultValue = default)
        {
            if (Metadata != null && Metadata.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    // Handle type conversion for common types
                    if (typeof(T) == typeof(string) && value != null)
                    {
                        return (T)(object)value.ToString();
                    }

                    // Handle numeric conversions
                    if (typeof(T) == typeof(int) && value != null)
                    {
                        return (T)(object)Convert.ToInt32(value);
                    }

                    if (typeof(T) == typeof(double) && value != null)
                    {
                        return (T)(object)Convert.ToDouble(value);
                    }

                    if (typeof(T) == typeof(bool) && value != null)
                    {
                        return (T)(object)Convert.ToBoolean(value);
                    }

                    if (typeof(T) == typeof(DateTime) && value != null)
                    {
                        return (T)(object)Convert.ToDateTime(value);
                    }
                }
                catch
                {
                    // If conversion fails, return default
                    return defaultValue;
                }
            }

            return defaultValue;
        }
    }
}
