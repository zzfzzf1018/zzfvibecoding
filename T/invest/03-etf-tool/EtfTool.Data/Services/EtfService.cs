
using EtfTool.Core.Enums;
using EtfTool.Core.Interfaces;
using EtfTool.Core.Models;
using EtfTool.Data.Providers;

namespace EtfTool.Data.Services
{
    public class EtfService
    {
        private readonly EtfDataProviderFactory _providerFactory;
        private readonly ICacheManager _cacheManager;
        private IEtfDataProvider _currentProvider;

        public DataSource CurrentDataSource { get; private set; }

        public EtfService(EtfDataProviderFactory providerFactory, ICacheManager cacheManager)
        {
            _providerFactory = providerFactory;
            _cacheManager = cacheManager;
            CurrentDataSource = DataSource.EastMoney;
            _currentProvider = new CachedEtfDataProvider(providerFactory.GetProvider(CurrentDataSource), cacheManager);
        }

        public void SwitchDataSource(DataSource dataSource)
        {
            if (CurrentDataSource != dataSource)
            {
                CurrentDataSource = dataSource;
                _currentProvider = new CachedEtfDataProvider(_providerFactory.GetProvider(dataSource), _cacheManager);
            }
        }

        public async Task<EtfInfo?> GetEtfInfoAsync(string etfCode)
        {
            return await _currentProvider.GetEtfInfoAsync(etfCode);
        }

        public async Task<List<EtfComponent>> GetEtfComponentsAsync(string etfCode)
        {
            return await _currentProvider.GetEtfComponentsAsync(etfCode);
        }

        public async Task<List<KlineData>> GetKlineDataAsync(string etfCode, string period = "day", int count = 120)
        {
            return await _currentProvider.GetKlineDataAsync(etfCode, period, count);
        }

        public async Task<List<EtfDividend>> GetEtfDividendsAsync(string etfCode)
        {
            return await _currentProvider.GetEtfDividendsAsync(etfCode);
        }

        public async Task<List<EtfInfo>> SearchEtfAsync(string keyword)
        {
            return await _currentProvider.SearchEtfAsync(keyword);
        }

        public async Task<EtfStatistics> CalculateStatisticsAsync(string etfCode, int periodMonths = 36)
        {
            var klineData = await GetKlineDataAsync(etfCode, "day", periodMonths * 22);
            
            var validPeData = klineData.Where(k => k.PeRatio.HasValue && k.PeRatio > 0).Select(k => k.PeRatio.Value).ToList();
            var validPbData = klineData.Where(k => k.PbRatio.HasValue && k.PbRatio > 0).Select(k => k.PbRatio.Value).ToList();

            var statistics = new EtfStatistics
            {
                EtfCode = etfCode,
                DataCount = klineData.Count,
                StartDate = klineData.MinBy(k => k.Date)?.Date,
                EndDate = klineData.MaxBy(k => k.Date)?.Date
            };

            if (klineData.Any())
            {
                var latest = klineData.Last();
                statistics.CurrentPe = latest.PeRatio;
                statistics.CurrentPb = latest.PbRatio;
            }

            if (validPeData.Any())
            {
                validPeData.Sort();
                statistics.AvgPe = validPeData.Average();
                statistics.PeMin = validPeData.Min();
                statistics.PeMax = validPeData.Max();
                
                if (statistics.CurrentPe.HasValue)
                {
                    var belowCount = validPeData.Count(p => p <= statistics.CurrentPe.Value);
                    statistics.PePercentile = Math.Round((decimal)belowCount / validPeData.Count * 100, 2);
                }
            }

            if (validPbData.Any())
            {
                validPbData.Sort();
                statistics.AvgPb = validPbData.Average();
                statistics.PbMin = validPbData.Min();
                statistics.PbMax = validPbData.Max();
                
                if (statistics.CurrentPb.HasValue)
                {
                    var belowCount = validPbData.Count(p => p <= statistics.CurrentPb.Value);
                    statistics.PbPercentile = Math.Round((decimal)belowCount / validPbData.Count * 100, 2);
                }
            }

            return statistics;
        }

        public async Task<EtfStatistics> CalculateComponentBasedStatisticsAsync(string etfCode)
        {
            var components = await GetEtfComponentsAsync(etfCode);
            
            var weightedPe = components
                .Where(c => c.Weight.HasValue && c.PeRatio.HasValue && c.PeRatio > 0)
                .Sum(c => c.Weight.Value * c.PeRatio.Value / 100);

            var weightedPb = components
                .Where(c => c.Weight.HasValue && c.PbRatio.HasValue && c.PbRatio > 0)
                .Sum(c => c.Weight.Value * c.PbRatio.Value / 100);

            return new EtfStatistics
            {
                EtfCode = etfCode,
                CurrentPe = weightedPe > 0 ? weightedPe : null,
                CurrentPb = weightedPb > 0 ? weightedPb : null,
                DataCount = components.Count
            };
        }

        public void ClearCache()
        {
            _cacheManager.ClearAllCacheAsync().Wait();
        }
    }
}
