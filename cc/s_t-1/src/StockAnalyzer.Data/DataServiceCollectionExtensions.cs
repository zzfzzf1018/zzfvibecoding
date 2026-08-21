using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockAnalyzer.Core.Abstractions;

namespace StockAnalyzer.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>注册 SQLite 持久化层。</summary>
    /// <param name="databasePath">数据库文件绝对路径。</param>
    public static IServiceCollection AddStockDataStore(this IServiceCollection services, string databasePath)
    {
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();

        services.AddDbContextFactory<StockDbContext>(options =>
            options.UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(60)));

        services.AddSingleton<IStockRepository, SqliteStockRepository>();

        return services;
    }
}
