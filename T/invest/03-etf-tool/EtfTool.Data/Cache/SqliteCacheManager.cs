
using System.Data;
using Dapper;
using EtfTool.Core.Interfaces;
using EtfTool.Core.Models;
using Microsoft.Data.Sqlite;

namespace EtfTool.Data.Cache
{
    public class SqliteCacheManager : ICacheManager
    {
        private readonly string _connectionString;

        public SqliteCacheManager()
        {
            var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }
            _connectionString = $"Data Source={Path.Combine(cacheDir, "etf_cache.db")}";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTables = @"
                CREATE TABLE IF NOT EXISTS CacheData (
                    Key TEXT PRIMARY KEY,
                    Data TEXT NOT NULL,
                    ExpireTime INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS EtfInfo (
                    Code TEXT PRIMARY KEY,
                    Name TEXT,
                    FullName TEXT,
                    Type TEXT,
                    Exchange TEXT,
                    TotalAssets REAL,
                    Unit REAL,
                    LatestPrice REAL,
                    ChangePercent REAL,
                    PeRatio REAL,
                    PbRatio REAL,
                    DividendYield REAL,
                    ManagementFee REAL,
                    CustodyFee REAL,
                    SalesServiceFee REAL,
                    SubscriptionFee REAL,
                    RedemptionFee REAL,
                    ListedDate TEXT,
                    UpdateTime TEXT
                );

                CREATE TABLE IF NOT EXISTS EtfComponents (
                    EtfCode TEXT,
                    StockCode TEXT,
                    StockName TEXT,
                    Weight REAL,
                    Price REAL,
                    ChangePercent REAL,
                    PeRatio REAL,
                    PbRatio REAL,
                    Rank INTEGER,
                    UpdateTime TEXT,
                    PRIMARY KEY (EtfCode, StockCode)
                );

                CREATE TABLE IF NOT EXISTS KlineData (
                    EtfCode TEXT,
                    Period TEXT,
                    Date TEXT,
                    Open REAL,
                    High REAL,
                    Low REAL,
                    Close REAL,
                    Volume REAL,
                    Amount REAL,
                    PeRatio REAL,
                    PbRatio REAL,
                    ChangePercent REAL,
                    PRIMARY KEY (EtfCode, Period, Date)
                );
            ";

            connection.Execute(createTables);
        }

        public async Task<T?> GetFromCacheAsync<T>(string key) where T : class
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT Data, ExpireTime FROM CacheData WHERE Key = @Key";
                var result = await connection.QueryFirstOrDefaultAsync<(string Data, long ExpireTime)>(query, new { Key = key });

                if (result.Data != null && result.ExpireTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(result.Data);
                }

                await connection.ExecuteAsync("DELETE FROM CacheData WHERE Key = @Key", new { Key = key });
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task SaveToCacheAsync<T>(string key, T data, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var expireTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (expiration?.TotalSeconds ?? 3600);
                var jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data);

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var upsert = @"
                    INSERT OR REPLACE INTO CacheData (Key, Data, ExpireTime)
                    VALUES (@Key, @Data, @ExpireTime)
                ";
                await connection.ExecuteAsync(upsert, new { Key = key, Data = jsonData, ExpireTime = expireTime });
            }
            catch (Exception)
            {
            }
        }

        public async Task ClearCacheAsync(string key)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                await connection.ExecuteAsync("DELETE FROM CacheData WHERE Key = @Key", new { Key = key });
            }
            catch (Exception)
            {
            }
        }

        public async Task ClearAllCacheAsync()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                await connection.ExecuteAsync("DELETE FROM CacheData");
                await connection.ExecuteAsync("DELETE FROM EtfInfo");
                await connection.ExecuteAsync("DELETE FROM EtfComponents");
                await connection.ExecuteAsync("DELETE FROM KlineData");
            }
            catch (Exception)
            {
            }
        }

        public async Task<EtfInfo?> GetEtfInfoFromCacheAsync(string etfCode)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT * FROM EtfInfo WHERE Code = @Code";
                var result = await connection.QueryFirstOrDefaultAsync<EtfInfo>(query, new { Code = etfCode });

                if (result != null && result.UpdateTime.HasValue && 
                    (DateTime.Now - result.UpdateTime.Value).TotalHours < 24)
                {
                    return result;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task SaveEtfInfoToCacheAsync(EtfInfo etfInfo)
        {
            try
            {
                etfInfo.UpdateTime = DateTime.Now;

                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var upsert = @"
                    INSERT OR REPLACE INTO EtfInfo (
                        Code, Name, FullName, Type, Exchange, TotalAssets, Unit,
                        LatestPrice, ChangePercent, PeRatio, PbRatio, DividendYield,
                        ManagementFee, CustodyFee, SalesServiceFee, SubscriptionFee,
                        RedemptionFee, ListedDate, UpdateTime
                    ) VALUES (
                        @Code, @Name, @FullName, @Type, @Exchange, @TotalAssets, @Unit,
                        @LatestPrice, @ChangePercent, @PeRatio, @PbRatio, @DividendYield,
                        @ManagementFee, @CustodyFee, @SalesServiceFee, @SubscriptionFee,
                        @RedemptionFee, @ListedDate, @UpdateTime
                    )
                ";
                await connection.ExecuteAsync(upsert, etfInfo);
            }
            catch (Exception)
            {
            }
        }

        public async Task<List<EtfComponent>?> GetEtfComponentsFromCacheAsync(string etfCode)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT * FROM EtfComponents WHERE EtfCode = @EtfCode ORDER BY Rank";
                var result = (await connection.QueryAsync<EtfComponent>(query, new { EtfCode = etfCode })).ToList();

                if (result.Any() && result.First().UpdateTime.HasValue &&
                    (DateTime.Now - result.First().UpdateTime.Value).TotalDays < 7)
                {
                    return result;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task SaveEtfComponentsToCacheAsync(string etfCode, List<EtfComponent> components)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                await connection.ExecuteAsync("DELETE FROM EtfComponents WHERE EtfCode = @EtfCode", new { EtfCode = etfCode });

                foreach (var component in components)
                {
                    component.EtfCode = etfCode;
                    component.UpdateTime = DateTime.Now;

                    var insert = @"
                        INSERT INTO EtfComponents (
                            EtfCode, StockCode, StockName, Weight, Price, ChangePercent,
                            PeRatio, PbRatio, Rank, UpdateTime
                        ) VALUES (
                            @EtfCode, @StockCode, @StockName, @Weight, @Price, @ChangePercent,
                            @PeRatio, @PbRatio, @Rank, @UpdateTime
                        )
                    ";
                    await connection.ExecuteAsync(insert, component);
                }
            }
            catch (Exception)
            {
            }
        }

        public async Task<List<KlineData>?> GetKlineDataFromCacheAsync(string etfCode, string period)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                var query = "SELECT * FROM KlineData WHERE EtfCode = @EtfCode AND Period = @Period ORDER BY Date";
                var result = (await connection.QueryAsync<KlineData>(query, new { EtfCode = etfCode, Period = period })).ToList();

                if (result.Any())
                {
                    return result;
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task SaveKlineDataToCacheAsync(string etfCode, string period, List<KlineData> klineData)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                await connection.ExecuteAsync("DELETE FROM KlineData WHERE EtfCode = @EtfCode AND Period = @Period", 
                    new { EtfCode = etfCode, Period = period });

                foreach (var item in klineData)
                {
                    item.EtfCode = etfCode;

                    var insert = @"
                        INSERT INTO KlineData (
                            EtfCode, Period, Date, Open, High, Low, Close,
                            Volume, Amount, PeRatio, PbRatio, ChangePercent
                        ) VALUES (
                            @EtfCode, @Period, @Date, @Open, @High, @Low, @Close,
                            @Volume, @Amount, @PeRatio, @PbRatio, @ChangePercent
                        )
                    ";
                    await connection.ExecuteAsync(insert, item);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
