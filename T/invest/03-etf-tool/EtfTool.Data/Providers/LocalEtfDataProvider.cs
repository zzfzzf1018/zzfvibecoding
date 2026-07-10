

using System.Collections.Generic;
using System.IO;
using EtfTool.Core.Interfaces;
using EtfTool.Core.Models;
using Newtonsoft.Json;

namespace EtfTool.Data.Providers
{
    public class LocalEtfDataProvider : IEtfDataProvider
    {
        private readonly List<EtfInfo> _etfDatabase = new();

        public LocalEtfDataProvider()
        {
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            try
            {
                var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var dataPath = Path.Combine(assemblyDir ?? AppDomain.CurrentDomain.BaseDirectory, "Data", "etf_database.json");
                
                if (!File.Exists(dataPath))
                {
                    dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "etf_database.json");
                }
                
                if (!File.Exists(dataPath))
                {
                    var projectDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../Data/etf_database.json");
                    if (File.Exists(projectDataPath))
                    {
                        dataPath = projectDataPath;
                    }
                }

                if (File.Exists(dataPath))
                {
                    var json = File.ReadAllText(dataPath);
                    var etfs = JsonConvert.DeserializeObject<List<EtfInfo>>(json);
                    if (etfs != null)
                    {
                        _etfDatabase.AddRange(etfs);
                    }
                }
            }
            catch
            {
            }
        }

        public Task<EtfInfo?> GetEtfInfoAsync(string etfCode)
        {
            var etf = _etfDatabase.FirstOrDefault(e => e.Code == etfCode);
            return Task.FromResult<EtfInfo?>(etf);
        }

        public Task<List<EtfComponent>> GetEtfComponentsAsync(string etfCode)
        {
            return Task.FromResult(new List<EtfComponent>());
        }

        public Task<List<KlineData>> GetKlineDataAsync(string etfCode, string period = "day", int count = 120)
        {
            return Task.FromResult(new List<KlineData>());
        }

        public Task<List<EtfDividend>> GetEtfDividendsAsync(string etfCode)
        {
            return Task.FromResult(new List<EtfDividend>());
        }

        public Task<List<EtfInfo>> SearchEtfAsync(string keyword)
        {
            var results = _etfDatabase
                .Where(e => 
                    e.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    e.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            return Task.FromResult(results);
        }
    }
}
