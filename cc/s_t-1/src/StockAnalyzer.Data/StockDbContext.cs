using Microsoft.EntityFrameworkCore;
using StockAnalyzer.Core.Models;

namespace StockAnalyzer.Data;

/// <summary>本地 SQLite 数据库上下文。</summary>
public sealed class StockDbContext : DbContext
{
    public StockDbContext(DbContextOptions<StockDbContext> options) : base(options)
    {
    }

    public DbSet<StockInfo> Stocks => Set<StockInfo>();

    public DbSet<WatchlistItem> Watchlist => Set<WatchlistItem>();

    public DbSet<StockQuote> Quotes => Set<StockQuote>();

    public DbSet<DailyBar> DailyBars => Set<DailyBar>();

    public DbSet<FinancialReport> FinancialReports => Set<FinancialReport>();

    public DbSet<SyncStamp> SyncStamps => Set<SyncStamp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockInfo>(entity =>
        {
            entity.ToTable("Stocks");
            entity.HasKey(e => new { e.Market, e.Code });
            entity.Ignore(e => e.Key);
            entity.Ignore(e => e.DisplayText);
            entity.Property(e => e.Name).HasMaxLength(64);
            entity.Property(e => e.SecId).HasMaxLength(32);
            entity.Property(e => e.NameInitials).HasMaxLength(64);
            entity.HasIndex(e => e.Code);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.NameInitials);
        });

        modelBuilder.Entity<WatchlistItem>(entity =>
        {
            entity.ToTable("Watchlist");
            entity.HasKey(e => new { e.Market, e.Code });
            entity.Ignore(e => e.Key);
            entity.Property(e => e.Name).HasMaxLength(64);
            entity.Property(e => e.Note).HasMaxLength(512);
            entity.HasIndex(e => e.SortOrder);
        });

        modelBuilder.Entity<StockQuote>(entity =>
        {
            entity.ToTable("Quotes");
            entity.HasKey(e => new { e.Market, e.Code });
            entity.Ignore(e => e.Currency);
            entity.Property(e => e.Name).HasMaxLength(64);
        });

        modelBuilder.Entity<DailyBar>(entity =>
        {
            entity.ToTable("DailyBars");
            entity.HasKey(e => new { e.Market, e.Code, e.Date });
            entity.HasIndex(e => new { e.Market, e.Code, e.Date });
        });

        modelBuilder.Entity<FinancialReport>(entity =>
        {
            entity.ToTable("FinancialReports");
            entity.HasKey(e => new { e.Market, e.Code, e.ReportDate });
            entity.Ignore(e => e.Quarter);
            entity.Ignore(e => e.Year);
        });

        modelBuilder.Entity<SyncStamp>(entity =>
        {
            entity.ToTable("SyncStamps");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
        });

        base.OnModelCreating(modelBuilder);
    }
}
