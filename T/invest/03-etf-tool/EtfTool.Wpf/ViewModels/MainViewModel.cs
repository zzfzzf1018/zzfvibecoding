
using System.Collections.ObjectModel;
using System.Windows.Input;
using EtfTool.Core.Enums;
using EtfTool.Core.Models;
using EtfTool.Data.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace EtfTool.Wpf.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly EtfService _etfService;
        private string _searchKeyword = string.Empty;
        private bool _isLoading;
        private string _selectedEtfCode = string.Empty;
        private DataSource _selectedDataSource;

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => Set(ref _searchKeyword, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public string SelectedEtfCode
        {
            get => _selectedEtfCode;
            set => Set(ref _selectedEtfCode, value);
        }

        public DataSource SelectedDataSource
        {
            get => _selectedDataSource;
            set
            {
                if (Set(ref _selectedDataSource, value))
                {
                    _etfService.SwitchDataSource(value);
                }
            }
        }

        public ObservableCollection<EtfInfo> SearchResults { get; } = new();
        public ObservableCollection<DataSource> AvailableDataSources { get; } = new();

        public ICommand SearchCommand { get; }
        public ICommand ClearCacheCommand { get; }

        public MainViewModel(EtfService etfService)
        {
            _etfService = etfService;
            _selectedDataSource = etfService.CurrentDataSource;

            AvailableDataSources.Add(DataSource.Sina);
            AvailableDataSources.Add(DataSource.EastMoney);

            SearchCommand = new RelayCommand(SearchEtfAsync);
            ClearCacheCommand = new RelayCommand(() => _etfService.ClearCache());
        }

        private async void SearchEtfAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchKeyword))
                return;

            IsLoading = true;
            SearchResults.Clear();

            try
            {
                var results = await _etfService.SearchEtfAsync(SearchKeyword);
                foreach (var etf in results)
                {
                    SearchResults.Add(etf);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadEtfDetailAsync(string etfCode)
        {
            SelectedEtfCode = etfCode;
        }
    }
}
