using S4League.Resource.Internal;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace S4League.Resource;

/// <summary>
/// Higher-level S4 League transforms (SEED cipher wrapping, X7 obfuscation, checksum).
/// Faithful port of NetspherePirates' S4CryptoUtilities.
/// </summary>
internal static class S4CryptoUtilities
{
    private static readonly SecureRandom Random = new();

    public static long S4CRC(this byte[] data, string fullName)
    {
        long dataCrc = Crc32.Compute(data);
        long pathCrc = Crc32.Compute(System.Text.Encoding.ASCII.GetBytes(fullName));
        var finalCrc = dataCrc | (pathCrc << 32);

        var tmp = BitConverter.GetBytes(finalCrc);
        S4Crypt.OldCapped32.Encrypt(tmp);
        return BitConverter.ToInt64(tmp, 0);
    }

    public static void SwapBytes(this byte[] data)
    {
        var size = data.Length;
        var i = 0;
        var sizeCapped = size >= 128 ? 128 : size;

        while (i < sizeCapped / 2)
        {
            var j = size - 1 - i;
            (data[j], data[i]) = (data[i], data[j]);
            i++;
        }
    }

    public static void SwapBlocks(this byte[] data)
    {
        const int blockSize = 16;
        var buffer = new byte[blockSize];

        var numBlocks = data.Length / blockSize;
        for (var i = 0; i < numBlocks; i++)
        {
            Array.Copy(data, i * blockSize, buffer, 0, blockSize);
            for (var j = 0; j < blockSize; j++)
            {
                var block = j / 4;
                var blockIndex = j % 4;
                data[i * blockSize + j] = buffer[blockIndex * 4 + block];
            }
        }
    }

    public static byte[] InsertKeys(this byte[] data, byte[] key, byte[] iv)
    {
        using var r = data.ToBinaryReader();
        using var w = new BinaryWriter(new MemoryStream());

        if (data.Length >= 6)
        {
            var blockSize = data.Length / 3;
            w.Write(r.ReadBytes(blockSize));
            w.Write(key);
            w.Write(r.ReadBytes(blockSize));
            w.Write(iv);
            w.Write(r.ReadBytes(data.Length - (int)r.BaseStream.Position));
        }
        else
        {
            w.Write(key);
            w.Write(r.ReadBytes(data.Length));
            w.Write(iv);
        }

        return w.ToArray();
    }

    public static byte[] ExtractKeys(this byte[] data, out byte[] key, out byte[] iv)
    {
        var newSize = data.Length - 16 * 2;
        using var r = data.ToBinaryReader();
        using var w = new BinaryWriter(new MemoryStream());

        if (newSize >= 6)
        {
            var blockSize = newSize / 3;
            w.Write(r.ReadBytes(blockSize));
            key = r.ReadBytes(16);
            w.Write(r.ReadBytes(blockSize));
            iv = r.ReadBytes(16);
            w.Write(r.ReadBytes(data.Length - (int)r.BaseStream.Position));
        }
        else
        {
            key = r.ReadBytes(16);
            w.Write(r.ReadBytes(newSize));
            iv = r.ReadBytes(16);
        }

        return w.ToArray();
    }

    /// <param name="swapOuterBlocks">
    /// When true (canonical), the final block-transpose is applied. The S4Max container variant
    /// omits it (the inner transpose on the plaintext is always applied).
    /// </param>
    public static byte[] EncryptSeed(this byte[] data, bool swapOuterBlocks = true)
    {
        var key = new byte[16];
        var iv = new byte[16];
        Random.NextBytes(key);
        Random.NextBytes(iv);

        data = data.FastClone();
        data.SwapBlocks();

        var parameters = new ParametersWithIV(new KeyParameter(key), iv);
        var cipher = CipherUtilities.GetCipher("SEED/SIC");
        cipher.Init(true, parameters);

        var output = new byte[cipher.GetOutputSize(data.Length)];
        var len = cipher.ProcessBytes(data, 0, data.Length, output, 0);
        cipher.DoFinal(output, len);

        S4Crypt.Default.Encrypt(output);
        output = output.InsertKeys(key, iv);
        if (swapOuterBlocks)
            output.SwapBlocks();

        return output;
    }

    public static byte[] DecryptSeed(this byte[] data, bool swapOuterBlocks = true)
    {
        data = data.FastClone();
        if (swapOuterBlocks)
            data.SwapBlocks();
        data = data.ExtractKeys(out var key, out var iv);
        S4Crypt.Default.Decrypt(data);

        var parameters = new ParametersWithIV(new KeyParameter(key), iv);
        var cipher = CipherUtilities.GetCipher("SEED/SIC");
        cipher.Init(false, parameters);

        var output = new byte[cipher.GetOutputSize(data.Length)];
        var len = cipher.ProcessBytes(data, 0, data.Length, output, 0);
        cipher.DoFinal(output, len);

        output.SwapBlocks();
        return output;
    }

    public static byte[] EncryptX7(this byte[] data)
    {
        var crc = X7CRC(data);
        var realSize = data.Length;

        data = MiniLzo.Compress(data);
        data = BuildX7(data, crc, realSize);

        return data;
    }

    public static byte[] DecryptX7(this byte[] data)
    {
        var realSize = (int)(BitConverter.ToInt32(data, 0) ^ 0xFE292513);
        data = RemoveX7Junk(data);
        return MiniLzo.Decompress(data, realSize);
    }

    private static byte[] BuildX7(byte[] data, uint crc, int realSize)
    {
        var newSize = data.Length * 4 + 8;
        var encrypted1 = data.FastClone();
        var encrypted2 = data.FastClone();
        S4Crypt.Default.Encrypt(encrypted1, newSize, 0);
        S4Crypt.Default.Encrypt(encrypted2, newSize, 1);

        using var w = new BinaryWriter(new MemoryStream(newSize));
        var encryptedSize = (int)(realSize ^ 0xFE292513);
        w.Write(encryptedSize);
        w.Write(crc);
        for (var i = 0; i < data.Length; i++)
        {
            w.Write(data[i]);
            w.Write(encrypted1[i]);
            w.Write(data[i]);
            w.Write(encrypted2[i]);
        }

        return w.ToArray();
    }

    private static byte[] RemoveX7Junk(byte[] data)
    {
        var newSize = (data.Length - 8) / 4;
        var outBuffer = new byte[newSize];
        for (var i = 0; i < newSize; i++)
            outBuffer[i] = data[i * 4 + 8];

        return outBuffer;
    }

    private static uint X7CRC(byte[] data) => Crc32.Compute(data) ^ 0xBAD0A4B3;
}
