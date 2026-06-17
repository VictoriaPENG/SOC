using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

public interface IMeasurementSource : IAsyncDisposable
{
    IAsyncEnumerable<MeasurementSample> ReadSamplesAsync(CancellationToken cancellationToken);
}
