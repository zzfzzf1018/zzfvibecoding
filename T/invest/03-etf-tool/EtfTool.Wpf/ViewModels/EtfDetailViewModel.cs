
using System.Collections.ObjectModel;
using System.Windows.Input;
using EtfTool.Core.Models;
using EtfTool.Data.Services;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;

namespace EtfTool.Wpf.ViewModels
{
    public class EtfDetailViewModel : ViewModelBase
    {
        private readonly EtfService _etfService;
        private string _etfCode = string.Empty;
        private bool _isLoading;
        private EtfInfo? _etfInfo;
        private EtfStatistics? _etfStatistics;
        private string _klinePeriod = "day";

        public string EtfCode
        {
            get => _etfCode;
            set => Set(ref _etfCode, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        public EtfInfo? EtfInfo
        {
            get => _etfInfo;
            set => Set(ref _etfInfo, value);
        }

        public EtfStatistics? EtfStatistics
        {
            get => _etfStatistics;
            set => Set(ref _etfStatistics, value);
        }

        public string KlinePeriod
        {
            get => _klinePeriod;
            set => Set(ref _klinePeriod, value);
        }

        public ObservableCollection<EtfComponent> Components { get; } = new();
        public ObservableCollection<KlineData> KlineData { get; } = new();
        public ObservableCollection<EtfDividend> Dividends { get; } = new();

        public ISeries[] KlineSeries { get; set; } = Array.Empty<ISeries>();
        public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; set; } = Array.Empty<Axis>();

        public ICommand LoadDataCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand LoadKlineDataCommand { get; }

        public EtfDetailViewModel(EtfService etfService)
        {
            _etfService = etfService;
            LoadDataCommand = new RelayCommand<string>(async (code) => await LoadDataAsync(code));
            RefreshCommand = new RelayCommand(async () => await LoadDataAsync(EtfCode));
            LoadKlineDataCommand = new RelayCommand<string>(async (period) => await LoadKlineDataAsync(period));
        }

        public async Task LoadDataAsync(string etfCode)
        {
            if (string.IsNullOrWhiteSpace(etfCode))
                return;

            EtfCode = etfCode;
            IsLoading = true;

            try
            {
                await LoadEtfInfoAsync();
                await LoadComponentsAsync();
                await LoadKlineDataAsync();
                await LoadStatisticsAsync();
                await LoadDividendsAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadEtfInfoAsync()
        {
            EtfInfo = await _etfService.GetEtfInfoAsync(EtfCode);
        }

        private async Task LoadComponentsAsync()
        {
            Components.Clear();
            var components = await _etfService.GetEtfComponentsAsync(EtfCode);
            foreach (var component in components.Take(20))
            {
                Components.Add(component);
            }
        }

        private async Task LoadKlineDataAsync(string? period = null)
        {
            if (period != null)
            {
                KlinePeriod = period;
            }

            KlineData.Clear();
            var data = await _etfService.GetKlineDataAsync(EtfCode, KlinePeriod, 120);
            foreach (var item in data)
            {
                KlineData.Add(item);
            }

            UpdateKlineChart();
        }

        private void UpdateKlineChart()
        {
            if (!KlineData.Any())
                return;

            var candleSeries = new LineSeries<KlineData>
            {
                Values = KlineData,
                Stroke = new SolidColorPaint(SKColors.Black),
                Fill = null,
                GeometrySize = 4
            };

            var ma5Values = KlineData.Select((k, i) => new { Kline = k, Index = i })
                .Where(x => x.Index >= 4)
                .Select(x => new { x.Kline.Date, Value = KlineData.Skip(x.Index - 4).Take(5).Average(k => k.Close) })
                .Select(x => new KlineData { Date = x.Date, Close = x.Value })
                .ToList();

            var ma5Series = new LineSeries<KlineData>
            {
                Values = ma5Values,
                Stroke = new SolidColorPaint(SKColors.Yellow),
                Fill = null,
                Name = "MA5"
            };

            var ma10Values = KlineData.Select((k, i) => new { Kline = k, Index = i })
                .Where(x => x.Index >= 9)
                .Select(x => new { x.Kline.Date, Value = KlineData.Skip(x.Index - 9).Take(10).Average(k => k.Close) })
                .Select(x => new KlineData { Date = x.Date, Close = x.Value })
                .ToList();

            var ma10Series = new LineSeries<KlineData>
            {
                Values = ma10Values,
                Stroke = new SolidColorPaint(SKColors.Blue),
                Fill = null,
                Name = "MA10"
            };

            KlineSeries = new ISeries[] { candleSeries, ma5Series, ma10Series };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = KlineData.Select(k => k.Date.ToShortDateString()).ToArray(),
                    LabelsRotation = 45,
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray),
                    LabelsPaint = new SolidColorPaint(SKColors.Black)
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray),
                    LabelsPaint = new SolidColorPaint(SKColors.Black)
                }
            };

            RaisePropertyChanged(nameof(KlineSeries));
            RaisePropertyChanged(nameof(XAxes));
            RaisePropertyChanged(nameof(YAxes));
        }

        private async Task LoadStatisticsAsync()
        {
            EtfStatistics = await _etfService.CalculateStatisticsAsync(EtfCode, 36);
        }

        private async Task LoadDividendsAsync()
        {
            Dividends.Clear();
            var dividends = await _etfService.GetEtfDividendsAsync(EtfCode);
            foreach (var dividend in dividends.Take(20))
            {
                Dividends.Add(dividend);
            }
        }
    }
}
