using System.Globalization;
using System.IO;
using System.Text;
using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

/// <summary>
/// 实时 CSV 数据记录器。
/// 点击“保存数据”并选择文件后创建，采集过程中每收到一帧估计结果就追加一行。
/// </summary>
public sealed class CsvDataRecorder : IDisposable
{
    private readonly StreamWriter _writer;

    public CsvDataRecorder(string filePath)
    {
        // 用户可能选择一个尚不存在的目录，写文件前先确保目录存在。
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        FilePath = filePath;

        // 写入 UTF-8 BOM，便于 Excel 在中文 Windows 环境下直接识别编码。
        _writer = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _writer.WriteLine(
            "timestamp,algorithm_name,engineering_ch1,engineering_ch2,quality_ch1,quality_ch2,flow_ch1,flow_ch2,flow_status_ch1,flow_status_ch2,total_integer_ch1,total_integer_ch2,timed_total_ch1,timed_total_ch2,daily_total_ch1,daily_total_ch2,monthly_total_ch1,monthly_total_ch2,algorithm_estimate");
    }

    /// <summary>
    /// 当前 CSV 文件完整路径，用于界面状态栏显示。
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 追加一帧采样/估计结果，并立即 Flush，降低程序异常退出时的数据丢失风险。
    /// </summary>
    public void Append(MeasurementSample sample, string algorithmName)
    {
        string[] values =
        [
            sample.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            Escape(algorithmName),
            Format(sample.EngineeringChannel1),
            Format(sample.EngineeringChannel2),
            sample.QualityChannel1.ToString(CultureInfo.InvariantCulture),
            sample.QualityChannel2.ToString(CultureInfo.InvariantCulture),
            Format(sample.FlowChannel1),
            Format(sample.FlowChannel2),
            sample.FlowStatusChannel1.ToString(CultureInfo.InvariantCulture),
            sample.FlowStatusChannel2.ToString(CultureInfo.InvariantCulture),
            sample.TotalIntegerChannel1.ToString(CultureInfo.InvariantCulture),
            sample.TotalIntegerChannel2.ToString(CultureInfo.InvariantCulture),
            Format(sample.TimedTotalChannel1),
            Format(sample.TimedTotalChannel2),
            Format(sample.DailyTotalChannel1),
            Format(sample.DailyTotalChannel2),
            Format(sample.MonthlyTotalChannel1),
            Format(sample.MonthlyTotalChannel2),
            Format(sample.AlgorithmEstimate)
        ];

        _writer.WriteLine(string.Join(",", values));
        _writer.Flush();
    }

    // 使用 G17 保留 double 的完整有效精度，方便后续离线算法复现。
    private static string Format(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

    // CSV 中字符串字段用双引号包裹，内部双引号按 CSV 规范转义成两个双引号。
    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    public void Dispose() => _writer.Dispose();
}
