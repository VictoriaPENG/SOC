using System.Globalization;
using System.IO;
using System.Text;
using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

public sealed class CsvDataRecorder : IDisposable
{
    private readonly StreamWriter _writer;

    public CsvDataRecorder(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        FilePath = filePath;
        _writer = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        _writer.WriteLine(
            "timestamp,algorithm_name,engineering_ch1,engineering_ch2,quality_ch1,quality_ch2,flow_ch1,flow_ch2,flow_status_ch1,flow_status_ch2,total_integer_ch1,total_integer_ch2,timed_total_ch1,timed_total_ch2,daily_total_ch1,daily_total_ch2,monthly_total_ch1,monthly_total_ch2,algorithm_estimate");
    }

    public string FilePath { get; }

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

    private static string Format(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    public void Dispose() => _writer.Dispose();
}
