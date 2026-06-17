using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Algorithms;

/// <summary>
/// 透传算法：不做滤波或估计，只把 EngineeringChannel1 作为算法输出。
/// 这个实现适合用来对照原始数据，或者在调试采集链路时确认算法层没有改变数值。
/// </summary>
public sealed class PassThroughEstimator : IAlgorithmEstimator
{
    public string Name => "透传算法 (PT)";

    public MeasurementSample Estimate(MeasurementSample rawSample) =>
        rawSample with { AlgorithmEstimate = rawSample.EngineeringChannel1 };
}
