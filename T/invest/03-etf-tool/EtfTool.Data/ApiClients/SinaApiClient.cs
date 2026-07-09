
using System.Net.Http.Headers;
using System.Text;
using EtfTool.Core.Interfaces;
using EtfTool.Core.Models;
using Newtonsoft.Json;

namespace EtfTool.Data.ApiClients
{
    public class SinaApiClient : IEtfDataProvider
    {
        private readonly HttpClient _httpClient;

        public SinaApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<EtfInfo?> GetEtfInfoAsync(string etfCode)
        {
            try
            {
                var market = etfCode.StartsWith("5") ? "sh" : "sz";
                var url = $"https://hq.sinajs.cn/list={market}{etfCode}";
                var response = await _httpClient.GetStringAsync(url);
                
                if (!string.IsNullOrEmpty(response))
                {
                    var data = ParseSinaHqData(response);
                    if (data != null && data.Count >= 5)
                    {
                        return new EtfInfo
                        {
                            Code = etfCode,
                            Name = data[0],
                            LatestPrice = decimal.TryParse(data[3], out var price) ? price : null,
                            ChangePercent = decimal.TryParse(data[4], out var change) ? change : null,
                            UpdateTime = DateTime.Now
                        };
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public async Task<List<EtfComponent>> GetEtfComponentsAsync(string etfCode)
        {
            var components = new List<EtfComponent>();
            try
            {
                var market = etfCode.StartsWith("5") ? "SH" : "SZ";
                var url = $"https://money.finance.sina.com.cn/dzjy/yy/etf/index/{market}{etfCode}.html";
                var response = await _httpClient.GetStringAsync(url);
                
                var stockCodes = ExtractStockCodesFromHtml(response);
                foreach (var code in stockCodes.Take(50))
                {
                    components.Add(new EtfComponent
                    {
                        EtfCode = etfCode,
                        StockCode = code,
                        StockName = code,
                        UpdateTime = DateTime.Now
                    });
                }
            }
            catch (Exception)
            {
            }
            return components;
        }

        public async Task<List<KlineData>> GetKlineDataAsync(string etfCode, string period = "day", int count = 120)
        {
            var klineData = new List<KlineData>();
            try
            {
                var market = etfCode.StartsWith("5") ? "sh" : "sz";
                var periodParam = period switch
                {
                    "week" => "W",
                    "month" => "M",
                    _ => "day"
                };
                var url = $"https://finance.sina.com.cn/stock/api/jsonp.php/var%20_{market}{etfCode}_{periodParam}=/CN_MarketData.getKLineData?symbol={market}{etfCode}&scale=5&ma=5,10,20&datalen={count}";
                var response = await _httpClient.GetStringAsync(url);
                
                var jsonStart = response.IndexOf("[", StringComparison.Ordinal);
                var jsonEnd = response.LastIndexOf("]", StringComparison.Ordinal);
                
                if (jsonStart >= 0 && jsonEnd >= 0)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var data = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                    
                    if (data != null)
                    {
                        foreach (var item in data)
                        {
                            klineData.Add(new KlineData
                            {
                                EtfCode = etfCode,
                                Date = DateTime.TryParse(item.GetValueOrDefault("day")?.ToString(), out var date) ? date : DateTime.MinValue,
                                Open = decimal.TryParse(item.GetValueOrDefault("open")?.ToString(), out var open) ? open : 0,
                                High = decimal.TryParse(item.GetValueOrDefault("high")?.ToString(), out var high) ? high : 0,
                                Low = decimal.TryParse(item.GetValueOrDefault("low")?.ToString(), out var low) ? low : 0,
                                Close = decimal.TryParse(item.GetValueOrDefault("close")?.ToString(), out var close) ? close : 0,
                                Volume = decimal.TryParse(item.GetValueOrDefault("volume")?.ToString(), out var volume) ? volume : 0,
                                Amount = decimal.TryParse(item.GetValueOrDefault("amount")?.ToString(), out var amount) ? amount : 0
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return klineData;
        }

        public async Task<List<EtfDividend>> GetEtfDividendsAsync(string etfCode)
        {
            return new List<EtfDividend>();
        }

        public async Task<List<EtfInfo>> SearchEtfAsync(string keyword)
        {
            var etfs = new List<EtfInfo>();
            try
            {
                var url = $"https://suggest.sinajs.cn/suggest/type=11&key={Uri.EscapeDataString(keyword)}";
                var response = await _httpClient.GetStringAsync(url);
                
                var data = ParseSinaSuggestData(response);
                foreach (var item in data)
                {
                    etfs.Add(new EtfInfo
                    {
                        Code = item.Item1,
                        Name = item.Item2,
                        UpdateTime = DateTime.Now
                    });
                }
            }
            catch (Exception)
            {
            }
            return etfs;
        }

        private List<string> ParseSinaHqData(string response)
        {
            try
            {
                var start = response.IndexOf("\"", StringComparison.Ordinal);
                var end = response.LastIndexOf("\"", StringComparison.Ordinal);
                
                if (start >= 0 && end >= 0)
                {
                    var dataStr = response.Substring(start + 1, end - start - 1);
                    return dataStr.Split(",").ToList();
                }
            }
            catch (Exception)
            {
            }
            return new List<string>();
        }

        private List<string> ExtractStockCodesFromHtml(string html)
        {
            var codes = new List<string>();
            try
            {
                var pattern = @"[sS][hH]\d{6}|[sS][zZ]\d{6}";
                var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern);
                
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var code = match.Value.ToUpper();
                    if (!codes.Contains(code))
                    {
                        codes.Add(code.Substring(2));
                    }
                }
            }
            catch (Exception)
            {
            }
            return codes;
        }

        private List<Tuple<string, string>> ParseSinaSuggestData(string response)
        {
            var result = new List<Tuple<string, string>>();
            try
            {
                var start = response.IndexOf("\"", StringComparison.Ordinal);
                var end = response.LastIndexOf("\"", StringComparison.Ordinal);
                
                if (start >= 0 && end >= 0)
                {
                    var dataStr = response.Substring(start + 1, end - start - 1);
                    var items = dataStr.Split(";");
                    
                    foreach (var item in items)
                    {
                        var parts = item.Split(",");
                        if (parts.Length >= 2)
                        {
                            result.Add(Tuple.Create(parts[0], parts[1]));
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return result;
        }
    }
}
