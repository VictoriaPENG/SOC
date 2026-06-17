using System.IO.Ports;
using NModbus;
using NModbus.IO;
using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

/// <summary>
/// Modbus RTU 串口数据源。
/// 负责打开串口、创建 NModbus RTU 主站、按固定寄存器表读取一帧 MeasurementSample。
/// </summary>
public sealed class ModbusMeasurementSource : IMeasurementSource
{
    private readonly ModbusConnectionOptions _options;
    private SerialPort? _serialPort;
    private SerialPortStreamResource? _streamResource;
    private IModbusSerialMaster? _master;

    public ModbusMeasurementSource(ModbusConnectionOptions options)
    {
        _options = options;
    }

    public async IAsyncEnumerable<MeasurementSample> ReadSamplesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // 第一次采集前才真正打开串口，便于界面先完成参数选择。
        EnsureConnected();

        while (!cancellationToken.IsCancellationRequested)
        {
            yield return ReadCurrentSample();
            await Task.Delay(_options.PollIntervalMilliseconds, cancellationToken);
        }
    }

    private void EnsureConnected()
    {
        if (_master is not null)
        {
            return;
        }

        // SerialPort 负责底层串口，NModbus 通过 IStreamResource 读写 RTU 帧。
        _serialPort = new SerialPort(_options.PortName, _options.BaudRate, _options.Parity, _options.DataBits, _options.StopBits)
        {
            ReadTimeout = _options.ReadTimeoutMilliseconds,
            WriteTimeout = _options.WriteTimeoutMilliseconds
        };
        _serialPort.Open();

        _streamResource = new SerialPortStreamResource(_serialPort);
        _master = new ModbusFactory().CreateRtuMaster(_streamResource);
        _master.Transport.ReadTimeout = _options.ReadTimeoutMilliseconds;
        _master.Transport.WriteTimeout = _options.WriteTimeoutMilliseconds;
        _master.Transport.Retries = 0;
    }

    /// <summary>
    /// 按当前设备寄存器表读取所有需要的通道。
    /// 地址以 0x2000、0x2100 等保持寄存器地址为准，使用 0x03 功能码读取。
    /// </summary>
    private MeasurementSample ReadCurrentSample()
    {
        return new MeasurementSample(
            DateTimeOffset.Now,
            EngineeringChannel1: ReadFloat(0x2000),
            EngineeringChannel2: ReadFloat(0x2002),
            QualityChannel1: ReadUInt16(0x2080),
            QualityChannel2: ReadUInt16(0x2081),
            FlowChannel1: ReadFloat(0x2100),
            FlowChannel2: ReadFloat(0x2102),
            FlowStatusChannel1: ReadUInt16(0x2150),
            FlowStatusChannel2: ReadUInt16(0x2151),
            TotalIntegerChannel1: ReadInt32(0x22C0),
            TotalIntegerChannel2: ReadInt32(0x22C2),
            TimedTotalChannel1: ReadFloat(0x2310),
            TimedTotalChannel2: ReadFloat(0x2312),
            DailyTotalChannel1: ReadFloat(0x2360),
            DailyTotalChannel2: ReadFloat(0x2362),
            MonthlyTotalChannel1: ReadFloat(0x23B0),
            MonthlyTotalChannel2: ReadFloat(0x23B2),
            AlgorithmEstimate: 0);
    }

    private ushort ReadUInt16(ushort address)
    {
        return _master!.ReadHoldingRegisters(_options.SlaveId, address, 1)[0];
    }

    // 32 位整数固定按 ABCD 拼接；如果设备整数也存在字节序差异，可在这里扩展配置项。
    private int ReadInt32(ushort address)
    {
        ushort[] registers = _master!.ReadHoldingRegisters(_options.SlaveId, address, 2);
        return BitConverter.ToInt32(ToOrderedBytes(registers, RegisterByteOrder.ABCD), 0);
    }

    // 浮点数使用界面选择的字节序，解决不同仪表/PLC 寄存器排列不一致的问题。
    private float ReadFloat(ushort address)
    {
        ushort[] registers = _master!.ReadHoldingRegisters(_options.SlaveId, address, 2);
        return BitConverter.ToSingle(ToOrderedBytes(registers, _options.FloatByteOrder), 0);
    }

    /// <summary>
    /// 把两个 16 位寄存器转换成 BitConverter 可直接读取的 4 字节数组。
    /// BitConverter 使用本机小端序，所以最后需要根据系统端序反转。
    /// </summary>
    private static byte[] ToOrderedBytes(IReadOnlyList<ushort> registers, RegisterByteOrder order)
    {
        byte a = (byte)(registers[0] >> 8);
        byte b = (byte)(registers[0] & 0xFF);
        byte c = (byte)(registers[1] >> 8);
        byte d = (byte)(registers[1] & 0xFF);

        byte[] bytes = order switch
        {
            RegisterByteOrder.CDAB => [c, d, a, b],
            RegisterByteOrder.BADC => [b, a, d, c],
            RegisterByteOrder.DCBA => [d, c, b, a],
            _ => [a, b, c, d]
        };

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    public ValueTask DisposeAsync()
    {
        // 释放串口句柄，避免下次采集或其他串口工具无法打开同一个 COM 口。
        _serialPort?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// NModbus 需要 IStreamResource；这里把 System.IO.Ports.SerialPort 包装成它能使用的流资源。
    /// </summary>
    private sealed class SerialPortStreamResource : IStreamResource
    {
        private readonly SerialPort _serialPort;

        public SerialPortStreamResource(SerialPort serialPort)
        {
            _serialPort = serialPort;
        }

        public int InfiniteTimeout => SerialPort.InfiniteTimeout;

        public int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }

        public void DiscardInBuffer() => _serialPort.DiscardInBuffer();

        public int Read(byte[] buffer, int offset, int count) => _serialPort.Read(buffer, offset, count);

        public void Write(byte[] buffer, int offset, int count) => _serialPort.Write(buffer, offset, count);

        public void Dispose()
        {
            // SerialPort 生命周期由外层 ModbusMeasurementSource 管理，这里不重复 Dispose。
        }
    }
}
