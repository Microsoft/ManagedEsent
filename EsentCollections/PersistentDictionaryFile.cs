// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PersistentDictionaryFile.cs" company="Microsoft Corporation">
//   Copyright (c) Microsoft Corporation.
// </copyright>
// <summary>
//   Methods that deal with PersistentDictionary database files.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.Isam.Esent.Collections.Generic
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.Database.Isam.Config;

    /// <summary>
    /// Methods that deal with <see cref="PersistentDictionary{TKey,TValue}"/>
    /// database files.
    /// </summary>
    public static class PersistentDictionaryFile
    {
        /// <summary>
        /// Determine if a dictionary database file exists in the specified directory.
        /// </summary>
        /// <param name="directory">The directory to look in.</param>
        /// <returns>True if the database file exists, false otherwise.</returns>
        public static bool Exists(string directory)
        {
            if (null == directory)
            {
                throw new ArgumentNullException("directory");
            }

            if (Directory.Exists(directory))
            {
                var defaultConfig = PersistentDictionaryDefaultConfig.GetDefaultDatabaseConfig();
                var config = new DatabaseConfig()
                {
                    DatabaseFilename = Path.Combine(directory, defaultConfig.DatabaseFilename)
                };
                config.Merge(defaultConfig, MergeRules.KeepExisting);
                return PersistentDictionaryFile.Exists(config);
            }

            return false;
        }

        /// <summary>
        /// Determine if a dictionary database file exists in the specified directory.
        /// </summary>
        /// <param name="config">The config to use for locating dictionary files.</param>
        /// <returns>True if the database file exists, false otherwise.</returns>
        public static bool Exists(DatabaseConfig config)
        {
            if (null == config)
            {
                throw new ArgumentNullException("config");
            }

            return File.Exists(config.DatabaseFilename);
        }

#if MANAGEDESENT_ON_WSA
        // File/Directory not availble in Windows Store Apps.
#else
        /// <summary>
        /// Delete all files associated with a PersistedDictionary database from
        /// the specified directory.
        /// </summary>
        /// <param name="directory">The directory to delete the files from.</param>
        public static void DeleteFiles(string directory)
        {
            if (null == directory)
            {
                throw new ArgumentNullException("directory");
            }

            if (Directory.Exists(directory))
            {
                var defaultConfig = PersistentDictionaryDefaultConfig.GetDefaultDatabaseConfig();
                var config = new DatabaseConfig()
                {
                    DatabaseFilename = Path.Combine(directory, defaultConfig.DatabaseFilename),
                    SystemPath = directory,
                    LogFilePath = directory,
                    TempPath = directory,
                };
                config.Merge(defaultConfig, MergeRules.KeepExisting);
                PersistentDictionaryFile.DeleteFiles(config);
            }
        }

        /// <summary>
        /// Delete all files associated with a PersistedDictionary database from
        /// the specified directory.
        /// </summary>
        /// <param name="config">The config to use for locating dictionary files.</param>
        public static void DeleteFiles(DatabaseConfig config)
        {
            if (null == config)
            {
                throw new ArgumentNullException("config");
            }

            PersistentDictionaryFile.DeleteIfExists(config.DatabaseFilename);

            var flushmapPath = Path.ChangeExtension(config.DatabaseFilename, ".jfm");
            PersistentDictionaryFile.DeleteIfExists(flushmapPath);

            PersistentDictionaryFile.DeleteIfExists(Path.Combine(config.SystemPath, string.Format(CultureInfo.InvariantCulture, "{0}.chk", config.BaseName)));
            foreach (string file in Directory.GetFiles(config.LogFilePath, string.Format(CultureInfo.InvariantCulture, "{0}*.log", config.BaseName)))
            {
                File.Delete(file);
            }

            foreach (string file in Directory.GetFiles(config.LogFilePath, "res*.log"))
            {
                File.Delete(file);
            }

            foreach (string file in Directory.GetFiles(config.LogFilePath, string.Format(CultureInfo.InvariantCulture, "{0}*.jrs", config.BaseName)))
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Delete the file if it exists.
        /// </summary>
        /// <param name="path">File path to delete.</param>
        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
#endif
    }
}
