using System.IO.Ports;

namespace SocMonitor.Desktop.Models;

public sealed class ModbusConnectionOptions
{
    public string PortName { get; set; } = "COM1";

    public byte SlaveId { get; set; } = 1;

    public int BaudRate { get; set; } = 9600;

    public Parity Parity { get; set; } = Parity.None;

    public int DataBits { get; set; } = 8;

    public StopBits StopBits { get; set; } = StopBits.One;

    public int PollIntervalMilliseconds { get; set; } = 500;

    public RegisterByteOrder FloatByteOrder { get; set; } = RegisterByteOrder.ABCD;

    public int ReadTimeoutMilliseconds { get; set; } = 1000;

    public int WriteTimeoutMilliseconds { get; set; } = 1000;
}

public enum RegisterByteOrder
{
    ABCD,
    CDAB,
    BADC,
    DCBA
}
