using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Infrastructure.DataAccessManager.EFCore;

/// <summary>
/// Brings the database up to date at start-up.
///  1. New database      : applies EF Core migrations (or EnsureCreated while no migration exists yet).
///  2. Migrated database : applies pending migrations.
///  3. Legacy database   : created by an older release with EnsureCreated (no __EFMigrationsHistory).
///                         Only the tables that are missing are created, and when migrations exist the
///                         history table is baselined so later migrations apply normally.
/// </summary>
public static class DatabaseUpgrader
{
    private const string HistoryTable = "__EFMigrationsHistory";
    private static readonly Regex BatchSplit = new(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly Regex CreateTable = new(@"^\s*CREATE\s+TABLE\s+(?:\[[^\]]+\]\.)?\[([^\]]+)\]", RegexOptions.IgnoreCase);
    private static readonly Regex CreateIndex = new(@"^\s*CREATE\s+(?:UNIQUE\s+)?(?:(?:NON)?CLUSTERED\s+)?INDEX\s+\[[^\]]+\]\s+ON\s+(?:\[[^\]]+\]\.)?\[([^\]]+)\]", RegexOptions.IgnoreCase);
    private static readonly Regex AlterTable = new(@"^\s*ALTER\s+TABLE\s+(?:\[[^\]]+\]\.)?\[([^\]]+)\]", RegexOptions.IgnoreCase);

    public static void Upgrade(DbContext context)
    {
        var database = context.Database;
        var hasMigrations = database.GetMigrations().Any();

        var connection = database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;

        bool databaseExists = database.CanConnect();
        bool identityTableExists = false;
        bool historyExists = false;

        if (databaseExists)
        {
            if (wasClosed) connection.Open();
            try
            {
                identityTableExists = TableExists(connection, "AspNetUsers");
                historyExists = TableExists(connection, HistoryTable);
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }

        if (!databaseExists || !identityTableExists)
        {
            //>>> brand-new (or empty) database
            if (hasMigrations) database.Migrate(); else database.EnsureCreated();
            return;
        }

        if (historyExists)
        {
            if (hasMigrations)
            {
                database.Migrate();
            }
            else
            {
                AddMissingTables(context);
                AddMissingColumns(context);
            }
            return;
        }

        //>>> legacy database
        AddMissingTables(context);
        AddMissingColumns(context);

        if (hasMigrations)
        {
            Baseline(context);
        }
    }

    private static void AddMissingTables(DbContext context)
    {
        var database = context.Database;
        var connection = database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) connection.Open();

        try
        {
            var script = database.GenerateCreateScript();
            var batches = BatchSplit.Split(script).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var batch in batches)
            {
                var m = CreateTable.Match(batch);
                if (m.Success && TableExists(connection, m.Groups[1].Value))
                {
                    existing.Add(m.Groups[1].Value);
                }
            }

            foreach (var batch in batches)
            {
                string? table = null;
                var m = CreateTable.Match(batch);
                if (m.Success) table = m.Groups[1].Value;
                else if ((m = CreateIndex.Match(batch)).Success) table = m.Groups[1].Value;
                else if ((m = AlterTable.Match(batch)).Success) table = m.Groups[1].Value;

                //>>> skip anything that is not about a table, and everything about tables that already exist
                if (table == null || existing.Contains(table))
                {
                    continue;
                }

                using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.ExecuteNonQuery();

                if (CreateTable.IsMatch(batch))
                {
                    created.Add(table);
                }
            }

            if (created.Count > 0)
            {
                Log.Information("DatabaseUpgrader created missing tables: {Tables}", string.Join(", ", created));
            }
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    /// <summary>
    /// Adds columns that exist in the model but not in the table (new properties on an existing database).
    /// Columns are added as NULL-able unless a safe default exists, so existing rows are never rejected.
    /// </summary>
    private static void AddMissingColumns(DbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) connection.Open();

        try
        {
            foreach (var entityType in context.Model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table == null || !TableExists(connection, table))
                {
                    continue;
                }

                var storeObject = StoreObjectIdentifier.Table(table, entityType.GetSchema());
                var existing = GetColumns(connection, table);

                foreach (var property in entityType.GetProperties())
                {
                    var column = property.GetColumnName(storeObject);
                    if (column == null || existing.Contains(column))
                    {
                        continue;
                    }

                    var storeType = property.GetColumnType(storeObject);
                    var clr = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                    var fallback = DefaultFor(clr);
                    var notNull = !property.IsNullable && fallback != null;

                    var sql = notNull
                        ? $"ALTER TABLE [{table}] ADD [{column}] {storeType} NOT NULL CONSTRAINT [DF_{table}_{column}] DEFAULT {fallback}"
                        : $"ALTER TABLE [{table}] ADD [{column}] {storeType} NULL";

                    Execute(connection, sql);
                    existing.Add(column);
                    Log.Information("DatabaseUpgrader added column {Table}.{Column}", table, column);
                }
            }
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    private static string? DefaultFor(Type clr)
    {
        if (clr == typeof(bool)) return "0";
        if (clr.IsEnum) return "0";
        if (clr == typeof(string)) return "N''";
        if (clr == typeof(DateTime) || clr == typeof(DateTimeOffset)) return "'0001-01-01'";
        if (clr == typeof(Guid)) return "'00000000-0000-0000-0000-000000000000'";
        if (clr == typeof(byte) || clr == typeof(short) || clr == typeof(int) || clr == typeof(long) ||
            clr == typeof(float) || clr == typeof(double) || clr == typeof(decimal)) return "0";
        return null;
    }

    private static HashSet<string> GetColumns(DbConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @name";
        var p = command.CreateParameter();
        p.ParameterName = "@name";
        p.Value = table;
        command.Parameters.Add(p);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private static void Baseline(DbContext context)
    {
        var database = context.Database;
        var connection = database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) connection.Open();

        try
        {
            Execute(connection,
                $"IF OBJECT_ID(N'[{HistoryTable}]') IS NULL CREATE TABLE [{HistoryTable}] ([MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK_{HistoryTable}] PRIMARY KEY ([MigrationId]))");

            foreach (var migration in database.GetMigrations())
            {
                var id = migration.Replace("'", "''");
                Execute(connection,
                    $"IF NOT EXISTS (SELECT 1 FROM [{HistoryTable}] WHERE [MigrationId] = N'{id}') INSERT INTO [{HistoryTable}] ([MigrationId], [ProductVersion]) VALUES (N'{id}', N'10.0.0')");
            }

            Log.Information("DatabaseUpgrader baselined the migration history of a legacy database");
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    private static bool TableExists(DbConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name";
        var p = command.CreateParameter();
        p.ParameterName = "@name";
        p.Value = table;
        command.Parameters.Add(p);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
