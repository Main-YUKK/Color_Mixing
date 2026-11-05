using System;

// TCP hanya mengalirkan byte tanpa batas pesan, jadi tiap pesan dibungkus "frame":
//   [4 byte panjang][1 byte jenis][isi]      (panjang = 1 + panjang isi, little-endian)
// FrameReader menyusun potongan byte yang datang sembarangan (terbelah / menyatu) menjadi frame utuh.
public static class Frame
{
    public const byte Welcome = 0;   // host -> client: isi 1 byte = index pemain untuk client itu
    public const byte Data = 1;      // isi = satu pesan game (NetMessage.Encode)

    public const int MaxLength = 1024 * 1024;   // frame lebih besar dianggap rusak

    public static byte[] Build(byte kind, byte[] payload)
    {
        payload ??= new byte[0];
        int length = 1 + payload.Length;
        var frame = new byte[4 + length];
        BitConverter.GetBytes(length).CopyTo(frame, 0);
        frame[4] = kind;
        Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
        return frame;
    }
}

public class FrameReader
{
    private byte[] buffer = new byte[256];
    private int count;

    public void Append(byte[] data, int offset, int length)
    {
        if (count + length > buffer.Length)
            Array.Resize(ref buffer, Math.Max(buffer.Length * 2, count + length));

        Buffer.BlockCopy(data, offset, buffer, count, length);
        count += length;
    }

    // true = satu frame utuh diambil. false = belum lengkap, tunggu data berikutnya.
    // Melempar FormatException kalau panjang frame mustahil (koneksi harus diputus).
    public bool TryRead(out byte kind, out byte[] payload)
    {
        kind = 0;
        payload = null;
        if (count < 4) return false;

        int length = BitConverter.ToInt32(buffer, 0);
        if (length < 1 || length > Frame.MaxLength)
            throw new FormatException($"Panjang frame tidak masuk akal: {length}");

        if (count < 4 + length) return false;

        kind = buffer[4];
        payload = new byte[length - 1];
        Buffer.BlockCopy(buffer, 5, payload, 0, payload.Length);

        count -= 4 + length;
        Buffer.BlockCopy(buffer, 4 + length, buffer, 0, count);
        return true;
    }
}
