using System;
using SmartBin.Core.Models;
using Xunit;

namespace SmartBin.Core.Tests
{
    public class SmartBinItemTests
    {
        [Fact]
        public void IsValid_WithValidData_ReturnsTrue()
        {
            // Arrange
            var item = new SmartBinItem
            {
                OriginalPath = @"C:\Users\Test\Documents\file.txt",
                OriginalFileName = "file.txt",
                OriginalSize = 1024,
                CurrentStoredSize = 1024,
                Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
            };

            // Act & Assert
            Assert.True(item.IsValid());
        }

        [Theory]
        [InlineData("", "file.txt", "hash")]
        [InlineData(@"C:\path.txt", "", "hash")]
        [InlineData(@"C:\path.txt", "file.txt", "")]
        [InlineData(@"C:\path.txt", "file.txt", "   ")]
        public void IsValid_WithInvalidStringInputs_ReturnsFalse(string originalPath, string originalFileName, string hash)
        {
            // Arrange
            var item = new SmartBinItem
            {
                OriginalPath = originalPath,
                OriginalFileName = originalFileName,
                OriginalSize = 1024,
                CurrentStoredSize = 1024,
                Sha256Hash = hash
            };

            // Act & Assert
            Assert.False(item.IsValid());
        }

        [Fact]
        public void IsValid_WithNegativeSizes_ReturnsFalse()
        {
            // Arrange
            var item1 = new SmartBinItem
            {
                OriginalPath = @"C:\path.txt",
                OriginalFileName = "path.txt",
                OriginalSize = -1,
                CurrentStoredSize = 1024,
                Sha256Hash = "hash"
            };

            var item2 = new SmartBinItem
            {
                OriginalPath = @"C:\path.txt",
                OriginalFileName = "path.txt",
                OriginalSize = 1024,
                CurrentStoredSize = -1,
                Sha256Hash = "hash"
            };

            // Act & Assert
            Assert.False(item1.IsValid());
            Assert.False(item2.IsValid());
        }

        [Fact]
        public void UpdateCompressionResult_WhenCompressedIsSmaller_UpdatesStatusToCompressed()
        {
            // Arrange
            var item = new SmartBinItem
            {
                OriginalSize = 1000,
                CurrentStoredSize = 1000,
                CompressionStatus = CompressionStatus.Uncompressed
            };

            // Act
            item.UpdateCompressionResult(400, CompressionAlgorithm.Zip);

            // Assert
            Assert.Equal(CompressionStatus.Compressed, item.CompressionStatus);
            Assert.Equal(CompressionAlgorithm.Zip, item.CompressionAlgorithm);
            Assert.Equal(400, item.CurrentStoredSize);
            Assert.NotNull(item.CompressionTimestamp);
        }

        [Fact]
        public void UpdateCompressionResult_WhenCompressedIsLargerOrEqual_UpdatesStatusToNotFeasible()
        {
            // Arrange
            var item = new SmartBinItem
            {
                OriginalSize = 1000,
                CurrentStoredSize = 1000,
                CompressionStatus = CompressionStatus.Uncompressed
            };

            // Act
            item.UpdateCompressionResult(1000, CompressionAlgorithm.Zip);

            // Assert
            Assert.Equal(CompressionStatus.NotFeasible, item.CompressionStatus);
            Assert.Equal(CompressionAlgorithm.None, item.CompressionAlgorithm);
            Assert.Equal(1000, item.CurrentStoredSize);
            Assert.NotNull(item.CompressionTimestamp);
        }

        [Fact]
        public void UpdateCompressionResult_WithNegativeSize_ThrowsArgumentException()
        {
            // Arrange
            var item = new SmartBinItem { OriginalSize = 100 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => item.UpdateCompressionResult(-10, CompressionAlgorithm.Zip));
        }
    }
}
