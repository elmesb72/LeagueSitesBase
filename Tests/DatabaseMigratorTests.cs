using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeagueSitesBackend.Tests;

public class DatabaseMigratorTests : IDisposable
{
    readonly string dbPath = Path.Combine(
        Path.GetTempPath(), $"migrator-test-{Guid.NewGuid():N}.db");

    string ConnectionString => $"Data Source={dbPath}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in Directory.GetFiles(
            Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*"))
        {
            File.Delete(file);
        }
        GC.SuppressFinalize(this);
    }

    static void Migrate(string connectionString) =>
        DatabaseMigrator.Migrate(connectionString, NullLogger.Instance);

    long UserVersion()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    long Scalar(string sql)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>Creates a database the way tenants had them before the migration system: baseline schema, user_version 0.</summary>
    void CreateLegacyDatabase(bool withStandingsColumn = false)
    {
        var baseline = DatabaseMigrator.LoadEmbeddedMigrations().First(m => m.Version == 1);
        using var connection = new SqliteConnection(ConnectionString + ";Foreign Keys=False");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = baseline.Sql;
            command.ExecuteNonQuery();
        }
        if (withStandingsColumn)
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE SiteConfig ADD COLUMN StandingsJson TEXT NOT NULL DEFAULT '{}';";
            alter.ExecuteNonQuery();
        }
    }

    [Fact]
    public void EmbeddedMigrations_LoadStrictlyInOrder()
    {
        var migrations = DatabaseMigrator.LoadEmbeddedMigrations();

        migrations.Select(m => m.Version).Should().Equal(1, 2, 3);
        migrations[0].Name.Should().Be("baseline");
        migrations[1].Name.Should().Be("standings_config");
        migrations[2].Name.Should().Be("per_season_standings");
        migrations.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Sql));
    }

    [Fact]
    public void FreshDatabase_IsCreatedAndFullyMigrated()
    {
        File.Exists(dbPath).Should().BeFalse("precondition: no database yet");

        Migrate(ConnectionString);

        UserVersion().Should().Be(3);
        Scalar("SELECT COUNT(*) FROM SiteConfig").Should().Be(1, "the baseline seeds a placeholder config");
        Scalar("SELECT COUNT(*) FROM GameStatus").Should().BeGreaterThan(0, "statuses are universal seed data");
        Scalar("SELECT COUNT(*) FROM pragma_table_info('SiteConfig') WHERE name='StandingsJson'")
            .Should().Be(1, "migration 0002 adds the site-level standings column");
        Scalar("SELECT COUNT(*) FROM pragma_table_info('Season') WHERE name='StandingsJson'")
            .Should().Be(1, "migration 0003 adds the per-season standings column");
    }

    [Fact]
    public void LegacyDatabase_IsStampedAsBaselineThenUpgraded()
    {
        CreateLegacyDatabase();
        UserVersion().Should().Be(0, "precondition: legacy databases have no version stamp");

        Migrate(ConnectionString);

        UserVersion().Should().Be(3);
        Scalar("SELECT COUNT(*) FROM pragma_table_info('SiteConfig') WHERE name='StandingsJson'")
            .Should().Be(1, "0002 should have run against the existing schema");
        Scalar("SELECT COUNT(*) FROM pragma_table_info('Season') WHERE name='StandingsJson'")
            .Should().Be(1, "0003 should have run against the existing schema");
        Scalar("SELECT COUNT(*) FROM SiteConfig").Should().Be(1, "existing data must be preserved");
    }

    [Fact]
    public void ExistingSeasons_AreStampedWithTheCanonicalRules()
    {
        CreateLegacyDatabase();
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();
            using var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO Season (Year, Subseason, StartDate) VALUES (2025, 'Regular Season', '2025-05-01');";
            insert.ExecuteNonQuery();
        }

        Migrate(ConnectionString);

        using var check = new SqliteConnection(ConnectionString);
        check.Open();
        using var command = check.CreateCommand();
        command.CommandText = "SELECT StandingsJson FROM Season;";
        var stamped = (string)command.ExecuteScalar()!;

        // The retroactive stamp freezes every pre-existing season on the
        // corrected canonical rules (head-to-head tiebreaks, no overall run
        // differential), explicit so future default changes can't re-rank it.
        var parsed = StandingsConfigService.Parse(stamped);
        parsed.Tiebreakers.Should().Equal(
            "Points", "Wins", "HeadToHeadWins", "HeadToHeadRunDifferential");
        parsed.WinsValue.Should().Be(2);
        parsed.ForfeitWinnerScore.Should().Be(7);
        stamped.Should().NotBeNullOrWhiteSpace("the stamp must be explicit, not inherited defaults");
    }

    [Fact]
    public void LegacyDatabase_WithManualStandingsAlter_IsStampedCurrent()
    {
        // A tenant that already received the ALTER by hand (the old runbook
        // flow) must not have 0002 re-applied over it.
        CreateLegacyDatabase(withStandingsColumn: true);

        Migrate(ConnectionString);

        UserVersion().Should().Be(3);
        Scalar("SELECT COUNT(*) FROM pragma_table_info('SiteConfig') WHERE name='StandingsJson'")
            .Should().Be(1);
    }

    [Fact]
    public void MigratedDatabase_SecondRunIsANoOp()
    {
        Migrate(ConnectionString);
        foreach (var bak in Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*.bak"))
        {
            File.Delete(bak);
        }

        Migrate(ConnectionString);

        UserVersion().Should().Be(3);
        Directory.GetFiles(Path.GetDirectoryName(dbPath)!, Path.GetFileName(dbPath) + "*.bak")
            .Should().BeEmpty("an up-to-date database needs no backup");
    }

    [Fact]
    public void LegacyUpgrade_WritesARestorablePreMigrationBackup()
    {
        CreateLegacyDatabase();

        Migrate(ConnectionString);

        // The backup must be a valid database representing the pre-0002
        // state: stamped as version 1, no StandingsJson column, data intact.
        var backupPath = $"{dbPath}.v1.bak";
        File.Exists(backupPath).Should().BeTrue("the pre-upgrade state must be recoverable");

        using var backup = new SqliteConnection($"Data Source={backupPath};Pooling=False");
        backup.Open();
        using var command = backup.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        ((long)command.ExecuteScalar()!).Should().Be(1);
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SiteConfig') WHERE name='StandingsJson';";
        ((long)command.ExecuteScalar()!).Should().Be(0, "the backup predates migration 0002");
        command.CommandText = "SELECT COUNT(*) FROM SiteConfig;";
        ((long)command.ExecuteScalar()!).Should().Be(1, "seeded data must be present in the backup");
    }

    [Fact]
    public void HalfFormedDatabase_RefusesToGuess()
    {
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE SiteConfig (ID INTEGER PRIMARY KEY);";
            command.ExecuteNonQuery();
        }

        var act = () => Migrate(ConnectionString);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Refusing*");
        UserVersion().Should().Be(0, "a refused database must be left untouched");
    }

    [Fact]
    public void FutureVersion_WarnsAndLeavesDatabaseAlone()
    {
        Migrate(ConnectionString);
        using (var connection = new SqliteConnection(ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99;";
            command.ExecuteNonQuery();
        }

        var act = () => Migrate(ConnectionString);

        act.Should().NotThrow("a rollback to older code must not break startup");
        UserVersion().Should().Be(99);
    }

    [Fact]
    public void NonSequentialMigrations_AreRejected()
    {
        var gapped = new List<DatabaseMigrator.Migration>
        {
            new(1, "one", "SELECT 1;"),
            new(3, "three", "SELECT 1;"),
        };

        var act = () => DatabaseMigrator.Migrate(ConnectionString, gapped, NullLogger.Instance);

        act.Should().Throw<InvalidOperationException>().WithMessage("*sequential*");
    }

    [Fact]
    public void FailedMigration_LeavesVersionAndSchemaUntouched()
    {
        var migrations = new List<DatabaseMigrator.Migration>
        {
            new(1, "good", "CREATE TABLE Alpha (ID INTEGER PRIMARY KEY);"),
            new(2, "bad", "CREATE TABLE Beta (ID INTEGER PRIMARY KEY); THIS IS NOT SQL;"),
        };

        var act = () => DatabaseMigrator.Migrate(ConnectionString, migrations, NullLogger.Instance);

        act.Should().Throw<SqliteException>();
        UserVersion().Should().Be(1, "the failed migration's transaction must roll back the version bump");
        Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='Beta'")
            .Should().Be(0, "no partial DDL from the failed migration may remain");
    }

    [Fact]
    public void MigratedDatabase_ServesTheEfModel()
    {
        Migrate(ConnectionString);

        var options = new DbContextOptionsBuilder<LeagueSitesContext>()
            .UseSqlite(ConnectionString)
            .Options;
        using var context = new LeagueSitesContext(options);

        var siteConfig = context.SiteConfigs.Single();
        siteConfig.StandingsJson.Should().Be("{}");
        siteConfig.Name.Should().Be("Empty Generic League");
        // The baseline seeds one hidden placeholder team backing the
        // bootstrap webmaster account.
        context.Teams.Single().Hidden.Should().BeTrue();
        context.GameStatuses.Count().Should().BeGreaterThan(0);
        context.Seasons.ToList().Should().BeEmpty();
    }
}
