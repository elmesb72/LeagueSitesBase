using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

/// <summary>
/// Brings a tenant's SQLite database up to the current schema at startup.
///
/// The journal is SQLite's built-in <c>PRAGMA user_version</c> (an integer in
/// the file header, 0 by default). Migrations are embedded SQL resources
/// named <c>NNNN_description.sql</c> under Migrations/, applied in strictly
/// sequential order; each file runs inside its own transaction (with foreign
/// keys off, dump-style) and the version is bumped in the same transaction,
/// so a failed migration leaves the database untouched at its old version.
///
/// Because every tenant runs a single backend process that owns its database
/// file exclusively, migrating at startup is race-free, and deploy order
/// stops mattering: new code repairs its own schema before serving requests.
///
/// Databases that predate the migration system (created from the old
/// Empty.db.sql, user_version 0 but fully populated) are detected and
/// stamped with the version their schema actually matches, rather than
/// re-running the baseline over them.
/// </summary>
public static partial class DatabaseMigrator
{
    public sealed record Migration(int Version, string Name, string Sql);

    [GeneratedRegex(@"^(\d{4})_([A-Za-z0-9_]+)\.sql$")]
    private static partial Regex MigrationFileName();

    /// <summary>Applies pending migrations. Throws on failure: a backend that cannot fix its schema must not serve traffic.</summary>
    public static void Migrate(string connectionString, ILogger logger)
    {
        Migrate(connectionString, LoadEmbeddedMigrations(), logger);
    }

    public static void Migrate(string connectionString, IReadOnlyList<Migration> migrations, ILogger logger)
    {
        ValidateSequential(migrations);

        // Foreign keys must be off while migrating (set outside any
        // transaction), mirroring how SQLite dumps restore. Microsoft.Data.
        // Sqlite turns them on by default.
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            ForeignKeys = false,
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open(); // creates the file when it does not exist yet

        var version = GetUserVersion(connection);

        if (version == 0 && migrations.Count > 0)
        {
            version = DetectLegacyVersion(connection, logger);
        }

        var latest = migrations.Count > 0 ? migrations[^1].Version : 0;
        if (version > latest)
        {
            logger.LogWarning(
                "Database is at version {Version} but this build only knows up to {Latest}. "
                + "Assuming a rollback to older code; continuing without changes.",
                version, latest);
            return;
        }

        var pending = migrations.Where(m => m.Version > version).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database is up to date at version {Version}.", version);
            return;
        }

        BackupBeforeMigrating(connection, version, logger);

        foreach (var migration in pending)
        {
            logger.LogInformation("Applying migration {Version} ({Name})...", migration.Version, migration.Name);
            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = migration.Sql;
                command.ExecuteNonQuery();
            }
            using (var bump = connection.CreateCommand())
            {
                bump.Transaction = transaction;
                // PRAGMA doesn't support parameters; Version is an int we parsed.
                bump.CommandText = $"PRAGMA user_version = {migration.Version};";
                bump.ExecuteNonQuery();
            }
            transaction.Commit();
            logger.LogInformation("Migration {Version} applied.", migration.Version);
        }

        logger.LogInformation("Database migrated to version {Version}.", pending[^1].Version);
    }

    public static IReadOnlyList<Migration> LoadEmbeddedMigrations()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var migrations = new List<Migration>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            // Embedded name looks like "LeagueSitesBackend.Migrations.0001_baseline.sql";
            // match on the trailing file-name segments.
            var parts = resource.Split('.');
            if (parts.Length < 2) continue;
            var fileName = $"{parts[^2]}.{parts[^1]}";
            var match = MigrationFileName().Match(fileName);
            if (!match.Success) continue;

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            migrations.Add(new Migration(
                int.Parse(match.Groups[1].Value),
                match.Groups[2].Value,
                reader.ReadToEnd()));
        }

        return [.. migrations.OrderBy(m => m.Version)];
    }

    static void ValidateSequential(IReadOnlyList<Migration> migrations)
    {
        for (int i = 0; i < migrations.Count; i++)
        {
            if (migrations[i].Version != i + 1)
            {
                throw new InvalidOperationException(
                    "Migrations must be numbered strictly sequentially from 0001. "
                    + $"Expected version {i + 1} at position {i}, found {migrations[i].Version} "
                    + $"({migrations[i].Name}).");
            }
        }
    }

    /// <summary>
    /// A database from before the migration system reports user_version 0 but
    /// already contains the baseline schema. Stamp it with the version its
    /// schema matches: 2 when the StandingsJson column is already present
    /// (manually migrated), otherwise 1 (the 0001 baseline).
    /// </summary>
    static long DetectLegacyVersion(SqliteConnection connection, ILogger logger)
    {
        if (!TableExists(connection, "SiteConfig")) return 0; // genuinely new database

        // Guard against half-formed databases: legacy detection must only
        // fire for a real baseline schema, never for a partial one.
        string[] expectedTables = ["Season", "Team", "Game", "GameStatus", "User", "Tournament"];
        var missing = expectedTables.Where(t => !TableExists(connection, t)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "This database has a SiteConfig table but is missing expected baseline tables "
                + $"({string.Join(", ", missing)}). Refusing to guess; restore or repair it manually.");
        }

        long stamped = ColumnExists(connection, "SiteConfig", "StandingsJson") ? 2 : 1;
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {stamped};";
        command.ExecuteNonQuery();
        logger.LogInformation(
            "Detected a pre-migration-system database; stamped it as version {Version}.", stamped);
        return stamped;
    }

    static void BackupBeforeMigrating(SqliteConnection connection, long fromVersion, ILogger logger)
    {
        var path = connection.DataSource;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        // A brand-new (zero-length or version-0-and-empty) file has nothing
        // worth copying.
        if (fromVersion == 0) return;

        var backupPath = $"{path}.v{fromVersion}.bak";
        File.Copy(path, backupPath, overwrite: true);
        logger.LogInformation("Pre-migration backup written to {BackupPath}.", backupPath);
    }

    static long GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    static bool TableExists(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", table);
        return (long)command.ExecuteScalar()! > 0;
    }

    static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name=$column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        return (long)command.ExecuteScalar()! > 0;
    }
}
