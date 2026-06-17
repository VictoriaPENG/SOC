namespace SocMonitor.Desktop.Models;

public sealed record MeasurementSample(
    DateTimeOffset Timestamp,
    double EngineeringChannel1,
    double EngineeringChannel2,
    ushort QualityChannel1,
    ushort QualityChannel2,
    double FlowChannel1,
    double FlowChannel2,
    ushort FlowStatusChannel1,
    ushort FlowStatusChannel2,
    int TotalIntegerChannel1,
    int TotalIntegerChannel2,
    double TimedTotalChannel1,
    double TimedTotalChannel2,
    double DailyTotalChannel1,
    double DailyTotalChannel2,
    double MonthlyTotalChannel1,
    double MonthlyTotalChannel2,
    double AlgorithmEstimate);
