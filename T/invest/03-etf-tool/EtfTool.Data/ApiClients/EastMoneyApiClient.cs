
using System.Net.Http.Headers;
using EtfTool.Core.Interfaces;
using EtfTool.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EtfTool.Data.ApiClients
{
    public class EastMoneyApiClient : IEtfDataProvider
    {
        private readonly HttpClient _httpClient;

        public EastMoneyApiClient()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<EtfInfo?> GetEtfInfoAsync(string etfCode)
        {
            try
            {
                var market = etfCode.StartsWith("5") ? "1" : "0";
                var url = $"https://push2.eastmoney.com/api/qt/stock/get?secid={market}.{etfCode}&fields=f57,f58,f116,f117,f115,f43,f44,f45,f46,f51,f168,f169,f170,f171,f172,f173,f174,f175,f176,f177,f178,f179,f180,f181,f182,f183,f184,f185,f186,f187,f188,f189,f190,f191,f192,f193,f194,f195,f196,f197,f198,f199,f200,f201,f202,f203,f204,f205,f206,f207,f208,f209,f210,f211,f212,f213,f214,f215,f216,f217,f218,f219,f220,f221,f222,f223,f224,f225,f226,f227,f228,f229,f230,f231,f232,f233,f234,f235,f236,f237,f238,f239,f240,f241,f242,f243,f244,f245,f246,f247,f248,f249,f250,f251,f252,f253,f254,f255,f256,f257,f258,f259,f260,f261,f262,f263,f264,f265,f266,f267,f268,f269,f270,f271,f272,f273,f274,f275,f276,f277,f278,f279,f280,f281,f282,f283,f284,f285,f286,f287,f288,f289,f290,f291,f292,f293,f294,f295,f296,f297,f298,f299,f300,f301,f302,f303,f304,f305,f306,f307,f308,f309,f310,f311,f312,f313,f314,f315,f316,f317,f318,f319,f320,f321,f322,f323,f324,f325,f326,f327,f328,f329,f330,f331,f332,f333,f334,f335,f336,f337,f338,f339,f340,f341,f342,f343,f344,f345,f346,f347,f348,f349,f350,f351,f352,f353,f354,f355,f356,f357,f358,f359,f360,f361,f362,f363,f364,f365,f366,f367,f368,f369,f370,f371,f372,f373,f374,f375,f376,f377,f378,f379,f380,f381,f382,f383,f384,f385,f386,f387,f388,f389,f390,f391,f392,f393,f394,f395,f396,f397,f398,f399";
                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<JObject>(response);
                
                if (json?["data"] != null)
                {
                    var data = json["data"];
                    return new EtfInfo
                    {
                        Code = etfCode,
                        Name = data["f58"]?.ToString() ?? string.Empty,
                        FullName = data["f116"]?.ToString() ?? string.Empty,
                        LatestPrice = data["f43"]?.Value<decimal>(),
                        ChangePercent = data["f44"]?.Value<decimal>(),
                        TotalAssets = data["f115"]?.Value<decimal>(),
                        Unit = data["f117"]?.Value<decimal>(),
                        PeRatio = data["f168"]?.Value<decimal>(),
                        PbRatio = data["f169"]?.Value<decimal>(),
                        UpdateTime = DateTime.Now
                    };
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
                var market = etfCode.StartsWith("5") ? "1" : "0";
                var url = $"https://datacenter.eastmoney.com/api/data/v1/get?reportName=RPT_FUND_PORTFOLIO_STOCK&columns=SECURITY_CODE,SECURITY_NAME,WEIGHT,RANK&filter=(FUND_CODE=\"{etfCode}\")(REPORT_DATE=%272024-09-30%27)&pageNumber=1&pageSize=100&sortColumns=RANK&sortTypes=1";
                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<JObject>(response);
                
                if (json?["result"]?["data"] is JArray dataArray)
                {
                    foreach (var item in dataArray)
                    {
                        components.Add(new EtfComponent
                        {
                            EtfCode = etfCode,
                            StockCode = item["SECURITY_CODE"]?.ToString() ?? string.Empty,
                            StockName = item["SECURITY_NAME"]?.ToString() ?? string.Empty,
                            Weight = item["WEIGHT"]?.Value<decimal>(),
                            Rank = item["RANK"]?.Value<int>(),
                            UpdateTime = DateTime.Now
                        });
                    }
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
                var market = etfCode.StartsWith("5") ? "1" : "0";
                var periodParam = period switch
                {
                    "week" => "102",
                    "month" => "103",
                    _ => "101"
                };
                var url = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={market}.{etfCode}&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61&klt={periodParam}&fqt=1&end=20991231&lmt={count}";
                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<JObject>(response);
                
                if (json?["data"]?["klines"] is JArray klineArray)
                {
                    foreach (var item in klineArray)
                    {
                        var parts = item.ToString().Split(",");
                        if (parts.Length >= 7)
                        {
                            klineData.Add(new KlineData
                            {
                                EtfCode = etfCode,
                                Date = DateTime.TryParse(parts[0], out var date) ? date : DateTime.MinValue,
                                Open = decimal.TryParse(parts[1], out var open) ? open : 0,
                                Close = decimal.TryParse(parts[2], out var close) ? close : 0,
                                Low = decimal.TryParse(parts[3], out var low) ? low : 0,
                                High = decimal.TryParse(parts[4], out var high) ? high : 0,
                                Volume = decimal.TryParse(parts[5], out var volume) ? volume : 0,
                                Amount = decimal.TryParse(parts[6], out var amount) ? amount : 0
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
            var dividends = new List<EtfDividend>();
            try
            {
                var url = $"https://datacenter.eastmoney.com/api/data/v1/get?reportName=RPT_FUND_DIVIDEND&columns=FUND_CODE,FUND_NAME,DIVIDEND_DATE,DIVIDEND_AMOUNT,DIVIDEND_TYPE&filter=(FUND_CODE=\"{etfCode}\")&pageNumber=1&pageSize=50&sortColumns=DIVIDEND_DATE&sortTypes=-1";
                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<JObject>(response);
                
                if (json?["result"]?["data"] is JArray dataArray)
                {
                    foreach (var item in dataArray)
                    {
                        dividends.Add(new EtfDividend
                        {
                            EtfCode = etfCode,
                            DividendDate = item["DIVIDEND_DATE"]?.Value<DateTime>(),
                            DividendPerUnit = item["DIVIDEND_AMOUNT"]?.Value<decimal>(),
                            DividendType = item["DIVIDEND_TYPE"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }
            catch (Exception)
            {
            }
            return dividends;
        }

        public async Task<List<EtfInfo>> SearchEtfAsync(string keyword)
        {
            var etfs = new List<EtfInfo>();
            try
            {
                var url = $"https://searchapi.eastmoney.com/suggest/get?input={Uri.EscapeDataString(keyword)}&type=14&token=D43BF722C8E33BDC906FB84D85E25476&count=10";
                var response = await _httpClient.GetStringAsync(url);
                var json = JsonConvert.DeserializeObject<JObject>(response);
                
                if (json?["QuotationCodeTable"]?["Data"] is JArray dataArray)
                {
                    foreach (var item in dataArray)
                    {
                        etfs.Add(new EtfInfo
                        {
                            Code = item["Code"]?.ToString() ?? string.Empty,
                            Name = item["Name"]?.ToString() ?? string.Empty,
                            UpdateTime = DateTime.Now
                        });
                    }
                }
            }
            catch (Exception)
            {
            }
            return etfs;
        }
    }
}
