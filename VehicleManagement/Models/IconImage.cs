namespace VehicleManagement.Models;

/// <summary>Labels stored image bytes correctly for display; this is not upload validation.</summary>
public static class IconImage
{
    public static string DataUrl(byte[] bytes) => $"data:{MediaType(bytes)};base64,{Convert.ToBase64String(bytes)}";

    public static string MediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 0xff, 0xd8, 0xff })) return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return "image/gif";
        if (bytes.StartsWith("BM"u8)) return "image/bmp";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        if (bytes.Length >= 16 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var boxSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes[..4]);
            var end = (int)Math.Min(boxSize, (uint)bytes.Length);
            for (var offset = 8; offset + 4 <= end; offset += 4)
            {
                if (offset == 12) continue; // Minor version is not a format brand.
                var brand = bytes.Slice(offset, 4);
                if (brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8)) return "image/avif";
            }
        }
        // PNG is the original stored format; preserve rendering of legacy records.
        return "image/png";
    }
}
