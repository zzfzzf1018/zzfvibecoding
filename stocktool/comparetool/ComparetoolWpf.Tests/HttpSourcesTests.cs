using System.Net;
using System.Net.Http;
using System.Text;
using ComparetoolWpf.Models;
using ComparetoolWpf.Services;
using Xunit;

namespace ComparetoolWpf.Tests;

public class HttpSourcesTests
{
    // ===== EastMoney =====

    [Fact]
    public async Task EastMoney_Search_ParsesCodes()
    {
        var json = "{\"QuotationCodeTable\":{\"Data\":[" +
                   "{\"Code\":\"600000\",\"Name\":\"浦发银行\",\"MktNum\":\"1\"}," +
                   "{\"Code\":\"000001\",\"Name\":\"平安银行\",\"MktNum\":\"0\"}," +
                   "{\"Code\":\"00700\",\"Name\":\"腾讯\",\"MktNum\":\"116\"}," +
                   "{\"Code\":\"abcdef\",\"Name\":\"bad\",\"MktNum\":\"1\"}" +
                   "]}}";
        var svc = new EastMoneyService(new StubHttpMessageHandler(json));
        var r = await svc.SearchStocksAsync("银行");
        Assert.Equal(2, r.Count);
        Assert.Contains(r, s => s.FullCode == "SH600000");
        Assert.Contains(r, s => s.FullCode == "SZ000001");
    }

    [Fact]
    public async Task EastMoney_Search_EmptyKeyword_ReturnsEmpty()
    {
        var svc = new EastMoneyService(new StubHttpMessageHandler("{}"));
        Assert.Empty(await svc.SearchStocksAsync(""));
    }

    [Fact]
    public async Task EastMoney_Search_MissingTable_ReturnsEmpty()
    {
        var svc = new EastMoneyService(new StubHttpMessageHandler("{\"foo\":1}"));
        Assert.Empty(await svc.SearchStocksAsync("x"));
    }

    [Fact]
    public async Task EastMoney_GetReports_Income_HappyPath()
    {
        var dateJson = "[{\"REPORT_DATE\":\"2024-12-31 00:00:00\",\"DATATYPE\":\"2024年报\"}," +
                       " {\"REPORT_DATE\":\"2023-12-31 00:00:00\",\"DATATYPE\":\"2023年报\"}]";
        var rowsJson = "[{\"REPORT_DATE\":\"2024-12-31\",\"DATATYPE\":\"年报\"," +
                       " \"TOTAL_OPERATE_INCOME\":1000,\"PARENT_NETPROFIT\":100}," +
                       " {\"REPORT_DATE\":\"2023-12-31\",\"DATATYPE\":\"年报\"," +
                       " \"TOTAL_OPERATE_INCOME\":800,\"PARENT_NETPROFIT\":80}]";
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            var u = req.RequestUri!.ToString();
            if (u.Contains("DateAjaxNew")) return StubHttpMessageHandler.Json(dateJson);
            if (u.Contains("AjaxNew")) return StubHttpMessageHandler.Json(rowsJson);
            return StubHttpMessageHandler.Json("[]");
        });
        var svc = new EastMoneyService(handler);
        var got = await svc.GetReportsAsync(new StockInfo("600000", "浦发", "SH"),
            ReportKind.Income, ReportPeriodType.Annual);
        Assert.Equal(2, got.Count);
        Assert.Equal(new DateTime(2024, 12, 31), got[0].ReportDate);
        Assert.Equal(1000, got[0].Items["营业总收入"]);
    }

    [Fact]
    public async Task EastMoney_GetReports_AllCompanyTypesFail_Throws()
    {
        // 总是空数组 → 所有 companyType 都会失败
        var handler = new StubHttpMessageHandler("[]");
        var svc = new EastMoneyService(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetReportsAsync(new StockInfo("600000", "x", "SH"), ReportKind.Balance));
    }

    // ===== Sina =====

    [Fact]
    public async Task Sina_Search_ParsesGbkPayload()
    {
        // 真实返回字段顺序：name,type,code,fullCode,...
        var body = "var suggestvalue=\"浦发银行,11,600000,sh600000,puhua,,浦发银行,99,1,ESG,,;" +
                   "平安银行,11,000001,sz000001,pingan,,平安银行,99,1,,,;" +
                   "bad,11,bad,bad,bad\";";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding("GBK").GetBytes(body);
        var handler = new StubHttpMessageHandler((_, _) => StubHttpMessageHandler.Bytes(bytes));
        var svc = new SinaStockSource(handler);
        var r = await svc.SearchStocksAsync("银行");
        Assert.Equal(2, r.Count);
        Assert.Contains(r, s => s.FullCode == "SH600000" && s.Name == "浦发银行");
        Assert.Contains(r, s => s.FullCode == "SZ000001" && s.Name == "平安银行");
    }

    [Fact]
    public async Task Sina_Search_Empty_ReturnsEmpty()
    {
        var svc = new SinaStockSource(new StubHttpMessageHandler("var suggestvalue=\"\";"));
        Assert.Empty(await svc.SearchStocksAsync("x"));
        Assert.Empty(await svc.SearchStocksAsync(""));
    }

    [Fact]
    public async Task Sina_Search_NoQuoted_ReturnsEmpty()
    {
        var svc = new SinaStockSource(new StubHttpMessageHandler("garbage no quotes"));
        Assert.Empty(await svc.SearchStocksAsync("x"));
    }

    [Fact]
    public async Task Sina_GetReports_Throws_NotSupported()
    {
        var svc = new SinaStockSource(new StubHttpMessageHandler(""));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            svc.GetReportsAsync(new StockInfo("1", "n", "SH"), ReportKind.Income));
    }

    // ===== Xueqiu =====

    [Fact]
    public async Task Xueqiu_Search_ParsesData()
    {
        var json = "{\"data\":[" +
                   "{\"code\":\"SH600000\",\"name\":\"浦发银行\",\"query\":\"600000\"}," +
                   "{\"code\":\"SZ000001\",\"name\":\"平安银行\",\"query\":\"000001\"}," +
                   "{\"code\":\"HK00700\",\"name\":\"腾讯\",\"query\":\"00700\"}" +
                   "]}";
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            var u = req.RequestUri!.ToString();
            if (u.Contains("xueqiu.com/query")) return StubHttpMessageHandler.Json(json);
            return StubHttpMessageHandler.Json("{}");          // bootstrap homepage
        });
        var svc = new XueqiuStockSource(handler);
        var r = await svc.SearchStocksAsync("银行");
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public async Task Xueqiu_Search_Empty_ReturnsEmpty()
    {
        var svc = new XueqiuStockSource(new StubHttpMessageHandler("{}"));
        Assert.Empty(await svc.SearchStocksAsync(""));
    }

    [Fact]
    public async Task Xueqiu_GetReports_Income_HappyPath()
    {
        var json = "{\"data\":{\"list\":[" +
                   "{\"report_date\":1735603200000,\"report_name\":\"2024年报\"," +
                   " \"total_revenue\":[1000,0.25,null]," +
                   " \"net_profit_atsopc\":[100,0.1,null]" +
                   "}]}}";
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            var u = req.RequestUri!.ToString();
            if (u.Contains("/finance/cn/")) return StubHttpMessageHandler.Json(json);
            return StubHttpMessageHandler.Json("{}");
        });
        var svc = new XueqiuStockSource(handler);
        var r = await svc.GetReportsAsync(new StockInfo("600000", "浦发", "SH"),
            ReportKind.Income, ReportPeriodType.Annual);
        Assert.Single(r);
    }

    [Theory]
    [InlineData(ReportKind.Balance)]
    [InlineData(ReportKind.CashFlow)]
    public async Task Xueqiu_GetReports_OtherKinds_NoData_ReturnsEmpty(ReportKind k)
    {
        var handler = new StubHttpMessageHandler("{}");
        var svc = new XueqiuStockSource(handler);
        Assert.Empty(await svc.GetReportsAsync(new StockInfo("1", "n", "SH"), k));
    }

    [Fact]
    public async Task Xueqiu_Search_ApiError_Throws_ForFallback()
    {
        // WAF 未通过时雪球返回 {"code":400016,"success":false}
        var handler = new StubHttpMessageHandler("{\"code\":400016,\"message\":\"\",\"success\":false}");
        var svc = new XueqiuStockSource(handler);
        await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(
            () => svc.SearchStocksAsync("x"));
    }

    [Fact]
    public async Task Xueqiu_GetReports_ApiError_Throws_ForFallback()
    {
        var handler = new StubHttpMessageHandler("{\"error_code\":\"400016\",\"error_description\":\"请求被拦截\"}");
        var svc = new XueqiuStockSource(handler);
        var ex = await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(
            () => svc.GetReportsAsync(new StockInfo("600000", "n", "SH"), ReportKind.Income));
        Assert.Contains("请求被拦截", ex.Message);
    }
}
