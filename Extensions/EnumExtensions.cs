using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Backend.Enums;

namespace Backend.Extensions
{
    /// <summary>
    /// Extension methods for enum types
    /// </summary>
    public static class EnumExtensions
    {
        /// <summary>
        /// Gets the description attribute value for an enum value
        /// </summary>
        /// <param name="value">The enum value</param>
        /// <returns>The description or the enum name if no description is found</returns>
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        /// <summary>
        /// Converts a ProcessStatus enum to its string representation
        /// </summary>
        public static string ToDbString(this ProcessStatus status) => status.GetDescription();

        /// <summary>
        /// Converts a FileStatus enum to its string representation
        /// </summary>
        public static string ToDbString(this FileStatus status) => status.GetDescription();

        /// <summary>
        /// Converts a FileProcessStatus enum to its string representation
        /// </summary>
        public static string ToDbString(this FileProcessStatus status) => status.GetDescription();

        /// <summary>
        /// Converts a DuplicateRecordStatus enum to its string representation
        /// </summary>
        public static string ToDbString(this DuplicateRecordStatus status) => status.GetDescription();

        /// <summary>
        /// Converts a ExceptionStatus enum to its string representation
        /// </summary>
        public static string ToDbString(this ExceptionStatus status) => status.GetDescription();

        /// <summary>
        /// Converts a ConflictStatus enum to its string representation
        /// </summary>
        public static string ToDbString(this ConflictStatus status) => status.GetDescription();

        /// <summary>
        /// Converts a string to a ProcessStatus enum
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>The corresponding enum value or Error if not found</returns>
        public static ProcessStatus ToProcessStatus(this string status)
        {
            foreach (ProcessStatus enumValue in Enum.GetValues(typeof(ProcessStatus)))
            {
                if (enumValue.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    return enumValue;
                }
            }
            return ProcessStatus.Error; // Default to Error if not found
        }

        /// <summary>
        /// Converts a string to a FileStatus enum
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>The corresponding enum value or Uploaded if not found</returns>
        public static FileStatus ToFileStatus(this string status)
        {
            foreach (FileStatus enumValue in Enum.GetValues(typeof(FileStatus)))
            {
                if (enumValue.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    return enumValue;
                }
            }
            return FileStatus.Uploaded; // Default to Uploaded if not found
        }

        /// <summary>
        /// Converts a string to a FileProcessStatus enum
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>The corresponding enum value or Pending if not found</returns>
        public static FileProcessStatus ToFileProcessStatus(this string status)
        {
            foreach (FileProcessStatus enumValue in Enum.GetValues(typeof(FileProcessStatus)))
            {
                if (enumValue.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    return enumValue;
                }
            }
            return FileProcessStatus.Pending; // Default to Pending if not found
        }

        /// <summary>
        /// Converts a string to a DuplicateRecordStatus enum
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>The corresponding enum value or Detected if not found</returns>
        public static DuplicateRecordStatus ToDuplicateRecordStatus(this string status)
        {
            foreach (DuplicateRecordStatus enumValue in Enum.GetValues(typeof(DuplicateRecordStatus)))
            {
                if (enumValue.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    return enumValue;
                }
            }
            return DuplicateRecordStatus.Detected; // Default to Detected if not found
        }

        /// <summary>
        /// Converts a string to an ExceptionStatus enum
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>The corresponding enum value or Pending if not found</returns>
        public static ExceptionStatus ToExceptionStatus(this string status)
        {
            foreach (ExceptionStatus enumValue in Enum.GetValues(typeof(ExceptionStatus)))
            {
                if (enumValue.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    return enumValue;
                }
            }
            return ExceptionStatus.Pending; // Default to Pending if not found
        }

        /// <summary>
        /// Converts a string to a ConflictStatus enum
        /// </summary>
        /// <param name="status">The status string</param>
        /// <returns>The corresponding enum value or Unresolved if not found</returns>
        public static ConflictStatus ToConflictStatus(this string status)
        {
            foreach (ConflictStatus enumValue in Enum.GetValues(typeof(ConflictStatus)))
            {
                if (enumValue.GetDescription().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    return enumValue;
                }
            }
            return ConflictStatus.Unresolved; // Default to Unresolved if not found
        }
    }
}
