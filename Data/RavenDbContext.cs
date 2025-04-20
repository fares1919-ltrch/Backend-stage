using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using System.Security.Cryptography.X509Certificates;

namespace Raven.Data
{
    public class RavenDbContext
    {
        public IDocumentStore Store { get; }

        public RavenDbContext(IConfiguration configuration)
        {
            // Read values from appsettings.json
            var urls = configuration.GetSection("RavenDb:Urls").Get<string[]>();
            var certificatePath = configuration["RavenDb:CertificatePath"];
            var databaseName = configuration["RavenDb:DatabaseName"]; // Optional: Default database name

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
                Database = databaseName, // Set the default database name
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
        public IDocumentSession OpenSession(string database = null)
        {
            return Store.OpenSession(database);
        }

        // Open an asynchronous session with an optional database name
        public IAsyncDocumentSession OpenAsyncSession(string database = null)
        {
            return Store.OpenAsyncSession(database);
        }
    }
}