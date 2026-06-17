using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Algorithms;

/// <summary>
/// SOC 估计算法的统一入口。
/// 主界面只依赖这个接口，不直接依赖某一种具体算法，
/// 这样后续替换 EKF、UKF、神经网络估计或外部 DLL 算法时，只需要新增实现类。
/// </summary>
public interface IAlgorithmEstimator
{
    /// <summary>
    /// 显示在界面和 CSV 文件中的算法名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 接收一帧原始采样数据，返回带有 AlgorithmEstimate 的估计结果。
    /// </summary>
    MeasurementSample Estimate(MeasurementSample rawSample);
}
