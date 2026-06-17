using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SocMonitor.Desktop.Algorithms;
using SocMonitor.Desktop.Models;
using SocMonitor.Desktop.Services;

namespace SocMonitor.Desktop.ViewModels;

/// <summary>
/// 主界面的 ViewModel。
/// 它负责连接数据源、调度算法、刷新 OxyPlot 示波器、控制通讯状态灯并保存 CSV。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    // 当前选中的算法实例。开始采集时根据菜单选择重新创建，避免旧算法状态影响新一轮采集。
    private IAlgorithmEstimator _estimator = new FirstOrderKalmanEstimator();
    private CancellationTokenSource? _pollingCancellation;
    private IMeasurementSource? _source;
    private CsvDataRecorder? _recorder;
    private MeasurementSample? _lastEstimatedSample;

    // 菜单中可选的算法名称。名称同时用于 CreateEstimator 的匹配。
    public IReadOnlyList<string> AlgorithmNames { get; } =
    [
        "透传算法 (PT)",
        "一阶卡尔曼滤波 (KF)"
    ];

    // 工况目前用于标记当前实验状态，后续可扩展为算法参数或 CSV 元数据。
    public IReadOnlyList<string> WorkConditionNames { get; } =
    [
        "静置 (REST)",
        "恒流充电 (CC-C)",
        "恒流放电 (CC-D)",
        "脉冲工况 (PULSE)"
    ];

    // 浮点字节序决定两个 Modbus 寄存器如何拼成 32 位 float。
    public IReadOnlyList<string> FloatByteOrderNames { get; } =
    [
        "ABCD",
        "CDAB",
        "BADC",
        "DCBA"
    ];

    [ObservableProperty]
    private string _selectedAlgorithmName = "一阶卡尔曼滤波 (KF)";

    [ObservableProperty]
    private string _selectedWorkConditionName = "静置 (REST)";

    [ObservableProperty]
    private string _selectedFloatByteOrderName = "ABCD";

    [ObservableProperty]
    private string _portName = "COM1";

    [ObservableProperty]
    private bool _useSimulation;

    [ObservableProperty]
    private string _status = "就绪：默认通讯参数为地址 1、9600、None、8、1，默认浮点字节序 ABCD。";

    [ObservableProperty]
    private string _communicationText = "未连接";

    [ObservableProperty]
    private bool _isCommunicationOk;

    [ObservableProperty]
    private bool _isPolling;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordButtonText = "保存数据";

    [ObservableProperty]
    private string _lastSavedFile = "";

    [ObservableProperty]
    private string _latestDataText = "暂无实时数据";

    [ObservableProperty]
    private PlotModel _currentPlotModel = CreateOscilloscopePlotModel("电流示波器 (Current)", "电流", OxyColors.SteelBlue);

    [ObservableProperty]
    private PlotModel _voltagePlotModel = CreateOscilloscopePlotModel("电压示波器 (Voltage)", "电压", OxyColors.OrangeRed);

    [ObservableProperty]
    private PlotModel _temperaturePlotModel = CreateOscilloscopePlotModel("温度示波器 (Temperature)", "温度", OxyColors.ForestGreen);

    [ObservableProperty]
    private PlotModel _vibrationPlotModel = CreateOscilloscopePlotModel("振动示波器 (Vibration)", "振动", OxyColors.DarkCyan);

    [ObservableProperty]
    private PlotModel _socPlotModel = CreateOscilloscopePlotModel("SOC估计值示波器 (SOC Estimate)", "SOC估计值", OxyColors.MediumPurple);

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (IsPolling)
        {
            return;
        }

        _estimator = CreateEstimator(SelectedAlgorithmName);
        IsPolling = true;
        IsCommunicationOk = false;

        // 记录本轮采集是否为仿真模式，避免用户采集中切换复选框造成状态判断混乱。
        bool isSimulationRun = UseSimulation;
        CommunicationText = isSimulationRun ? "仿真数据" : "连接中";
        Status = $"采集中：算法={SelectedAlgorithmName}，工况={SelectedWorkConditionName}";
        _pollingCancellation = new CancellationTokenSource();
        _source = CreateMeasurementSource();

        try
        {
            await foreach (MeasurementSample rawSample in _source.ReadSamplesAsync(_pollingCancellation.Token))
            {
                MeasurementSample estimatedSample = _estimator.Estimate(rawSample);

                // 只有真实串口采集成功读到数据时才点亮通讯灯；仿真数据不冒充串口成功。
                IsCommunicationOk = !isSimulationRun;
                CommunicationText = isSimulationRun ? "仿真数据" : "通讯成功";
                AppendSample(rawSample, estimatedSample);
                _lastEstimatedSample = estimatedSample;
                _recorder?.Append(estimatedSample, _estimator.Name);
            }
        }
        catch (OperationCanceledException)
        {
            IsCommunicationOk = false;
            Status = "已停止采集。";
            CommunicationText = "已停止";
        }
        catch (Exception ex)
        {
            IsCommunicationOk = false;
            CommunicationText = "通讯失败";
            Status = $"通讯失败：{ex.Message}";
        }
        finally
        {
            // 无论正常停止还是异常退出，都释放串口/记录器，避免资源被锁住。
            if (_source is not null)
            {
                await _source.DisposeAsync();
                _source = null;
            }

            StopRecording();
            _pollingCancellation?.Dispose();
            _pollingCancellation = null;
            IsPolling = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        if (!IsPolling)
        {
            return;
        }

        Status = "正在停止采集...";
        _pollingCancellation?.Cancel();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void SaveData()
    {
        if (IsRecording)
        {
            StopRecording();
            Status = string.IsNullOrWhiteSpace(LastSavedFile) ? "已停止保存数据。" : $"已停止保存数据：{LastSavedFile}";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "选择数据保存位置",
            Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = $"soc_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        // 用户取消文件选择时不创建 CSV，也不切换到“正在保存”状态。
        if (dialog.ShowDialog() != true)
        {
            Status = "已取消保存数据。";
            return;
        }

        _recorder = new CsvDataRecorder(dialog.FileName);
        IsRecording = true;
        RecordButtonText = "停止保存";
        LastSavedFile = _recorder.FilePath;

        // 如果采集已经有最新估计值，开始保存时先写入这一帧，避免文件为空。
        if (_lastEstimatedSample is not null)
        {
            _recorder.Append(_lastEstimatedSample, _estimator.Name);
        }

        Status = $"开始保存数据：{LastSavedFile}";
    }

    [RelayCommand]
    private void SelectPort(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return;
        }

        PortName = portName;
        if (!IsPolling)
        {
            Status = $"已选择串口：{portName}";
        }
    }

    [RelayCommand]
    private void SelectFloatByteOrder(string? floatByteOrderName)
    {
        if (string.IsNullOrWhiteSpace(floatByteOrderName))
        {
            return;
        }

        SelectedFloatByteOrderName = floatByteOrderName;
        if (!IsPolling)
        {
            Status = $"已选择浮点字节序：{floatByteOrderName}";
        }
    }

    [RelayCommand]
    private void SelectAlgorithm(string? algorithmName)
    {
        if (string.IsNullOrWhiteSpace(algorithmName))
        {
            return;
        }

        SelectedAlgorithmName = algorithmName;
    }

    [RelayCommand]
    private void SelectWorkCondition(string? workConditionName)
    {
        if (string.IsNullOrWhiteSpace(workConditionName))
        {
            return;
        }

        SelectedWorkConditionName = workConditionName;
    }

    private bool CanStart() => !IsPolling;

    private bool CanStop() => IsPolling;

    partial void OnIsPollingChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedAlgorithmNameChanged(string value)
    {
        if (!IsPolling)
        {
            Status = $"已选择算法：{value}";
        }
    }

    partial void OnSelectedWorkConditionNameChanged(string value)
    {
        if (!IsPolling)
        {
            Status = $"已选择工况：{value}";
        }
    }

    private IMeasurementSource CreateMeasurementSource()
    {
        if (UseSimulation)
        {
            return new SimulatedMeasurementSource(TimeSpan.FromMilliseconds(500));
        }

        // 真实串口采集采用固定通讯参数，只把串口号和浮点字节序暴露给菜单选择。
        return new ModbusMeasurementSource(new ModbusConnectionOptions
        {
            PortName = PortName,
            SlaveId = 1,
            BaudRate = 9600,
            Parity = System.IO.Ports.Parity.None,
            DataBits = 8,
            StopBits = System.IO.Ports.StopBits.One,
            PollIntervalMilliseconds = 500,
            FloatByteOrder = Enum.Parse<RegisterByteOrder>(SelectedFloatByteOrderName)
        });
    }

    private static IAlgorithmEstimator CreateEstimator(string algorithmName) =>
        algorithmName switch
        {
            string name when name.Contains("KF", StringComparison.OrdinalIgnoreCase) => new FirstOrderKalmanEstimator(),
            _ => new PassThroughEstimator()
        };

    private void AppendSample(MeasurementSample rawSample, MeasurementSample estimatedSample)
    {
        double x = DateTimeAxis.ToDouble(rawSample.Timestamp.LocalDateTime);

        // 五个示波器分别绑定独立 PlotModel；每次采样只追加一个点并立即刷新。
        AddPoint(CurrentPlotModel, "Signal", x, rawSample.EngineeringChannel1);
        CurrentPlotModel.InvalidatePlot(true);

        AddPoint(VoltagePlotModel, "Signal", x, rawSample.EngineeringChannel2);
        VoltagePlotModel.InvalidatePlot(true);

        AddPoint(TemperaturePlotModel, "Signal", x, rawSample.FlowChannel1);
        TemperaturePlotModel.InvalidatePlot(true);

        AddPoint(VibrationPlotModel, "Signal", x, rawSample.FlowChannel2);
        VibrationPlotModel.InvalidatePlot(true);

        AddPoint(SocPlotModel, "Signal", x, estimatedSample.AlgorithmEstimate);
        SocPlotModel.InvalidatePlot(true);

        LatestDataText =
            $"电流={rawSample.EngineeringChannel1:F4}，电压={rawSample.EngineeringChannel2:F4}，" +
            $"温度={rawSample.FlowChannel1:F4}，振动={rawSample.FlowChannel2:F4}，" +
            $"SOC估计值={estimatedSample.AlgorithmEstimate:F4}";
    }

    private void StopRecording()
    {
        _recorder?.Dispose();
        _recorder = null;
        IsRecording = false;
        RecordButtonText = "保存数据";
    }

    private static void AddPoint(PlotModel plotModel, string tag, double x, double y)
    {
        if (plotModel.Series.FirstOrDefault(series => series.Tag?.Equals(tag) == true) is not LineSeries series)
        {
            return;
        }

        series.Points.Add(new DataPoint(x, y));

        // 只保留最近 300 个点，防止长时间采集后内存和绘图开销持续增长。
        while (series.Points.Count > 300)
        {
            series.Points.RemoveAt(0);
        }
    }

    private static PlotModel CreateOscilloscopePlotModel(string title, string yAxisTitle, OxyColor color)
    {
        // 收紧标题、坐标轴和绘图区边距，让有限面板里尽量显示更多曲线区域。
        var model = new PlotModel
        {
            Title = title,
            TitleFontSize = 13,
            TitlePadding = 2,
            PlotMargins = new OxyThickness(36, 8, 6, 24)
        };

        model.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            Title = "时间",
            FontSize = 9,
            TitleFontSize = 10,
            MajorTickSize = 3,
            MinorTickSize = 0
        });
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = yAxisTitle,
            FontSize = 9,
            TitleFontSize = 10,
            MajorTickSize = 3,
            MinorTickSize = 0
        });
        model.Series.Add(new LineSeries { Title = yAxisTitle, Tag = "Signal", Color = color, StrokeThickness = 2 });
        return model;
    }
}
