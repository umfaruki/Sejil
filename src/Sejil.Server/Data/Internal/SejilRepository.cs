using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Sejil.Configuration.Internal;
using Sejil.Models.Internal;

namespace Sejil.Data.Internal
{
    public class SejilRepository: ISejilRepository
    {
        private readonly ISejilSqlProvider _sql;
        private readonly string _connectionString;
        private const int PAGE_SIZE = 100;

        public SejilRepository(ISejilSqlProvider sql, SejilSettings settings)
        {
            _sql = sql;
            _connectionString = $"DataSource={settings.SqliteDbPath}";
        }

        public async Task<bool> SaveQueryAsync(LogQuery logQuery)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = _sql.InsertLogQuerySql();
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.AddWithValue("@name", logQuery.Name);
                    cmd.Parameters.AddWithValue("@query", logQuery.Query);
                    return await cmd.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<IEnumerable<LogQuery>> GetSavedQueriesAsync()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                return conn.Query<LogQuery>(_sql.GetSavedQueriesSql());
            }
        }

        public async Task<IEnumerable<LogEntry>> GetPageAsync(int page, DateTime startingTimestamp, string query)
        {
            var sql = _sql.GetPagedLogEntriesSql(page == 0 ? 1 : page, PAGE_SIZE, startingTimestamp, query);

            using (var conn = new SqliteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                var lookup = new Dictionary<string, LogEntry>();

                var data = conn.Query<LogEntry, LogEntryProperty, LogEntry>(sql, (l, p) =>
                    {
                        LogEntry logEntry;
                        if (!lookup.TryGetValue(l.Id, out logEntry))
                        {
                            lookup.Add(l.Id, logEntry = l);
                        }

                        if (logEntry.Properties == null)
                        {
                            logEntry.Properties = new List<LogEntryProperty>();
                        }
                        logEntry.Properties.Add(p);
                        return logEntry;

                    }).ToList();

                return lookup.Values.AsEnumerable();
            }
        }
    }
}