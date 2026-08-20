
namespace HelpDeskServer.Utilities;

/*
전기측 장비의 crc 계산 방식은 
CRC = CRC16_Modbus(data) XOR 0x6340
이다. 해당 로직방식으로 구현해두었다. 아래. 코드값이나 방식 변경 하지 말것.
*/
public static class KepcoCrc
{
    private const ushort XorKey = 0x6340;

    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;

        foreach (byte b in data)
        {
            crc ^= b;

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }

        // 🔥 핵심
        crc ^= XorKey;

        return crc;
    }

    public static ushort ComputeHdlcCrc_x(ReadOnlySpan<byte> data){
    ushort crc = 0xFFFF;

    foreach (byte b in data)
    {
        crc ^= b;

        for (int i = 0; i < 8; i++)
        {
            if ((crc & 0x0001) != 0)
                crc = (ushort)((crc >> 1) ^ 0x8408);
            else
                crc >>= 1;
        }
    }

    return (ushort)~crc; // 중요 (Final XOR)
}

public static ushort ComputeHdlcCrc(ReadOnlySpan<byte> data)
{
    ushort crc = 0xFFFF;

    foreach (byte b in data)
    {
        crc ^= (ushort)(b << 8);

        for (int i = 0; i < 8; i++)
        {
            if ((crc & 0x8000) != 0)
                crc = (ushort)((crc << 1) ^ 0x1021);
            else
                crc <<= 1;
        }
    }

    return (ushort)(~crc); // HDLC는 마지막 NOT
}





    public static bool Validate(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 3) return false;

        var data = packet[..^2];

        ushort computed = Compute(data);
        ushort received = (ushort)(packet[^2] | (packet[^1] << 8));

        return computed == received;
    }
}