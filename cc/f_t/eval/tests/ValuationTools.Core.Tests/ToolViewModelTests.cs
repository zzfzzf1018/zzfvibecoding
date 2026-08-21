using System.IO;
using ValuationTools.Desktop.Models;
using ValuationTools.Desktop.ViewModels;
using ValuationTools.Desktop.ViewModels.Tools;
using Xunit;

namespace ValuationTools.Core.Tests;

/// <summary>确保每个工具用默认参数都能算出结果，且输入变化能触发重算。</summary>
public class ToolViewModelTests
{
    public static IEnumerable<object[]> AllTools() => new List<object[]>
    {
        new object[] { new DcfViewModel() },
        new object[] { new DdmViewModel() },
        new object[] { new ResidualIncomeViewModel() },
        new object[] { new PegViewModel() },
        new object[] { new RelativeValuationViewModel() },
        new object[] { new GrahamViewModel() },
        new object[] { new DiscountRateViewModel() },
        new object[] { new GrowthViewModel() },
        new object[] { new ProjectCashFlowViewModel() },
        new object[] { new BondViewModel() },
        new object[] { new OptionViewModel() }
    };

    [Theory]
    [MemberData(nameof(AllTools))]
    public void DefaultInputs_ProduceResultsWithoutError(ToolViewModel tool)
    {
        Assert.NotEmpty(tool.Groups);
        Assert.NotEmpty(tool.Results);
        Assert.False(tool.IsError, $"{tool.Title} 计算出错：{tool.Message}");
        Assert.Contains(tool.Results, r => r.IsPrimary);
    }

    [Theory]
    [MemberData(nameof(AllTools))]
    public void ReportContainsTitleAndResults(ToolViewModel tool)
    {
        string report = tool.BuildReport();
        Assert.Contains(tool.Title, report);
        Assert.Contains("计算结果", report);
    }

    [Fact]
    public void ChangingInput_TriggersRecalculation()
    {
        var tool = new DcfViewModel();
        string before = tool.Results.First(r => r.IsPrimary).Value;

        var discountRate = tool.Groups.SelectMany(g => g.Fields).OfType<NumberField>().Single(f => f.Key == "wacc");
        discountRate.Value += 2;

        string after = tool.Results.First(r => r.IsPrimary).Value;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ResetCommand_RestoresDefaultResult()
    {
        var tool = new PegViewModel();
        string original = tool.Results.First(r => r.IsPrimary).Value;

        var growth = tool.Groups.SelectMany(g => g.Fields).OfType<NumberField>().Single(f => f.Key == "growth");
        growth.Value = 40;
        Assert.NotEqual(original, tool.Results.First(r => r.IsPrimary).Value);

        tool.ResetCommand.Execute(null);
        Assert.Equal(original, tool.Results.First(r => r.IsPrimary).Value);
    }

    [Fact]
    public void InvalidInput_ShowsErrorInsteadOfCrashing()
    {
        var tool = new DcfViewModel();
        var terminalGrowth = tool.Groups.SelectMany(g => g.Fields).OfType<NumberField>().Single(f => f.Key == "tg");
        terminalGrowth.Value = 50; // 永续增长率高于折现率

        Assert.True(tool.IsError);
        Assert.False(string.IsNullOrWhiteSpace(tool.Message));
    }

    [Fact]
    public void ErrorState_ClearsStaleResultsAndTables()
    {
        var tool = new DcfViewModel();
        Assert.NotEmpty(tool.Results);
        Assert.NotNull(tool.Schedule);
        Assert.NotNull(tool.Sensitivity);

        var years = tool.Groups.SelectMany(g => g.Fields).OfType<NumberField>().Single(f => f.Key == "y1");
        years.Value = 500;

        Assert.True(tool.IsError);
        Assert.Empty(tool.Results);
        Assert.Null(tool.Schedule);
        Assert.Null(tool.Sensitivity);
    }

    [Fact]
    public void Report_UsesReadableTableHeaders()
    {
        string report = new DcfViewModel().BuildReport();

        Assert.Contains("自由现金流", report);
        Assert.Contains("永续增长率 \\ 折现率", report);
        Assert.DoesNotContain("C0\tC1", report);
    }

    [Fact]
    public void ExportFileName_StripsCharactersIllegalInWindowsPaths()
    {
        var invalid = Path.GetInvalidFileNameChars();

        foreach (var tool in new MainViewModel().Tools)
        {
            string fileName = ToolViewModel.SanitizeFileName(tool.Title);
            Assert.False(string.IsNullOrWhiteSpace(fileName));
            Assert.DoesNotContain(fileName, c => invalid.Contains(c));
        }
    }

    [Fact]
    public void MainViewModel_SearchFiltersTools()
    {
        var main = new MainViewModel();
        Assert.Equal(11, main.Tools.Count);

        main.SearchText = "PEG";
        var filtered = main.FilteredTools.Cast<ToolViewModel>().ToList();
        Assert.InRange(filtered.Count, 1, 10);
        Assert.Contains(filtered, t => t is PegViewModel);

        main.SearchText = string.Empty;
        Assert.Equal(11, main.FilteredTools.Cast<ToolViewModel>().Count());
    }
}
