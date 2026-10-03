namespace Stint.Core
{
    /// <summary>
    /// A backup or restore failed for a reason worth telling the user in plain words (not a
    /// valid database, made by a newer version, ...). <see cref="Exception.Message"/> is written
    /// to be shown on screen as is.
    /// </summary>
    public class DatabaseBackupException : Exception
    {
        #region Constructors
        public DatabaseBackupException(string message) : base(message)
        {
        }

        public DatabaseBackupException(string message, Exception innerException) : base(message, innerException)
        {
        }
        #endregion
    }
}
