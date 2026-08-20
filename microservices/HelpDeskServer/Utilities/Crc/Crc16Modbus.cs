
namespace HelpDeskServer.Utilities;

public static class Crc16Modbus
{
    private const ushort Polynomial = 0xA001;
    private const ushort InitialValue = 0xFFFF;

    /// <summary>
    /// CRC 계산 (Modbus)
    /// </summary>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = InitialValue;

        foreach (var b in data)
        {
            crc ^= b;

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ Polynomial);
                else
                    crc >>= 1;
            }
        }

        return crc;
    }

    /// <summary>
    /// CRC 검증 (마지막 2바이트가 CRC일 때)
    /// </summary>
    public static bool Validate(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 3)
            return false;

        var data = packet[..^2]; // CRC 제외
        ushort computed = Compute(data);

        ushort received = (ushort)(packet[^2] | (packet[^1] << 8)); // Little Endian

        return computed == received;
    }

    /// <summary>
    /// CRC 붙이기 (패킷 생성용)
    /// </summary>
    public static byte[] AppendCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = Compute(data);

        byte[] result = new byte[data.Length + 2];
        data.CopyTo(result);

        // Little Endian
        result[^2] = (byte)(crc & 0xFF);       // Low
        result[^1] = (byte)(crc >> 8);         // High

        return result;
    }
}