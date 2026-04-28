using System.IO;
using ComparetoolWpf.Models;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace ComparetoolWpf.Services;

/// <summary>
/// 报表本地缓存（SQLite）。
/// 表结构：
///   CREATE TABLE Reports(
///       StockFullCode TEXT NOT NULL,
///       Kind          INTEGER NOT NULL,
///       ReportDate    TEXT NOT NULL,    -- yyyy-MM-dd
///       PeriodLabel   TEXT,
///       ItemsJson     TEXT NOT NULL,    -- Items 字典的 JSON
///       UpdatedAt     TEXT NOT NULL,
///       PRIMARY KEY (StockFullCode, Kind, ReportDate)
///   );
///
/// 缓存策略：
///   - “年报/中报/已结束季报”视为终值，永久缓存；
///   - “最近一期”可能被追溯调整，提供 <see cref="GetReportsAsync"/> 的 maxAgeDays
///     参数控制再次拉取的频率（默认 0=任何缓存都直接用）。
/// </summary>
public class ReportCache
{
    private readonly string _connStr;

    public ReportCache(string? dbPath = null)
    {
        dbPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ComparetoolWpf", "reports.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connStr = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        Init();
    }

    private void Init()
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Reports(
    StockFullCode TEXT NOT NULL,
    Kind          INTEGER NOT NULL,
    ReportDate    TEXT NOT NULL,
    PeriodLabel   TEXT,
    ItemsJson     TEXT NOT NULL,
    UpdatedAt     TEXT NOT NULL,
    PRIMARY KEY (StockFullCode, Kind, ReportDate)
);
CREATE TABLE IF NOT EXISTS Watchlist(
    FullCode  TEXT PRIMARY KEY,
    Code      TEXT NOT NULL,
    Name      TEXT NOT NULL,
    Market    TEXT NOT NULL,
    AddedAt   TEXT NOT NULL,
    Note      TEXT
);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>返回某只股票某种报表的最近一次拉取时间(UTC)；不存在返回 null。</summary>
    public DateTime? GetLastFetchedAt(string stockFullCode, ReportKind kind)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT MAX(UpdatedAt) FROM Reports WHERE StockFullCode=$c AND Kind=$k";
        cmd.Parameters.AddWithValue("$c", stockFullCode);
        cmd.Parameters.AddWithValue("$k", (int)kind);
        var v = cmd.ExecuteScalar();
        if (v == null || v == DBNull.Value) return null;
        return DateTime.Parse((string)v);
    }

    // ===== 自选股 =====

    public List<StockInfo> LoadWatchlist()
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Code, Name, Market FROM Watchlist ORDER BY AddedAt ASC";
        using var rdr = cmd.ExecuteReader();
        var list = new List<StockInfo>();
        while (rdr.Read())
            list.Add(new StockInfo(rdr.GetString(0), rdr.GetString(1), rdr.GetString(2)));
        return list;
    }

    public void AddWatch(StockInfo s, string? note = null)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO Watchlist(FullCode, Code, Name, Market, AddedAt, Note)
                            VALUES($f,$c,$n,$m,$t,$o)";
        cmd.Parameters.AddWithValue("$f", s.FullCode);
        cmd.Parameters.AddWithValue("$c", s.Code);
        cmd.Parameters.AddWithValue("$n", s.Name);
        cmd.Parameters.AddWithValue("$m", s.Market);
        cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$o", (object?)note ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void RemoveWatch(string fullCode)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM Watchlist WHERE FullCode=$f";
        cmd.Parameters.AddWithValue("$f", fullCode);
        cmd.ExecuteNonQuery();
    }

    public bool IsWatched(string fullCode)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT 1 FROM Watchlist WHERE FullCode=$f LIMIT 1";
        cmd.Parameters.AddWithValue("$f", fullCode);
        return cmd.ExecuteScalar() != null;
    }

    /// <summary>读取某只股票某种报表的所有缓存（按报告期倒序）。</summary>
    /// <param name="maxAgeDays">仅返回更新时间不超过 N 天的记录；&lt;=0 表示不限制。</param>
    public List<FinancialReport> Load(string stockFullCode, ReportKind kind, int maxAgeDays = 0)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT ReportDate, PeriodLabel, ItemsJson, UpdatedAt
                            FROM Reports
                            WHERE StockFullCode=$c AND Kind=$k
                            ORDER BY ReportDate DESC";
        cmd.Parameters.AddWithValue("$c", stockFullCode);
        cmd.Parameters.AddWithValue("$k", (int)kind);

        var list = new List<FinancialReport>();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var date = DateTime.Parse(rdr.GetString(0));
            var label = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
            var json = rdr.GetString(2);
            var updated = DateTime.Parse(rdr.GetString(3));
            if (maxAgeDays > 0 && (DateTime.UtcNow - updated).TotalDays > maxAgeDays)
                continue;

            var items = JsonConvert.DeserializeObject<Dictionary<string, double?>>(json)
                        ?? new Dictionary<string, double?>();
            list.Add(new FinancialReport
            {
                StockFullCode = stockFullCode,
                Kind = kind,
                ReportDate = date,
                PeriodLabel = label,
                Items = items,
            });
        }
        return list;
    }

    /// <summary>批量写入/覆盖缓存。</summary>
    public void Save(IEnumerable<FinancialReport> reports)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO Reports
            (StockFullCode, Kind, ReportDate, PeriodLabel, ItemsJson, UpdatedAt)
            VALUES ($c,$k,$d,$l,$j,$u);";
        var pC = cmd.Parameters.Add("$c", SqliteType.Text);
        var pK = cmd.Parameters.Add("$k", SqliteType.Integer);
        var pD = cmd.Parameters.Add("$d", SqliteType.Text);
        var pL = cmd.Parameters.Add("$l", SqliteType.Text);
        var pJ = cmd.Parameters.Add("$j", SqliteType.Text);
        var pU = cmd.Parameters.Add("$u", SqliteType.Text);
        var now = DateTime.UtcNow.ToString("o");

        foreach (var r in reports)
        {
            pC.Value = r.StockFullCode;
            pK.Value = (int)r.Kind;
            pD.Value = r.ReportDate.ToString("yyyy-MM-dd");
            pL.Value = (object?)r.PeriodLabel ?? DBNull.Value;
            pJ.Value = JsonConvert.SerializeObject(r.Items);
            pU.Value = now;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>清空所有缓存。</summary>
    public void Clear()
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Reports;";
        cmd.ExecuteNonQuery();
    }
}
