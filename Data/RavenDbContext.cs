using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Backend.Data
{
    /// <summary>
    /// Context for RavenDB operations, providing standardized access to databases
    /// </summary>
    public class RavenDbContext
    {
        /// <summary>
        /// Enum representing the different database types in the system
        /// </summary>
        public enum DatabaseType
        {
            /// <summary>User authentication and profile data</summary>
            Users,

            /// <summary>File metadata and content</summary>
            Files,

            /// <summary>Deduplication process data</summary>
            Processes,

            /// <summary>Duplicate record data</summary>
            Deduplicated,

            /// <summary>Exception data</summary>
            Exceptions,

            /// <summary>Conflict data</summary>
            Conflicts
        }

        public IDocumentStore Store { get; }

        public RavenDbContext(IConfiguration configuration)
        {
            // Read values from appsettings.json
            var urls = configuration.GetSection("RavenDb:Urls").Get<string[]>();
            var certificatePath = configuration["RavenDb:CertificatePath"];
            var databaseName = configuration["RavenDb:Database"];

            if (urls == null || certificatePath == null)
            {
                throw new InvalidOperationException("RavenDB configuration is missing in appsettings.json.");
            }

            // Load the X.509 certificate
            var clientCertificate = new X509Certificate2(certificatePath);

            // Initialize the DocumentStore
            Store = new DocumentStore
            {
                Urls = urls,
                Certificate = clientCertificate,
                Database = databaseName,
                Conventions =
                {
                    MaxNumberOfRequestsPerSession = 10,
                    UseOptimisticConcurrency = true,
                    DisposeCertificate = false // Ensure the certificate is not disposed
                }
            };

            // Initialize the DocumentStore
            Store.Initialize();
        }

        /// <summary>
        /// Gets the database name for a database type
        /// </summary>
        /// <param name="databaseType">The database type</param>
        /// <returns>The database name</returns>
        public string GetDatabaseName(DatabaseType databaseType)
        {
            return databaseType switch
            {
                DatabaseType.Users => "users",
                DatabaseType.Files => "files",
                DatabaseType.Processes => "processes",
                DatabaseType.Deduplicated => "deduplicated",
                DatabaseType.Exceptions => "exceptions",
                DatabaseType.Conflicts => "conflicts",
                _ => throw new ArgumentException($"Unknown database type: {databaseType}")
            };
        }

        /// <summary>
        /// Opens a synchronous session with a database type
        /// </summary>
        /// <param name="databaseType">The database type</param>
        /// <returns>A document session</returns>
        public IDocumentSession OpenSession(DatabaseType databaseType)
        {
            string database = GetDatabaseName(databaseType);
            return Store.OpenSession(database);
        }

        /// <summary>
        /// Opens a synchronous session with a database name (legacy method, prefer using DatabaseType)
        /// </summary>
        /// <param name="database">The database name</param>
        /// <returns>A document session</returns>
        [Obsolete("Use OpenSession(DatabaseType) instead")]
        public IDocumentSession OpenSession(string database = null)
        {
            return Store.OpenSession(database);
        }

        /// <summary>
        /// Opens an asynchronous session with a database type
        /// </summary>
        /// <param name="databaseType">The database type</param>
        /// <returns>An asynchronous document session</returns>
        public IAsyncDocumentSession OpenAsyncSession(DatabaseType databaseType)
        {
            string database = GetDatabaseName(databaseType);
            return Store.OpenAsyncSession(database);
        }

        /// <summary>
        /// Opens an asynchronous session with a database name (legacy method, prefer using DatabaseType)
        /// </summary>
        /// <param name="database">The database name</param>
        /// <returns>An asynchronous document session</returns>
        [Obsolete("Use OpenAsyncSession(DatabaseType) instead")]
        public IAsyncDocumentSession OpenAsyncSession(string database = null)
        {
            return Store.OpenAsyncSession(database);
        }

        /// <summary>
        /// Executes an operation with concurrency control, automatically retrying on concurrency conflicts
        /// </summary>
        /// <typeparam name="T">The document type</typeparam>
        /// <param name="databaseType">The database type</param>
        /// <param name="documentId">The document ID</param>
        /// <param name="updateFunc">The function to update the document</param>
        /// <param name="maxRetries">Maximum number of retries (default: 5)</param>
        /// <returns>The updated document</returns>
        public async Task<T> ExecuteWithConcurrencyControlAsync<T>(
            DatabaseType databaseType,
            string documentId,
            Func<IAsyncDocumentSession, T, Task<T>> updateFunc,
            int maxRetries = 5)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    using var session = OpenAsyncSession(databaseType);
                    var document = await session.LoadAsync<T>(documentId);

                    if (document == null)
                    {
                        throw new InvalidOperationException($"Document with ID {documentId} not found in database {databaseType}");
                    }

                    document = await updateFunc(session, document);
                    await session.SaveChangesAsync();

                    return document;
                }
                catch (Raven.Client.Exceptions.ConcurrencyException ex)
                {
                    retryCount++;

                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"Failed to update document {documentId} after {maxRetries} attempts due to concurrency conflicts", ex);
                    }

                    // Wait a bit before retrying with exponential backoff
                    await Task.Delay(100 * (int)Math.Pow(2, retryCount));
                }
            }
        }

        /// <summary>
        /// Executes an operation with concurrency control using database name (legacy method)
        /// </summary>
        /// <typeparam name="T">The document type</typeparam>
        /// <param name="database">The database name</param>
        /// <param name="documentId">The document ID</param>
        /// <param name="updateFunc">The function to update the document</param>
        /// <param name="maxRetries">Maximum number of retries (default: 5)</param>
        /// <returns>The updated document</returns>
        [Obsolete("Use ExecuteWithConcurrencyControlAsync(DatabaseType, ...) instead")]
        public async Task<T> ExecuteWithConcurrencyControlAsync<T>(
            string database,
            string documentId,
            Func<IAsyncDocumentSession, T, Task<T>> updateFunc,
            int maxRetries = 5)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    using var session = OpenAsyncSession(database);
                    var document = await session.LoadAsync<T>(documentId);

                    if (document == null)
                    {
                        throw new InvalidOperationException($"Document with ID {documentId} not found in database {database}");
                    }

                    document = await updateFunc(session, document);
                    await session.SaveChangesAsync();

                    return document;
                }
                catch (Raven.Client.Exceptions.ConcurrencyException ex)
                {
                    retryCount++;

                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"Failed to update document {documentId} after {maxRetries} attempts due to concurrency conflicts", ex);
                    }

                    // Wait a bit before retrying with exponential backoff
                    await Task.Delay(100 * (int)Math.Pow(2, retryCount));
                }
            }
        }

        /// <summary>
        /// Gets the document store for a specific database type
        /// </summary>
        /// <param name="databaseType">The database type</param>
        /// <returns>The document store configured for the specified database</returns>
        public IDocumentStore GetDocumentStore(DatabaseType databaseType)
        {
            string databaseName = GetDatabaseName(databaseType);
            return GetDocumentStore(databaseName);
        }

        /// <summary>
        /// Gets the document store for a specific database name (legacy method)
        /// </summary>
        /// <param name="databaseName">The name of the database</param>
        /// <returns>The document store configured for the specified database</returns>
        [Obsolete("Use GetDocumentStore(DatabaseType) instead")]
        public IDocumentStore GetDocumentStore(string databaseName)
        {
            // Create a new document store with the same configuration but a different database
            var store = new DocumentStore
            {
                Urls = Store.Urls,
                Certificate = Store.Certificate,
                Database = databaseName,
                Conventions = Store.Conventions
            };

            store.Initialize();
            return store;
        }
    }
}
