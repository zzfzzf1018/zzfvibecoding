using System.Globalization;
using Microsoft.Data.Sqlite;
using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.Storage;

/// <summary>
/// 本地 SQLite K 线缓存。位于 %AppData%/StockTechAnalyzer/cache.db。
/// </summary>
public sealed class KlineCache : IDisposable
{
    private readonly SqliteConnection _conn;

    public KlineCache()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StockTechAnalyzer");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "cache.db");
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS klines (
  code TEXT NOT NULL,
  period INTEGER NOT NULL,
  date TEXT NOT NULL,
  open REAL, high REAL, low REAL, close REAL, volume REAL, amount REAL,
  PRIMARY KEY(code, period, date)
);
CREATE INDEX IF NOT EXISTS idx_klines_cd ON klines(code, period, date);
";
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Kline> Load(string code, KlinePeriod period, int count)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT date,open,high,low,close,volume,amount FROM klines
                            WHERE code=@c AND period=@p
                            ORDER BY date DESC LIMIT @n";
        cmd.Parameters.AddWithValue("@c", code);
        cmd.Parameters.AddWithValue("@p", (int)period);
        cmd.Parameters.AddWithValue("@n", count);
        using var rd = cmd.ExecuteReader();
        var list = new List<Kline>();
        while (rd.Read())
        {
            list.Add(new Kline
            {
                Date = DateTime.ParseExact(rd.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Open = rd.GetDouble(1),
                High = rd.GetDouble(2),
                Low = rd.GetDouble(3),
                Close = rd.GetDouble(4),
                Volume = rd.GetDouble(5),
                Amount = rd.IsDBNull(6) ? 0 : rd.GetDouble(6),
            });
        }
        list.Sort((a, b) => a.Date.CompareTo(b.Date));
        return list;
    }

    public void Save(string code, KlinePeriod period, IEnumerable<Kline> bars)
    {
        using var tx = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT OR REPLACE INTO klines
                            (code,period,date,open,high,low,close,volume,amount)
                            VALUES (@c,@p,@d,@o,@h,@l,@cl,@v,@a)";
        var pC = cmd.Parameters.Add("@c", SqliteType.Text);
        var pP = cmd.Parameters.Add("@p", SqliteType.Integer);
        var pD = cmd.Parameters.Add("@d", SqliteType.Text);
        var pO = cmd.Parameters.Add("@o", SqliteType.Real);
        var pH = cmd.Parameters.Add("@h", SqliteType.Real);
        var pL = cmd.Parameters.Add("@l", SqliteType.Real);
        var pCl = cmd.Parameters.Add("@cl", SqliteType.Real);
        var pV = cmd.Parameters.Add("@v", SqliteType.Real);
        var pA = cmd.Parameters.Add("@a", SqliteType.Real);

        pC.Value = code; pP.Value = (int)period;
        foreach (var b in bars)
        {
            pD.Value = b.Date.ToString("yyyy-MM-dd");
            pO.Value = b.Open; pH.Value = b.High; pL.Value = b.Low;
            pCl.Value = b.Close; pV.Value = b.Volume; pA.Value = b.Amount;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public DateTime? LastDate(string code, KlinePeriod period)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(date) FROM klines WHERE code=@c AND period=@p";
        cmd.Parameters.AddWithValue("@c", code);
        cmd.Parameters.AddWithValue("@p", (int)period);
        var v = cmd.ExecuteScalar();
        if (v == null || v is DBNull) return null;
        return DateTime.ParseExact((string)v, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public void Dispose() => _conn.Dispose();
}
