using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Algorithms;

public interface IAlgorithmEstimator
{
    string Name { get; }

    MeasurementSample Estimate(MeasurementSample rawSample);
}
