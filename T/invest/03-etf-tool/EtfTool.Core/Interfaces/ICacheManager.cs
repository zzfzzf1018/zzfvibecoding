
using EtfTool.Core.Models;

namespace EtfTool.Core.Interfaces
{
    public interface ICacheManager
    {
        Task<T?> GetFromCacheAsync<T>(string key) where T : class;
        Task SaveToCacheAsync<T>(string key, T data, TimeSpan? expiration = null) where T : class;
        Task ClearCacheAsync(string key);
        Task ClearAllCacheAsync();
        Task<EtfInfo?> GetEtfInfoFromCacheAsync(string etfCode);
        Task SaveEtfInfoToCacheAsync(EtfInfo etfInfo);
        Task<List<EtfComponent>?> GetEtfComponentsFromCacheAsync(string etfCode);
        Task SaveEtfComponentsToCacheAsync(string etfCode, List<EtfComponent> components);
        Task<List<KlineData>?> GetKlineDataFromCacheAsync(string etfCode, string period);
        Task SaveKlineDataToCacheAsync(string etfCode, string period, List<KlineData> klineData);
    }
}
