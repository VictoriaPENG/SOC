namespace SocMonitor.Desktop.Models;

/// <summary>
/// 一帧采样数据的完整快照。
/// Modbus 数据源和仿真数据源都输出这个模型，算法、示波器和 CSV 保存也都围绕它工作。
/// record 类型支持 with 表达式，便于算法在保留原始通道的同时只替换 AlgorithmEstimate。
/// </summary>
public sealed record MeasurementSample(
    // 本地采样时间，用于示波器横轴和 CSV 时间戳。
    DateTimeOffset Timestamp,
    // 工程量通道 1/2：当前界面分别作为电流、电压显示。
    double EngineeringChannel1,
    double EngineeringChannel2,
    // 质量/状态类寄存器，保留到 CSV 中，方便后续做数据质量诊断。
    ushort QualityChannel1,
    ushort QualityChannel2,
    // 流量类通道 1/2：当前界面分别作为温度、振动显示。
    double FlowChannel1,
    double FlowChannel2,
    ushort FlowStatusChannel1,
    ushort FlowStatusChannel2,
    // 累计量寄存器，当前不直接绘图，但会保存到 CSV 便于离线分析。
    int TotalIntegerChannel1,
    int TotalIntegerChannel2,
    double TimedTotalChannel1,
    double TimedTotalChannel2,
    double DailyTotalChannel1,
    double DailyTotalChannel2,
    double MonthlyTotalChannel1,
    double MonthlyTotalChannel2,
    // 算法输出。原始采集时为 0，经过 IAlgorithmEstimator 后写入 SOC 估计值。
    double AlgorithmEstimate);
