
using EtfTool.Core.Interfaces;
using EtfTool.Core.Models;

namespace EtfTool.Data.Providers
{
    public class CachedEtfDataProvider : IEtfDataProvider
    {
        private readonly IEtfDataProvider _innerProvider;
        private readonly ICacheManager _cacheManager;

        public CachedEtfDataProvider(IEtfDataProvider innerProvider, ICacheManager cacheManager)
        {
            _innerProvider = innerProvider;
            _cacheManager = cacheManager;
        }

        public async Task<EtfInfo?> GetEtfInfoAsync(string etfCode)
        {
            var cached = await _cacheManager.GetEtfInfoFromCacheAsync(etfCode);
            if (cached != null)
            {
                return cached;
            }

            var data = await _innerProvider.GetEtfInfoAsync(etfCode);
            if (data != null)
            {
                await _cacheManager.SaveEtfInfoToCacheAsync(data);
            }
            return data;
        }

        public async Task<List<EtfComponent>> GetEtfComponentsAsync(string etfCode)
        {
            var cached = await _cacheManager.GetEtfComponentsFromCacheAsync(etfCode);
            if (cached != null)
            {
                return cached;
            }

            var data = await _innerProvider.GetEtfComponentsAsync(etfCode);
            if (data.Any())
            {
                await _cacheManager.SaveEtfComponentsToCacheAsync(etfCode, data);
            }
            return data;
        }

        public async Task<List<KlineData>> GetKlineDataAsync(string etfCode, string period = "day", int count = 120)
        {
            var cached = await _cacheManager.GetKlineDataFromCacheAsync(etfCode, period);
            if (cached != null && cached.Count >= count * 0.8)
            {
                return cached;
            }

            var data = await _innerProvider.GetKlineDataAsync(etfCode, period, count);
            if (data.Any())
            {
                await _cacheManager.SaveKlineDataToCacheAsync(etfCode, period, data);
            }
            return data;
        }

        public async Task<List<EtfDividend>> GetEtfDividendsAsync(string etfCode)
        {
            return await _innerProvider.GetEtfDividendsAsync(etfCode);
        }

        public async Task<List<EtfInfo>> SearchEtfAsync(string keyword)
        {
            return await _innerProvider.SearchEtfAsync(keyword);
        }
    }
}
