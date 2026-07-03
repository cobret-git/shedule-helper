using System.IO;
using System.Text;

namespace SheduleHelper.WpfApp.Components.Data
{
    /// <summary>
    /// Configuration settings for SQLite database connection and behavior.
    /// Contains all necessary parameters to establish database connection,
    /// configure Entity Framework Core options, and manage database encryption.
    /// </summary>
    public record DatabaseConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or initializes the directory path where the database file will be stored.
        /// This should be an absolute path to a valid directory.
        /// </summary>
        public required string DatabasePath { get; init; }

        /// <summary>
        /// Gets or initializes the name of the SQLite database file including extension.
        /// Default value is "ScheduleHelper.db".
        /// The file will be created at the location specified by <see cref="DatabasePath"/>.
        /// </summary>
        public string DatabaseName { get; init; } = "ScheduleHelper.db";

        /// <summary>
        /// Enable SQLCipher encryption (requires SQLCipher NuGet package)
        /// </summary>
        public bool EncryptionEnabled { get; init; }

        /// <summary>
        /// Encryption password for SQLCipher
        /// </summary>
        public string? EncryptionKey { get; init; }

        /// <summary>
        /// SQLite open mode: ReadWriteCreate, ReadWrite, ReadOnly, Memory
        /// </summary>
        public string Mode { get; init; } = "ReadWriteCreate";

        /// <summary>
        /// Enable foreign key constraints enforcement
        /// </summary>
        public bool ForeignKeys { get; init; } = true;

        /// <summary>
        /// Connection pooling: Shared or Private
        /// </summary>
        public string Cache { get; init; } = "Shared";

        /// <summary>
        /// Enable connection pooling
        /// </summary>
        public bool Pooling { get; init; } = true;

        /// <summary>
        /// Command timeout in seconds (0 = infinite)
        /// </summary>
        public int CommandTimeout { get; init; } = 30;

        /// <summary>
        /// Enable detailed errors in development
        /// </summary>
        public bool EnableDetailedErrors { get; init; } = false;

        /// <summary>
        /// Enable sensitive data logging (connection strings, parameter values)
        /// WARNING: Only use in development
        /// </summary>
        public bool EnableSensitiveDataLogging { get; init; } = false;

        #endregion

        #region Methods

        /// <summary>
        /// Gets the full path to the database file
        /// </summary>
        public string GetFullDatabasePath()
        {
            return Path.Combine(DatabasePath, DatabaseName);
        }

        /// <summary>
        /// Builds the SQLite connection string from configuration
        /// </summary>
        public string ToConnectionString()
        {
            var builder = new StringBuilder();

            // Required: Data Source
            builder.Append($"Data Source={GetFullDatabasePath()}");

            // Mode
            if (!string.IsNullOrWhiteSpace(Mode))
            {
                builder.Append($";Mode={Mode}");
            }

            // Cache
            if (!string.IsNullOrWhiteSpace(Cache))
            {
                builder.Append($";Cache={Cache}");
            }

            // Foreign Keys
            builder.Append($";Foreign Keys={ForeignKeys}");

            // Pooling
            builder.Append($";Pooling={Pooling}");

            // Encryption (SQLCipher)
            if (EncryptionEnabled && !string.IsNullOrWhiteSpace(EncryptionKey))
            {
                builder.Append($";Password={EncryptionKey}");
            }

            return builder.ToString();
        }

        #endregion
    }
}
