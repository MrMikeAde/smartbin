using SmartBin.Core.Services;
using Xunit;

namespace SmartBin.Core.Tests
{
    public class CompressionHeuristicsTests
    {
        [Theory]
        [InlineData("video.mp4", true)]
        [InlineData("document.pdf", true)]
        [InlineData("archive.zip", true)]
        [InlineData("image.png", true)]
        [InlineData("TEXT.TXT", false)]
        [InlineData("code.cs", false)]
        [InlineData("data.json", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsTypicallyCompressed_ChecksExtensionsCorrectly(string? path, bool expected)
        {
            // Act
            bool result = CompressionHeuristics.IsTypicallyCompressed(path!);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
