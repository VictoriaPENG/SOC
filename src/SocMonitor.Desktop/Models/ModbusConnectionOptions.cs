using System.IO.Ports;

namespace SocMonitor.Desktop.Models;

/// <summary>
/// Modbus RTU 串口连接参数。
/// 当前界面只暴露串口号和浮点字节序，其余参数按设备要求固定为 1、9600、None、8、1。
/// </summary>
public sealed class ModbusConnectionOptions
{
    /// <summary>Windows 串口名，例如 COM1、COM3。</summary>
    public string PortName { get; set; } = "COM1";

    /// <summary>Modbus 从站地址。</summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>串口波特率。</summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>校验位。</summary>
    public Parity Parity { get; set; } = Parity.None;

    /// <summary>数据位。</summary>
    public int DataBits { get; set; } = 8;

    /// <summary>停止位。</summary>
    public StopBits StopBits { get; set; } = StopBits.One;

    /// <summary>采样轮询周期。</summary>
    public int PollIntervalMilliseconds { get; set; } = 500;

    /// <summary>设备寄存器中 32 位浮点数的字节排列方式。</summary>
    public RegisterByteOrder FloatByteOrder { get; set; } = RegisterByteOrder.ABCD;

    /// <summary>串口读超时时间。</summary>
    public int ReadTimeoutMilliseconds { get; set; } = 1000;

    /// <summary>串口写超时时间。</summary>
    public int WriteTimeoutMilliseconds { get; set; } = 1000;
}

/// <summary>
/// 两个 16 位寄存器拼成一个 float 时的字节顺序。
/// 例如 ABCD 表示第一个寄存器高字节、低字节，再接第二个寄存器高字节、低字节。
/// </summary>
public enum RegisterByteOrder
{
    ABCD,
    CDAB,
    BADC,
    DCBA
}
