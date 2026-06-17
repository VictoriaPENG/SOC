using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

/// <summary>
/// 采样源抽象。
/// ViewModel 通过这个接口读取数据，因此真实 Modbus 设备和仿真数据可以无缝切换。
/// </summary>
public interface IMeasurementSource : IAsyncDisposable
{
    /// <summary>
    /// 持续输出采样点。调用方通过 cancellationToken 停止采集循环。
    /// </summary>
    IAsyncEnumerable<MeasurementSample> ReadSamplesAsync(CancellationToken cancellationToken);
}
