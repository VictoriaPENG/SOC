using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Algorithms;

public sealed class FirstOrderKalmanEstimator : IAlgorithmEstimator
{
    private const double ProcessNoise = 0.02;
    private const double MeasurementNoise = 2.5;

    private bool _hasState;
    private double _estimate;
    private double _errorCovariance = 1;

    public string Name => "一阶卡尔曼滤波 (KF)";

    public MeasurementSample Estimate(MeasurementSample rawSample)
    {
        if (!_hasState)
        {
            _estimate = rawSample.EngineeringChannel1;
            _hasState = true;
        }

        _errorCovariance += ProcessNoise;
        double kalmanGain = _errorCovariance / (_errorCovariance + MeasurementNoise);
        _estimate += kalmanGain * (rawSample.EngineeringChannel1 - _estimate);
        _errorCovariance *= 1 - kalmanGain;

        return rawSample with { AlgorithmEstimate = _estimate };
    }
}
