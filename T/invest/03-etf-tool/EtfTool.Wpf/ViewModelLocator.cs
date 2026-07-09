
using EtfTool.Core.Interfaces;
using EtfTool.Data.Cache;
using EtfTool.Data.Providers;
using EtfTool.Data.Services;

namespace EtfTool.Wpf
{
    public class ViewModelLocator
    {
        private readonly ICacheManager _cacheManager;
        private readonly EtfDataProviderFactory _providerFactory;
        private readonly EtfService _etfService;

        public ViewModelLocator()
        {
            _cacheManager = new SqliteCacheManager();
            _providerFactory = new EtfDataProviderFactory();
            _etfService = new EtfService(_providerFactory, _cacheManager);
        }

        public ViewModels.MainViewModel MainViewModel => new(_etfService);

        public ViewModels.EtfDetailViewModel EtfDetailViewModel => new(_etfService);

        public static void Cleanup()
        {
        }
    }
}
