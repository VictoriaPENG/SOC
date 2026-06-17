using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

public sealed class SimulatedMeasurementSource : IMeasurementSource
{
    private readonly TimeSpan _interval;
    private double _phase;

    public SimulatedMeasurementSource(TimeSpan interval)
    {
        _interval = interval;
    }

    public async IAsyncEnumerable<MeasurementSample> ReadSamplesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _phase += 0.15;
            yield return new MeasurementSample(
                DateTimeOffset.Now,
                EngineeringChannel1: 1.2 + Math.Sin(_phase) * 0.15,
                EngineeringChannel2: 2.4 + Math.Cos(_phase * 0.8) * 0.2,
                QualityChannel1: 0,
                QualityChannel2: 0,
                FlowChannel1: 12 + Math.Sin(_phase * 0.7) * 1.8,
                FlowChannel2: 15 + Math.Cos(_phase * 0.5) * 1.4,
                FlowStatusChannel1: 0,
                FlowStatusChannel2: 0,
                TotalIntegerChannel1: (int)(1000 + _phase * 5),
                TotalIntegerChannel2: (int)(2000 + _phase * 4),
                TimedTotalChannel1: 40 + Math.Sin(_phase * 0.2) * 2,
                TimedTotalChannel2: 48 + Math.Cos(_phase * 0.2) * 2,
                DailyTotalChannel1: 180 + _phase * 0.3,
                DailyTotalChannel2: 210 + _phase * 0.25,
                MonthlyTotalChannel1: 4200 + _phase * 1.2,
                MonthlyTotalChannel2: 5100 + _phase,
                AlgorithmEstimate: 0);

            await Task.Delay(_interval, cancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
