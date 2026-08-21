using CommunityToolkit.Mvvm.ComponentModel;
using StockAnalyzer.Core.Models;
using StockAnalyzer.Desktop.Infrastructure;

namespace StockAnalyzer.Desktop.ViewModels;

/// <summary>自选股列表的一行。</summary>
public sealed partial class WatchlistRowViewModel : ObservableObject
{
    [ObservableProperty]
    private StockQuote? _quote;

    public WatchlistRowViewModel(WatchlistItem item)
    {
        Item = item;
    }

    public WatchlistItem Item { get; }

    public string Code => Item.Code;

    public string Name => string.IsNullOrWhiteSpace(Quote?.Name) ? Item.Name : Quote!.Name;

    public MarketType Market => Item.Market;

    public string MarketName => Item.Market.ToDisplayName();

    public string PriceText => Formatting.Number(Quote?.Price);

    public string ChangeText => Formatting.SignedPercent(Quote?.ChangePercent);

    public double? ChangeValue => Quote?.ChangePercent;

    public string PeText => Formatting.Multiple(Quote?.PeTtm);

    public string PbText => Formatting.Multiple(Quote?.Pb);

    public StockInfo ToStockInfo() => new()
    {
        Code = Item.Code,
        Market = Item.Market,
        Name = Name
    };

    partial void OnQuoteChanged(StockQuote? value)
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(ChangeValue));
        OnPropertyChanged(nameof(PeText));
        OnPropertyChanged(nameof(PbText));
    }
}
