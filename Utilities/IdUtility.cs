using System;
using System.Text.RegularExpressions;

namespace Backend.Utilities
{
    /// <summary>
    /// Utility for handling document IDs
    /// </summary>
    public static class IdUtility
    {
        private static readonly Regex IdRegex = new Regex(@"^([a-zA-Z]+)/([a-zA-Z0-9-]+)$", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes an ID to ensure it has the correct prefix
        /// </summary>
        /// <param name="id">The ID to normalize</param>
        /// <param name="prefix">The prefix to ensure</param>
        /// <returns>The normalized ID</returns>
        public static string NormalizeId(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("ID cannot be null or empty", nameof(id));
            }

            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));
            }

            // Ensure prefix ends with "/"
            if (!prefix.EndsWith("/"))
            {
                prefix += "/";
            }

            // If ID already has the correct prefix, return it as is
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }

            // If ID has a different prefix, extract the ID part and add the correct prefix
            var match = IdRegex.Match(id);
            if (match.Success)
            {
                return $"{prefix}{match.Groups[2].Value}";
            }

            // If ID has no prefix, add the prefix
            return $"{prefix}{id}";
        }

        /// <summary>
        /// Creates a new ID with the specified prefix
        /// </summary>
        /// <param name="prefix">The prefix for the ID</param>
        /// <returns>A new ID with the specified prefix</returns>
        public static string CreateNewId(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                throw new ArgumentException("Prefix cannot be null or empty", nameof(prefix));
            }

            // Ensure prefix ends with "/"
            if (!prefix.EndsWith("/"))
            {
                prefix += "/";
            }

            return $"{prefix}{Guid.NewGuid()}";
        }

        /// <summary>
        /// Extracts the ID part from a prefixed ID
        /// </summary>
        /// <param name="id">The prefixed ID</param>
        /// <returns>The ID part without the prefix</returns>
        public static string ExtractIdPart(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("ID cannot be null or empty", nameof(id));
            }

            var match = IdRegex.Match(id);
            if (match.Success)
            {
                return match.Groups[2].Value;
            }

            return id;
        }

        /// <summary>
        /// Gets the prefix part from a prefixed ID
        /// </summary>
        /// <param name="id">The prefixed ID</param>
        /// <returns>The prefix part with the trailing slash</returns>
        public static string ExtractPrefixPart(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("ID cannot be null or empty", nameof(id));
            }

            var match = IdRegex.Match(id);
            if (match.Success)
            {
                return $"{match.Groups[1].Value}/";
            }

            return string.Empty;
        }

        /// <summary>
        /// Checks if an ID has a specific prefix
        /// </summary>
        /// <param name="id">The ID to check</param>
        /// <param name="prefix">The prefix to check for</param>
        /// <returns>True if the ID has the specified prefix, false otherwise</returns>
        public static bool HasPrefix(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                return false;
            }

            // Ensure prefix ends with "/"
            if (!prefix.EndsWith("/"))
            {
                prefix += "/";
            }

            return id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
