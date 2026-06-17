using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Algorithms;

/// <summary>
/// 一阶卡尔曼滤波示例算法。
/// 当前版本使用 EngineeringChannel1 作为观测量，输出平滑后的 AlgorithmEstimate。
/// 如果后续 SOC 算法需要融合电压、电流、温度等多通道数据，可以在 Estimate 中扩展状态方程。
/// </summary>
public sealed class FirstOrderKalmanEstimator : IAlgorithmEstimator
{
    // 过程噪声越大，估计值越容易跟随新数据变化。
    private const double ProcessNoise = 0.02;

    // 测量噪声越大，滤波器越不信任当前采样点，输出会更平滑。
    private const double MeasurementNoise = 2.5;

    private bool _hasState;
    private double _estimate;
    private double _errorCovariance = 1;

    public string Name => "一阶卡尔曼滤波 (KF)";

    /// <summary>
    /// 运行一阶卡尔曼预测/校正流程，并把估计值写回 AlgorithmEstimate。
    /// </summary>
    public MeasurementSample Estimate(MeasurementSample rawSample)
    {
        // 首帧数据没有历史状态，直接用当前观测量初始化滤波器。
        if (!_hasState)
        {
            _estimate = rawSample.EngineeringChannel1;
            _hasState = true;
        }

        // 预测：状态本身按一阶模型保持不变，只扩大误差协方差。
        _errorCovariance += ProcessNoise;

        // 校正：根据测量噪声计算卡尔曼增益，再用新观测修正估计值。
        double kalmanGain = _errorCovariance / (_errorCovariance + MeasurementNoise);
        _estimate += kalmanGain * (rawSample.EngineeringChannel1 - _estimate);
        _errorCovariance *= 1 - kalmanGain;

        return rawSample with { AlgorithmEstimate = _estimate };
    }
}
