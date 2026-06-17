using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Algorithms;

public sealed class PassThroughEstimator : IAlgorithmEstimator
{
    public string Name => "透传算法 (PT)";

    public MeasurementSample Estimate(MeasurementSample rawSample) =>
        rawSample with { AlgorithmEstimate = rawSample.EngineeringChannel1 };
}
