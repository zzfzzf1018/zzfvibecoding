using StockTechAnalyzer.Models;

namespace StockTechAnalyzer.ML;

/// <summary>
/// 轻量逻辑回归预测器：
/// - 标准化特征 (z-score)
/// - 二分类 (次日涨/跌)
/// - 批量梯度下降 + L2 正则
/// - 走样本外回测评估准确率
///
/// 不引入任何第三方机器学习依赖，避免 ~100MB 原生包。
/// 适用规模：单只股票 ~250 根日 K 训练，秒级完成。
/// </summary>
internal sealed class PricePredictor
{
    public sealed record TrainResult(
        int Samples,
        double TrainAccuracy,
        double TestAccuracy,
        double UpProbability,
        string Direction,
        string Recommendation,
        IReadOnlyList<(string Name, double Weight)> FeatureImportance,
        string Note);

    public TrainResult Train(IReadOnlyList<Kline> bars,
        double learningRate = 0.05, int epochs = 800, double l2 = 0.01,
        double testRatio = 0.25)
    {
        var (X, y, names) = FeatureBuilder.Build(bars);
        if (X.Length < 60)
        {
            return new TrainResult(X.Length, 0, 0, 0.5, "样本不足", "需要至少 80 根 K 线",
                Array.Empty<(string, double)>(),
                "数据样本太少，无法可靠训练。");
        }

        // 标准化
        int d = X[0].Length;
        var mean = new double[d];
        var std = new double[d];
        for (int j = 0; j < d; j++)
        {
            double s = 0; for (int i = 0; i < X.Length; i++) s += X[i][j];
            mean[j] = s / X.Length;
            double v = 0; for (int i = 0; i < X.Length; i++) { double dx = X[i][j] - mean[j]; v += dx * dx; }
            std[j] = Math.Sqrt(v / X.Length); if (std[j] < 1e-9) std[j] = 1;
        }
        var Xn = X.Select(row =>
        {
            var r = new double[d]; for (int j = 0; j < d; j++) r[j] = (row[j] - mean[j]) / std[j];
            return r;
        }).ToArray();

        int testSize = Math.Max(10, (int)(X.Length * testRatio));
        int trainSize = X.Length - testSize;

        var w = new double[d]; double b = 0;
        var rnd = new Random(42);
        for (int j = 0; j < d; j++) w[j] = (rnd.NextDouble() - 0.5) * 0.01;

        for (int ep = 0; ep < epochs; ep++)
        {
            var gw = new double[d]; double gb = 0;
            for (int i = 0; i < trainSize; i++)
            {
                double z = b; for (int j = 0; j < d; j++) z += w[j] * Xn[i][j];
                double p = Sigmoid(z);
                double err = p - y[i];
                for (int j = 0; j < d; j++) gw[j] += err * Xn[i][j];
                gb += err;
            }
            for (int j = 0; j < d; j++) w[j] -= learningRate * (gw[j] / trainSize + l2 * w[j]);
            b -= learningRate * gb / trainSize;
        }

        int trainCorrect = 0, testCorrect = 0;
        for (int i = 0; i < trainSize; i++)
        {
            double z = b; for (int j = 0; j < d; j++) z += w[j] * Xn[i][j];
            int pred = Sigmoid(z) >= 0.5 ? 1 : 0;
            if (pred == y[i]) trainCorrect++;
        }
        for (int i = trainSize; i < X.Length; i++)
        {
            double z = b; for (int j = 0; j < d; j++) z += w[j] * Xn[i][j];
            int pred = Sigmoid(z) >= 0.5 ? 1 : 0;
            if (pred == y[i]) testCorrect++;
        }
        double trainAcc = (double)trainCorrect / trainSize;
        double testAcc = (double)testCorrect / testSize;

        // 当前样本预测下一交易日方向
        var latest = FeatureBuilder.BuildLatest(bars);
        double upProb = 0.5;
        if (latest != null)
        {
            var ln = new double[d]; for (int j = 0; j < d; j++) ln[j] = (latest[j] - mean[j]) / std[j];
            double z = b; for (int j = 0; j < d; j++) z += w[j] * ln[j];
            upProb = Sigmoid(z);
        }

        string direction = upProb >= 0.6 ? "看涨 ↑" : upProb <= 0.4 ? "看跌 ↓" : "震荡 ⇄";
        string recommend = upProb >= 0.65 ? "强信号：模型偏多，可关注主升机会"
            : upProb >= 0.55 ? "弱信号：偏多，可小仓观察"
            : upProb >= 0.45 ? "中性：建议观望"
            : upProb >= 0.35 ? "弱信号：偏空，注意风险"
            : "强信号：模型偏空，建议规避";

        // 特征重要性 = |标准化权重|（已经标准化所以可比）
        var fi = new List<(string Name, double Weight)>();
        for (int j = 0; j < Math.Min(names.Length, d); j++) fi.Add((names[j], w[j]));
        fi = fi.OrderByDescending(x => Math.Abs(x.Weight)).ToList();

        string note = testAcc >= 0.58
            ? "样本外准确率较高，模型对该股近期模式有一定把握。"
            : testAcc >= 0.52
            ? "样本外准确率略高于随机，仅供辅助参考。"
            : "样本外准确率接近随机 (50%)，模型对该股可预测性弱，建议忽略。";

        return new TrainResult(X.Length, trainAcc, testAcc, upProb, direction, recommend, fi, note);
    }

    private static double Sigmoid(double z) => 1.0 / (1.0 + Math.Exp(-z));

    public static string Format(TrainResult r)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("══════ 短线方向预测（轻量逻辑回归）══════");
        sb.AppendLine($"训练样本：{r.Samples}    训练准确率：{r.TrainAccuracy:P1}    样本外准确率：{r.TestAccuracy:P1}");
        sb.AppendLine();
        sb.AppendLine($"下一交易日上涨概率：{r.UpProbability:P1}");
        sb.AppendLine($"方向判断：{r.Direction}");
        sb.AppendLine($"操作建议：{r.Recommendation}");
        sb.AppendLine();
        sb.AppendLine("—— 特征重要性 (按 |权重| 降序) ——");
        foreach (var (name, w) in r.FeatureImportance)
        {
            string bar = new string(w >= 0 ? '+' : '-', Math.Min(20, (int)(Math.Abs(w) * 8)));
            sb.AppendLine($"  {name,-18} {w,8:F3}  {bar}");
        }
        sb.AppendLine();
        sb.AppendLine("📝 " + r.Note);
        sb.AppendLine();
        sb.AppendLine("⚠️ 重要说明：");
        sb.AppendLine("  • 这是一个无记忆的统计模型，仅依赖近期价格/指标模式；");
        sb.AppendLine("  • 不能感知政策、突发新闻、业绩雷等基本面冲击；");
        sb.AppendLine("  • 当样本外准确率 < 55% 时，建议视为无信号；");
        sb.AppendLine("  • 永远不要把单一模型输出当作买卖决策依据。");
        return sb.ToString();
    }
}
