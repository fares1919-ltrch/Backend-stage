using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace Backend.Data
{
    public class RavenDbContext
    {
        public enum DatabaseType
        {
            Users,
            Files,
            Processes,
            Deduplicated,
            Exceptions,
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

        // Open a synchronous session with an optional database name
        public IDocumentSession OpenSession(string? database = null)
        {
            return Store.OpenSession(database);
        }

        // Open an asynchronous session with an optional database name
        public IAsyncDocumentSession OpenAsyncSession(string? database = null)
        {
            return Store.OpenAsyncSession(database);
        }

        // Open an asynchronous session with a database type
        public IAsyncDocumentSession OpenAsyncSession(DatabaseType databaseType)
        {
            string database = databaseType switch
            {
                DatabaseType.Users => "users",
                DatabaseType.Files => "files",
                DatabaseType.Processes => "processes",
                DatabaseType.Deduplicated => "deduplicated",
                DatabaseType.Exceptions => "exceptions",
                DatabaseType.Conflicts => "conflicts",
                _ => null
            };
            return OpenAsyncSession(database);
        }

        /// <summary>
        /// Executes an operation with concurrency control, automatically retrying on concurrency conflicts
        /// </summary>
        /// <typeparam name="T">The document type</typeparam>
        /// <param name="database">The database name</param>
        /// <param name="documentId">The document ID</param>
        /// <param name="updateFunc">The function to update the document</param>
        /// <param name="maxRetries">Maximum number of retries (default: 3)</param>
        /// <returns>The updated document</returns>
        public async Task<T> ExecuteWithConcurrencyControlAsync<T>(
            string database,
            string documentId,
            Func<IAsyncDocumentSession, T, Task<T>> updateFunc,
            int maxRetries = 3)
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
                        throw new Exception($"Document with ID {documentId} not found");
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
                        throw new Exception($"Failed to update document after {maxRetries} attempts due to concurrency conflicts", ex);
                    }

                    // Wait a bit before retrying
                    await Task.Delay(100 * retryCount);
                }
            }
        }

        /// <summary>
        /// Executes an operation with concurrency control using database type
        /// </summary>
        public async Task<T> ExecuteWithConcurrencyControlAsync<T>(
            DatabaseType databaseType,
            string documentId,
            Func<IAsyncDocumentSession, T, Task<T>> updateFunc,
            int maxRetries = 3)
        {
            string database = databaseType switch
            {
                DatabaseType.Users => "users",
                DatabaseType.Files => "files",
                DatabaseType.Processes => "processes",
                DatabaseType.Deduplicated => "deduplicated",
                DatabaseType.Exceptions => "exceptions",
                DatabaseType.Conflicts => "conflicts",
                _ => null
            };

            return await ExecuteWithConcurrencyControlAsync<T>(database, documentId, updateFunc, maxRetries);
        }

        /// <summary>
        /// Gets the document store for a specific database
        /// </summary>
        /// <param name="databaseName">The name of the database</param>
        /// <returns>The document store configured for the specified database</returns>
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
