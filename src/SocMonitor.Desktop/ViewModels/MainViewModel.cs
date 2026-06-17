using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SocMonitor.Desktop.Algorithms;
using SocMonitor.Desktop.Models;
using SocMonitor.Desktop.Services;

namespace SocMonitor.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private IAlgorithmEstimator _estimator = new FirstOrderKalmanEstimator();
    private CancellationTokenSource? _pollingCancellation;
    private IMeasurementSource? _source;
    private CsvDataRecorder? _recorder;
    private MeasurementSample? _lastEstimatedSample;

    public IReadOnlyList<string> AlgorithmNames { get; } =
    [
        "透传算法 (PT)",
        "一阶卡尔曼滤波 (KF)"
    ];

    public IReadOnlyList<string> WorkConditionNames { get; } =
    [
        "静置 (REST)",
        "恒流充电 (CC-C)",
        "恒流放电 (CC-D)",
        "脉冲工况 (PULSE)"
    ];

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
        CommunicationText = "连接中";
        Status = $"采集中：算法={SelectedAlgorithmName}，工况={SelectedWorkConditionName}";
        _pollingCancellation = new CancellationTokenSource();
        _source = CreateMeasurementSource();

        try
        {
            await foreach (MeasurementSample rawSample in _source.ReadSamplesAsync(_pollingCancellation.Token))
            {
                MeasurementSample estimatedSample = _estimator.Estimate(rawSample);
                IsCommunicationOk = true;
                CommunicationText = "通讯成功";
                AppendSample(rawSample, estimatedSample);
                _lastEstimatedSample = estimatedSample;
                _recorder?.Append(estimatedSample, _estimator.Name);
            }
        }
        catch (OperationCanceledException)
        {
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

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SocMonitorLogs");
        string fileName = $"soc_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        _recorder = new CsvDataRecorder(Path.Combine(directory, fileName));
        IsRecording = true;
        RecordButtonText = "停止保存";
        LastSavedFile = _recorder.FilePath;

        if (_lastEstimatedSample is not null)
        {
            _recorder.Append(_lastEstimatedSample, _estimator.Name);
        }

        Status = $"开始保存数据：{LastSavedFile}";
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

        AddPoint(CurrentPlotModel, "Signal", x, rawSample.EngineeringChannel1);
        CurrentPlotModel.InvalidatePlot(true);

        AddPoint(VoltagePlotModel, "Signal", x, rawSample.EngineeringChannel2);
        VoltagePlotModel.InvalidatePlot(true);

        AddPoint(TemperaturePlotModel, "Signal", x, rawSample.FlowChannel1);
        TemperaturePlotModel.InvalidatePlot(true);

        AddPoint(VibrationPlotModel, "Signal", x, rawSample.FlowChannel2);
        VibrationPlotModel.InvalidatePlot(true);

        LatestDataText =
            $"电流={rawSample.EngineeringChannel1:F4}，电压={rawSample.EngineeringChannel2:F4}，" +
            $"温度={rawSample.FlowChannel1:F4}，振动={rawSample.FlowChannel2:F4}，" +
            $"算法估计={estimatedSample.AlgorithmEstimate:F4}";
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
        while (series.Points.Count > 300)
        {
            series.Points.RemoveAt(0);
        }
    }

    private static PlotModel CreateOscilloscopePlotModel(string title, string yAxisTitle, OxyColor color)
    {
        var model = new PlotModel { Title = title };
        model.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "HH:mm:ss", Title = "时间" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = yAxisTitle });
        model.Series.Add(new LineSeries { Title = yAxisTitle, Tag = "Signal", Color = color, StrokeThickness = 2 });
        return model;
    }
}
