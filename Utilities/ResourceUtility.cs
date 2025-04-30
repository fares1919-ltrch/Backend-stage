using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Backend.Utilities
{
    /// <summary>
    /// Utility for managing resources
    /// </summary>
    public static class ResourceUtility
    {
        /// <summary>
        /// Executes an action with a disposable resource and ensures proper disposal
        /// </summary>
        /// <typeparam name="TResource">The type of the resource</typeparam>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="resourceFactory">Factory function to create the resource</param>
        /// <param name="action">Action to execute with the resource</param>
        /// <param name="logger">Optional logger for logging errors</param>
        /// <returns>The result of the action</returns>
        public static TResult UseResource<TResource, TResult>(
            Func<TResource> resourceFactory,
            Func<TResource, TResult> action,
            ILogger logger = null) where TResource : IDisposable
        {
            TResource resource = default;
            try
            {
                resource = resourceFactory();
                return action(resource);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing action with resource {ResourceType}", typeof(TResource).Name);
                throw;
            }
            finally
            {
                resource?.Dispose();
            }
        }

        /// <summary>
        /// Executes an async action with a disposable resource and ensures proper disposal
        /// </summary>
        /// <typeparam name="TResource">The type of the resource</typeparam>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="resourceFactory">Factory function to create the resource</param>
        /// <param name="action">Async action to execute with the resource</param>
        /// <param name="logger">Optional logger for logging errors</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task<TResult> UseResourceAsync<TResource, TResult>(
            Func<TResource> resourceFactory,
            Func<TResource, Task<TResult>> action,
            ILogger logger = null) where TResource : IDisposable
        {
            TResource resource = default;
            try
            {
                resource = resourceFactory();
                return await action(resource);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing async action with resource {ResourceType}", typeof(TResource).Name);
                throw;
            }
            finally
            {
                resource?.Dispose();
            }
        }

        /// <summary>
        /// Executes an async action with a disposable resource and ensures proper disposal (void version)
        /// </summary>
        /// <typeparam name="TResource">The type of the resource</typeparam>
        /// <param name="resourceFactory">Factory function to create the resource</param>
        /// <param name="action">Async action to execute with the resource</param>
        /// <param name="logger">Optional logger for logging errors</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task UseResourceAsync<TResource>(
            Func<TResource> resourceFactory,
            Func<TResource, Task> action,
            ILogger logger = null) where TResource : IDisposable
        {
            TResource resource = default;
            try
            {
                resource = resourceFactory();
                await action(resource);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing async action with resource {ResourceType}", typeof(TResource).Name);
                throw;
            }
            finally
            {
                resource?.Dispose();
            }
        }

        /// <summary>
        /// Safely deletes a file if it exists
        /// </summary>
        /// <param name="filePath">The path of the file to delete</param>
        /// <param name="logger">Optional logger for logging errors</param>
        /// <returns>True if the file was deleted or didn't exist, false if an error occurred</returns>
        public static bool SafeDeleteFile(string filePath, ILogger logger = null)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    logger?.LogDebug("Deleted file {FilePath}", filePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error deleting file {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Safely deletes a directory and its contents if it exists
        /// </summary>
        /// <param name="directoryPath">The path of the directory to delete</param>
        /// <param name="recursive">Whether to delete subdirectories and files</param>
        /// <param name="logger">Optional logger for logging errors</param>
        /// <returns>True if the directory was deleted or didn't exist, false if an error occurred</returns>
        public static bool SafeDeleteDirectory(string directoryPath, bool recursive = true, ILogger logger = null)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive);
                    logger?.LogDebug("Deleted directory {DirectoryPath}", directoryPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error deleting directory {DirectoryPath}", directoryPath);
                return false;
            }
        }
    }
}
