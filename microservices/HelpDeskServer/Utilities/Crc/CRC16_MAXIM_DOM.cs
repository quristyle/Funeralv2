





namespace HelpDeskServer.Utilities;









public static class Crc16Maxim
{
    public static ushort Compute(byte[] data)
    {
        ushort crc = 0x0000;

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

        // XOR OUT
        crc ^= 0xFFFF;

        return crc;
    }

    // byte 배열로 반환 (Low, High 순서 - 일반적인 통신 방식)
    public static byte[] ComputeBytes(byte[] data)
    {
        ushort crc = Compute(data);
        return new byte[]
        {
            (byte)(crc & 0xFF),       // Low byte
            (byte)((crc >> 8) & 0xFF) // High byte
        };
    }
}

