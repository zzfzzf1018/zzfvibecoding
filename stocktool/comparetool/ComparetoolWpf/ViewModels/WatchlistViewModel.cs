using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;

namespace ComparetoolWpf.ViewModels;

/// <summary>自选股 ViewModel：搜索股票 → 加入 / 移除。</summary>
public partial class WatchlistViewModel : ObservableObject
{
    private readonly StockDataService _data;
    private readonly WatchlistService _watch;

    public WatchlistViewModel(StockDataService data, WatchlistService watch)
    {
        _data = data;
        _watch = watch;
    }

    public ObservableCollection<StockInfo> Items => _watch.Items;

    [ObservableProperty] private string _searchKeyword = string.Empty;
    public ObservableCollection<StockInfo> SearchResults { get; } = new();
    [ObservableProperty] private StockInfo? _selectedSearch;
    [ObservableProperty] private StockInfo? _selectedItem;

    private int _searchToken;
    partial void OnSearchKeywordChanged(string value)
    {
        if (SelectedSearch != null && value == SelectedSearch.ToString()) return;
        var token = System.Threading.Interlocked.Increment(ref _searchToken);
        _ = AutoSearchAsync(value, token);
    }
    private async Task AutoSearchAsync(string text, int token)
    {
        try
        {
            await Task.Delay(300);
            if (token != _searchToken || string.IsNullOrWhiteSpace(text)) return;
            var list = await _data.SearchStocksAsync(text);
            if (token != _searchToken) return;
            SearchResults.Clear();
            foreach (var s in list) SearchResults.Add(s);
        }
        catch { }
    }

    [RelayCommand]
    private void Add()
    {
        if (SelectedSearch == null) return;
        _watch.Add(SelectedSearch);
    }

    [RelayCommand]
    private void Remove()
    {
        if (SelectedItem == null) return;
        _watch.Remove(SelectedItem.FullCode);
    }
}
