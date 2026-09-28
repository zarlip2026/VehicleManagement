using VehicleManagement.Models;
using Xunit;

namespace VehicleManagement.UnitTests;

public class IconImageTests
{
    [Theory]
    [InlineData("89504E470D0A1A0A", "image/png")]
    [InlineData("FFD8FFE0", "image/jpeg")]
    [InlineData("474946383761", "image/gif")]
    [InlineData("474946383961", "image/gif")]
    [InlineData("424D00000000", "image/bmp")]
    [InlineData("524946460000000057454250", "image/webp")]
    [InlineData("00000010667479706176696600000000", "image/avif")]
    [InlineData("00000014667479706D6966310000000061766966", "image/avif")]
    public void StoredImage_UsesCorrectMediaTypeWithoutChangingBytes(string hex, string expected)
    {
        var bytes = Convert.FromHexString(hex);
        Assert.Equal($"data:{expected};base64,{Convert.ToBase64String(bytes)}", IconImage.DataUrl(bytes));
    }

    [Theory]
    [InlineData("")]
    [InlineData("010203")]
    [InlineData("52494646")]
    [InlineData("0000001066747970")]
    public void ShortOrLegacyData_PreservesOriginalPngFallback(string hex)
    {
        Assert.Equal("image/png", IconImage.MediaType(Convert.FromHexString(hex)));
    }
}
