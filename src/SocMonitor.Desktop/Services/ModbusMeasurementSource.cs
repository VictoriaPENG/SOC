using System.IO.Ports;
using NModbus;
using NModbus.IO;
using SocMonitor.Desktop.Models;

namespace SocMonitor.Desktop.Services;

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

    private int ReadInt32(ushort address)
    {
        ushort[] registers = _master!.ReadHoldingRegisters(_options.SlaveId, address, 2);
        return BitConverter.ToInt32(ToOrderedBytes(registers, RegisterByteOrder.ABCD), 0);
    }

    private float ReadFloat(ushort address)
    {
        ushort[] registers = _master!.ReadHoldingRegisters(_options.SlaveId, address, 2);
        return BitConverter.ToSingle(ToOrderedBytes(registers, _options.FloatByteOrder), 0);
    }

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
        _serialPort?.Dispose();
        return ValueTask.CompletedTask;
    }

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
        }
    }
}
