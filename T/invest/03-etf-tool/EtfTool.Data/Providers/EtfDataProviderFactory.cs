
using EtfTool.Core.Enums;
using EtfTool.Core.Interfaces;
using EtfTool.Data.ApiClients;

namespace EtfTool.Data.Providers
{
    public class EtfDataProviderFactory
    {
        private readonly Dictionary<DataSource, IEtfDataProvider> _providers = new();

        public EtfDataProviderFactory()
        {
            _providers[DataSource.Sina] = new SinaApiClient();
            _providers[DataSource.EastMoney] = new EastMoneyApiClient();
        }

        public IEtfDataProvider GetProvider(DataSource dataSource)
        {
            if (_providers.TryGetValue(dataSource, out var provider))
            {
                return provider;
            }
            throw new ArgumentOutOfRangeException(nameof(dataSource));
        }

        public List<DataSource> GetAvailableDataSources()
        {
            return _providers.Keys.ToList();
        }
    }
}
