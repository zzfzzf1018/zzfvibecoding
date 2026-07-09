
using EtfTool.Core.Models;

namespace EtfTool.Core.Interfaces
{
    public interface IEtfDataProvider
    {
        Task<EtfInfo?> GetEtfInfoAsync(string etfCode);
        Task<List<EtfComponent>> GetEtfComponentsAsync(string etfCode);
        Task<List<KlineData>> GetKlineDataAsync(string etfCode, string period = "day", int count = 120);
        Task<List<EtfDividend>> GetEtfDividendsAsync(string etfCode);
        Task<List<EtfInfo>> SearchEtfAsync(string keyword);
    }
}
