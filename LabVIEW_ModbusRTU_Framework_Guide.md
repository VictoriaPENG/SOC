# LabVIEW RS485 Modbus RTU Acquisition Framework

This guide is for building a reusable LabVIEW application that reads the first two channels from a Modbus RTU device over RS485, displays numeric values and waveforms, and leaves a clean algorithm interface for future projects.

## Device Settings From Photos

- Slave address: `1`
- Baud rate: `9600`
- Parity: `None`
- Data bits: usually `8`
- Stop bits: usually `1`, use `2` if the device manual states `9600,N,8,2`
- Function code: `0x03` Read Holding Registers
- Example request: `01 03 20 00 00 02 CF CB`
- Example response value: `3F 80 00 00` means `1.00`
- Float byte order must be configurable. Start with standard big-endian `ABCD` for `3F 80 00 00 = 1.00`; if values are wrong, switch among `BADC`, `CDAB`, `DCBA`.

## Front Panel Layout

Use a tab control with these pages:

1. `Monitor`
   - Left: communication status LED, run/stop, connect/disconnect, sample interval.
   - Center: two waveform charts, one for channel 1 engineering/flow and one for channel 2 engineering/flow, or one mixed plot chart with clear plot names.
   - Right: numeric table for channel 1 and channel 2 values.
2. `Communication`
   - VISA resource name, slave address, baud, parity, data bits, stop bits, timeout, retry count, byte order.
   - Buttons: connect, disconnect, test read.
3. `Registers`
   - Table loaded from `modbus_register_map_ch1_ch2.csv`.
   - Enable checkbox per row, address, type, scale, offset, display name.
4. `Algorithm`
   - Algorithm enable, algorithm mode enum, input preview, output values, alarm/result indicators.
   - Keep this page simple now; the important part is the connector/interface.
5. `Log/Debug`
   - Raw TX/RX string, error log, last update time, save CSV enable.

## Recommended VI Structure

- `Main.vi`: UI, event loop, and shutdown coordination.
- `Modbus_Init.vi`: opens/configures VISA and Modbus master session.
- `Modbus_ReadMap.vi`: reads all enabled rows from the register map.
- `Decode_Register.vi`: converts U16 register arrays to float, U16, I32, etc.
- `Algorithm_Process.vi`: future algorithm interface. Input is acquisition data cluster; output is algorithm result cluster.
- `UI_Update.vi`: writes values to indicators and charts.
- `Logger_Write.vi`: optional CSV/TDMS logging.
- Type definitions:
  - `CommConfig.ctl`
  - `RegisterItem.ctl`
  - `ChannelData.ctl`
  - `AcquisitionFrame.ctl`
  - `AlgorithmResult.ctl`

## Main Architecture

Use a Queued Message Handler plus Producer/Consumer:

- UI Event Loop handles button clicks and setting changes.
- Acquisition Loop reads Modbus on a fixed interval.
- Algorithm Loop receives parsed frames and computes future results.
- UI Update Loop receives latest raw and algorithm data.
- Logging Loop writes data asynchronously.

Do not put Modbus reads directly inside the UI event case; otherwise the front panel can freeze when the serial line times out.

## Reading Strategy

For the first version, read by functional groups:

- Engineering values: start `0x2000`, quantity `4` registers for channel 1 and 2 floats.
- Quality codes: start `0x2080`, quantity `2` registers.
- Flow values: start `0x2100`, quantity `4` registers.
- Flow status: start `0x2150`, quantity `2` registers.
- Total accumulated integer: start `0x22C0`, quantity `4` registers.
- Timed accumulated values: start `0x2310`, quantity `4` registers.
- Daily accumulated values: start `0x2360`, quantity `4` registers.
- Monthly accumulated values: start `0x23B0`, quantity `4` registers.

This is more efficient than one request per value and still keeps each group easy to debug.

## LabVIEW Build Steps

1. Create a LabVIEW Project.
2. Add folders: `typedef`, `driver`, `decode`, `algorithm`, `ui`, `logging`, `config`.
3. Create the typedef clusters listed above.
4. Build `Modbus_Init.vi` with your Modbus library serial master open/configure VIs.
5. Build `Decode_Register.vi`.
6. Build `Modbus_ReadMap.vi` using function `0x03` reads and the register groups above.
7. Build `Algorithm_Process.vi` as a pass-through first.
8. Build `Main.vi` with one UI event loop and one acquisition loop.
9. Add waveform charts using shift registers or chart history.
10. Add build specification: Application EXE, include config CSV beside the EXE.

## Decode Notes

Modbus register data is usually an array of U16. A float uses two U16 registers:

- Register array example: `[0x3F80, 0x0000]`
- Bytes: `3F 80 00 00`
- IEEE754 single precision: `1.00`

In LabVIEW, convert by:

1. Split each U16 register into high byte and low byte.
2. Reorder the four bytes according to selected byte order.
3. Build U32 from the four bytes.
4. Type Cast U32 bytes to Single.

Keep byte order as a front-panel enum so the application can adapt to other devices.

## Algorithm Interface

`Algorithm_Process.vi` connector pane:

- Inputs:
  - `AcquisitionFrame`
  - `AlgorithmConfig`
  - `error in`
- Outputs:
  - `AlgorithmResult`
  - `error out`

For now, pass raw channel values through. Later, replace internal logic without changing acquisition, UI, or logging code.
