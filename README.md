# SOC Modbus 数据采集与算法验证软件

这是一个 Windows 桌面端 C# WPF 程序，用于通过 Modbus RTU 串口采集设备数据、实时显示五路示波器曲线、运行 SOC 估计算法，并把采集结果保存为 CSV 文件。

当前界面已经按实际使用流程调整：

- 顶部菜单栏选择串口、浮点字节序、算法和工况。
- 左侧显示电压、电流、温度、振动四个方形示波器。
- 右侧显示更大的 SOC 估计值示波器，并与左侧四个示波器上下对齐。
- 左侧状态灯显示真实串口通讯状态；仿真数据不会误显示为串口通讯成功。
- 点击“保存数据”会先弹出保存位置选择窗口，确认后才开始写入 CSV。

## 运行环境

- Windows
- .NET 8 SDK
- 串口设备或 USB 转串口适配器

项目使用的主要依赖：

- WPF：桌面界面
- CommunityToolkit.Mvvm：MVVM 属性和命令生成
- NModbus：Modbus RTU 通讯
- OxyPlot.Wpf：实时示波器曲线
- System.IO.Ports：串口访问

## 启动方式

在仓库根目录运行：

```powershell
dotnet restore .\SOC.sln
dotnet run --project .\src\SocMonitor.Desktop\SocMonitor.Desktop.csproj
```

如果程序正在运行，再次构建可能会因为 `SocMonitor.Desktop.exe` 或 DLL 被占用而失败。关闭正在运行的程序后重新构建即可。

## 界面说明

顶部菜单栏：

- `串口：COMx`：选择当前 Modbus RTU 串口，默认 `COM1`。
- `浮点字节序：ABCD`：选择两个 16 位寄存器拼成 32 位浮点数的字节顺序。
- `算法：...`：选择 SOC 估计算法，目前包含透传算法和一阶卡尔曼滤波。
- `工况：...`：标记当前实验工况，目前用于界面状态显示，后续可扩展为算法参数或保存元数据。
- `使用仿真数据`：不连接真实串口，使用内置正弦/余弦模拟数据测试界面和保存流程。

操作区：

- `开始采集`：根据当前菜单参数创建采集源并开始刷新示波器。
- `停止`：停止当前采集循环，释放串口资源。
- `保存数据`：弹出 CSV 保存位置窗口，选择后开始实时写入数据；再次点击会停止保存。
- 通讯状态灯：真实串口读数成功时变绿；未连接、失败、停止或仿真模式下不显示串口成功。

示波器区：

- 左上：电压示波器
- 右上：电流示波器
- 左下：温度示波器
- 右下：振动示波器
- 右侧：SOC 估计值示波器

每个示波器默认保留最近 300 个采样点，避免长时间运行导致内存和绘图开销持续增长。

## 通讯参数

当前真实设备通讯参数固定为：

| 参数 | 当前值 |
| --- | --- |
| 协议 | Modbus RTU |
| 功能码 | 0x03 读取保持寄存器 |
| 从站地址 | 1 |
| 波特率 | 9600 |
| 校验 | None |
| 数据位 | 8 |
| 停止位 | 1 |
| 轮询周期 | 500 ms |
| 读写超时 | 1000 ms |

界面目前开放选择：

- 串口号：`COM1` 到 `COM10`
- 浮点字节序：`ABCD`、`CDAB`、`BADC`、`DCBA`

## 寄存器映射

`ModbusMeasurementSource` 当前按以下地址读取设备数据：

| 字段 | 地址 | 类型 | 当前用途 |
| --- | --- | --- | --- |
| EngineeringChannel1 | `0x2000` | float | 电流曲线、算法输入 |
| EngineeringChannel2 | `0x2002` | float | 电压曲线 |
| QualityChannel1 | `0x2080` | uint16 | CSV 保存 |
| QualityChannel2 | `0x2081` | uint16 | CSV 保存 |
| FlowChannel1 | `0x2100` | float | 温度曲线 |
| FlowChannel2 | `0x2102` | float | 振动曲线 |
| FlowStatusChannel1 | `0x2150` | uint16 | CSV 保存 |
| FlowStatusChannel2 | `0x2151` | uint16 | CSV 保存 |
| TotalIntegerChannel1 | `0x22C0` | int32 | CSV 保存 |
| TotalIntegerChannel2 | `0x22C2` | int32 | CSV 保存 |
| TimedTotalChannel1 | `0x2310` | float | CSV 保存 |
| TimedTotalChannel2 | `0x2312` | float | CSV 保存 |
| DailyTotalChannel1 | `0x2360` | float | CSV 保存 |
| DailyTotalChannel2 | `0x2362` | float | CSV 保存 |
| MonthlyTotalChannel1 | `0x23B0` | float | CSV 保存 |
| MonthlyTotalChannel2 | `0x23B2` | float | CSV 保存 |
| AlgorithmEstimate | 算法生成 | double | SOC 估计曲线、CSV 保存 |

## 数据保存

点击 `保存数据` 后，程序会打开保存文件窗口：

- 默认文件名：`soc_data_yyyyMMdd_HHmmss.csv`
- 默认位置：用户“我的文档”
- 文件格式：UTF-8 BOM CSV，便于 Excel 直接打开中文内容

CSV 每一行包含：

- 时间戳
- 算法名称
- 原始工程量通道
- 质量/状态通道
- 累计量通道
- 算法估计值

程序每写入一帧都会立即 `Flush`，降低异常退出时的数据丢失风险。

## 算法说明

算法统一实现 `IAlgorithmEstimator`：

```csharp
public interface IAlgorithmEstimator
{
    string Name { get; }

    MeasurementSample Estimate(MeasurementSample rawSample);
}
```

当前已有算法：

- `PassThroughEstimator`：透传算法，把 `EngineeringChannel1` 直接作为 `AlgorithmEstimate`。
- `FirstOrderKalmanEstimator`：一阶卡尔曼滤波，用 `EngineeringChannel1` 作为观测量，输出平滑后的 SOC 估计值。

如果要接入自己的 SOC 算法，可以新增类实现 `IAlgorithmEstimator`，再在 `MainViewModel.CreateEstimator` 中增加选择分支。

## 项目结构

```text
SOC.sln
src/SocMonitor.Desktop/
  Algorithms/
    IAlgorithmEstimator.cs        # 算法接口
    PassThroughEstimator.cs       # 透传算法
    FirstOrderKalmanEstimator.cs  # 一阶卡尔曼滤波示例
  Models/
    MeasurementSample.cs          # 一帧采样数据模型
    ModbusConnectionOptions.cs    # 串口和 Modbus 参数
  Services/
    IMeasurementSource.cs         # 采样源接口
    ModbusMeasurementSource.cs    # 真实 Modbus RTU 数据源
    SimulatedMeasurementSource.cs # 仿真数据源
    CsvDataRecorder.cs            # CSV 保存
  ViewModels/
    MainViewModel.cs              # 主界面业务逻辑
  MainWindow.xaml                 # 主界面布局
  MainWindow.xaml.cs              # 主窗口初始化
```

## 常见问题

### 开始采集后通讯灯为什么不变绿？

只有真实串口模式下成功读到 Modbus 数据，通讯灯才会变绿。开启 `使用仿真数据` 时，状态会显示“仿真数据”，但不会把通讯灯显示为串口成功。

### 保存数据点取消会怎样？

不会创建 CSV 文件，也不会进入保存状态，状态栏会显示“已取消保存数据”。

### 构建提示文件被占用怎么办？

如果桌面程序正在运行，`bin/Debug/net8.0-windows` 里的 exe 或 dll 会被锁住。关闭程序后重新运行：

```powershell
dotnet build .\SOC.sln
```

## 后续可扩展方向

- 自动扫描可用串口，替代固定 `COM1` 到 `COM10` 菜单。
- 把从站地址、波特率、校验位等参数也加入配置界面。
- 增加历史 CSV 回放功能，用同一批数据对比不同 SOC 算法。
- 增加报警阈值线、曲线显隐、坐标轴缩放和截图导出。
- 增加配置文件，保存上次使用的串口、字节序、算法和工况。
