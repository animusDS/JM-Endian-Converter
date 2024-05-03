using System.IO.Compression;
using System.Text.Json.Nodes;

namespace JMEndianScript;

public static class JmEndianScript {
    public static void Main(string[] args) {
        string? path;
        string? outputPath;
        if (args.Length == 2) {
            path = args[0];
            outputPath = args[1];
        }
        else {
            if (args.Length > 0) {
                Console.WriteLine("Invalid arguments.");
                Thread.Sleep(2000);
                return;
            }
            
            Console.Write("Enter path of .jm files: ");
            path = Console.ReadLine();

            Console.WriteLine("Enter output path: ");
            outputPath = Console.ReadLine();
        }
        
        if (!Directory.Exists(path)) {
            Console.WriteLine("Invalid path to .jm files.");
            Thread.Sleep(2000);
            return;
        }
        
        if (!Directory.Exists(outputPath)) {
            Console.WriteLine("Invalid output path.");
            Thread.Sleep(2000);
            return;
        }

        var files = Directory.GetFiles(path, "*.jm", SearchOption.AllDirectories);
        foreach (var file in files) {
            var fileName = Path.GetFileNameWithoutExtension(file);
            Console.WriteLine($"Processing {fileName}...");

            var mapObj = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            var width = mapObj["width"]!.GetValue<int>();
            var height = mapObj["height"]!.GetValue<int>();
            var tiles = mapObj["dict"]!.AsArray();
            var data = Zlib.Decompress(Convert.FromBase64String(mapObj["data"]!.GetValue<string>()));
            Console.WriteLine($"Processed data: [width: {width}, height: {height}, dict: {tiles.Count}, data: {data.Length}]");

            var newBytes = new byte[data.Length];
            for (var i = 0; i < data.Length; i += 2) {
                newBytes[i] = data[i + 1];
                newBytes[i + 1] = data[i];
            }

            var newBase64 = Convert.ToBase64String(Zlib.Compress(newBytes));
            mapObj["data"] = newBase64;
            
            File.WriteAllText(Path.Combine(outputPath, $"{fileName}.jm"), mapObj.ToString());
        }
        
        Console.WriteLine("Done.");
    }
}

// credits https://github.com/creepylava/RotMG-Dungeon-Generator
public static class Zlib {
    static uint ADLER32(IEnumerable<byte> data) {
        const uint modulo = 0xfff1;
        uint a = 1, b = 0;
        foreach (var t in data) {
            a = (a + t) % modulo;
            b = (b + a) % modulo;
        }

        return b << 16 | a;
    }

    public static byte[] Compress(byte[] buffer) {
        byte[] comp;
        using (var output = new MemoryStream()) {
            using (var deflate = new DeflateStream(output, CompressionMode.Compress))
                deflate.Write(buffer, 0, buffer.Length);

            comp = output.ToArray();
        }

        // Refer to http://www.ietf.org/rfc/rfc1950.txt for zlib format
        const byte cm = 8;
        const byte cInfo = 7;
        const byte cmf = cm | cInfo << 4;
        const byte flg = 0xDA;

        var result = new byte[comp.Length + 6];
        result[0] = cmf;
        result[1] = flg;
        Buffer.BlockCopy(comp, 0, result, 2, comp.Length);

        var checkSum = ADLER32(buffer);
        var index = result.Length - 4;
        result[index++] = (byte) (checkSum >> 24);
        result[index++] = (byte) (checkSum >> 16);
        result[index++] = (byte) (checkSum >> 8);
        result[index++] = (byte) (checkSum >> 0);
        return result;
    }

    public static byte[] Decompress(byte[] buffer) {
        // cbf to find the unobfuscated version of this
        var num1 = buffer.Length >= 6 ? buffer[0] : throw new ArgumentException("Invalid ZLIB buffer.");
        var num2 = buffer[1];
        var num3 = (byte) (num1 & 15U);
        var num4 = (byte) ((uint) num1 >> 4);
        if (num3 != 8) {
            throw new NotSupportedException("Invalid compression method.");
        }

        if (num4 != 7) {
            throw new NotSupportedException("Unsupported window size.");
        }

        if ((num2 & 32) != 0) {
            throw new NotSupportedException("Preset dictionary not supported.");
        }

        if (((num1 << 8) + num2) % 31 != 0) {
            throw new InvalidDataException("Invalid header checksum");
        }

        var memoryStream1 = new MemoryStream(buffer, 2, buffer.Length - 6);
        var memoryStream2 = new MemoryStream();
        using (var deflateStream = new DeflateStream(memoryStream1, CompressionMode.Decompress)) {
            deflateStream.CopyTo(memoryStream2);
        }

        var array = memoryStream2.ToArray();
        var num5 = buffer.Length - 4;
        var num6 = num5 + 1;
        var num7 = buffer[num5] << 24;
        var num8 = num6 + 1;
        var num9 = buffer[num6] << 16;
        var num10 = num7 | num9;
        var num11 = num8 + 1;
        var num12 = buffer[num8] << 8;
        var num13 = num10 | num12;
        int num15 = buffer[num11];
        if ((num13 | num15) != (int) ADLER32(array)) {
            throw new InvalidDataException("Invalid data checksum");
        }

        return array;
    }
}