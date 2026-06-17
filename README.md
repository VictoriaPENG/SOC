# SOC Modbus 桌面采集与算法验证软件

本仓库提供一个面向 Windows 桌面端的 C# WPF 起步工程，用于替代/复现 LabVIEW 中常见的设备采集、实时波形显示、数据记录和算法估计验证流程。

## 建议后的功能需求

### 1. 通信与采集

- 支持 Modbus TCP 读取设备保持寄存器，预留扩展 Modbus RTU 串口通信的空间。
- 参数可配置：IP、端口、从站地址、起始寄存器、寄存器数量、轮询周期、缩放系数。
- 采集线程与 UI 分离，避免界面卡顿。
- 首期提供仿真数据源，方便在未连接真实设备时验证界面和算法流程。

### 2. 实时显示

- 使用 OxyPlot 绘制电压、电流、温度和 SOC 估计值曲线。
- 默认保留最近 300 个点，避免长时间运行导致内存持续增长。
- 后续可增加曲线显隐、坐标轴缩放、报警阈值线和截图导出。

### 3. 算法接口

- 所有原始采样点先进入 `IAlgorithmEstimator`，再输出到显示层。
- 当前实现 `PassThroughEstimator` 作为透传占位，后续可替换为 SOC/SOH/故障诊断算法。
- 算法应尽量保持无 UI 依赖，便于写单元测试和与 LabVIEW 结果对比。

### 4. 数据管理

- 建议下一阶段增加 CSV/SQLite 数据记录，字段至少包括时间戳、原始寄存器、换算值、算法输出和异常状态。
- 建议增加“实验配置文件”，把设备参数、算法版本、采样周期、操作人员等信息与数据文件绑定。

### 5. 可靠性与验证

- Modbus 连接失败、超时、寄存器数量不匹配时，应提示用户并记录日志。
- 使用仿真源进行界面回归测试，使用离线 CSV 回放进行算法回归测试，使用真实设备进行联调测试。

## 项目结构

```text
SOC.sln
src/SocMonitor.Desktop/
  Algorithms/            # 算法接口和示例实现
  Models/                # 采样数据、Modbus 参数模型
  Services/              # 仿真数据源、Modbus 数据源
  ViewModels/            # WPF MVVM 主界面逻辑
  MainWindow.xaml        # 主界面
```

## 在 VSCode 中运行

1. 在 Windows 上安装 [.NET 8 SDK](https://dotnet.microsoft.com/download) 和 VSCode C# Dev Kit。
2. 克隆仓库后进入本地目录，例如 `E:\Github\SOC`。
3. 还原依赖并运行：

```powershell
dotnet restore .\SOC.sln
dotnet run --project .\src\SocMonitor.Desktop\SocMonitor.Desktop.csproj
```

## 从仿真源切换到真实 Modbus 设备

在 `MainViewModel.StartAsync` 中把：

```csharp
_source = new SimulatedMeasurementSource(TimeSpan.FromMilliseconds(500));
```

替换为：

```csharp
_source = new ModbusMeasurementSource(new ModbusConnectionOptions
{
    Host = "192.168.1.10",
    Port = 502,
    SlaveId = 1,
    StartAddress = 0,
    RegisterCount = 4,
    PollIntervalMilliseconds = 500,
    RegisterScale = 0.01
});
```

真实项目中建议把这些参数放到配置界面或 JSON 配置文件，而不是写死在代码里。

## 如何接入自己的算法

新建一个类实现 `IAlgorithmEstimator`：

```csharp
public sealed class MySocEstimator : IAlgorithmEstimator
{
    public string Name => "My SOC Estimator";

    public MeasurementSample Estimate(MeasurementSample rawSample)
    {
        double soc = rawSample.SocEstimate; // 在这里替换为你的估计算法
        return rawSample with { SocEstimate = soc };
    }
}
```

然后在 `MainViewModel` 中把 `PassThroughEstimator` 替换为你的实现。

## 建议开发路线

1. 确认 LabVIEW 原程序的通道列表、寄存器表、单位、缩放关系和采样周期。
2. 用仿真数据跑通界面、曲线刷新、开始/停止流程。
3. 接入真实 Modbus TCP 设备并校验每个通道的量纲和数值。
4. 增加 CSV/SQLite 记录和历史数据回放。
5. 接入算法实现，并用同一批历史数据对比 C# 与 LabVIEW 输出。
6. 增加异常处理、日志、配置持久化和打包发布。
