
using System.Collections.ObjectModel;
using System.IO;
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
        private string _errorMessage = string.Empty;

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

        public string ErrorMessage
        {
            get => _errorMessage;
            set => Set(ref _errorMessage, value);
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
            
            LogInfo($"MainViewModel created, EtfService: {etfService.GetHashCode()}, SearchCommand: {SearchCommand != null}");
        }

        private async void SearchEtfAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchKeyword))
            {
                ErrorMessage = "请输入搜索关键词";
                LogError("Search failed: keyword is empty");
                return;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;
            SearchResults.Clear();

            try
            {
                LogInfo($"Searching ETF: {SearchKeyword}, DataSource: {SelectedDataSource}");
                var results = await _etfService.SearchEtfAsync(SearchKeyword);
                
                if (results == null || results.Count == 0)
                {
                    ErrorMessage = "未找到匹配的ETF，请尝试其他关键词";
                    LogInfo($"No results found for: {SearchKeyword}");
                }
                else
                {
                    foreach (var etf in results)
                    {
                        SearchResults.Add(etf);
                    }
                    LogInfo($"Found {results.Count} ETFs for: {SearchKeyword}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"搜索失败: {ex.Message}";
                LogError($"Search failed for '{SearchKeyword}': {ex.Message}\n{ex.StackTrace}");
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

        private void LogInfo(string message)
        {
            WriteLog($"INFO: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        private void LogError(string message)
        {
            WriteLog($"ERROR: {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        private void WriteLog(string message)
        {
            try
            {
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                var logFile = Path.Combine(logDir, $"etftool_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, message + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
