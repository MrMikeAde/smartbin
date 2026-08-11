using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SmartBin.Infrastructure.Hashing;
using Xunit;

namespace SmartBin.Infrastructure.Tests
{
    public class Sha256FileHasherTests : IDisposable
    {
        private readonly string _tempFile;

        public Sha256FileHasherTests()
        {
            _tempFile = Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }

        [Fact]
        public async Task ComputeHashAsync_WithKnownString_ReturnsCorrectSha256()
        {
            // Arrange
            var input = "Hello SmartBin";
            // SHA-256 for "Hello SmartBin" (UTF-8 without BOM) is 33ed1d1f6c550a9fd90c32620de2d669f1471d7b2148e031710ad23719909f4f
            var expectedHash = "33ed1d1f6c550a9fd90c32620de2d669f1471d7b2148e031710ad23719909f4f";

            // Explicitly use UTF-8 without BOM
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            await File.WriteAllTextAsync(_tempFile, input, encoding);
            var hasher = new Sha256FileHasher();

            // Act
            var hash = await hasher.ComputeHashAsync(_tempFile);

            // Assert
            Assert.Equal(expectedHash, hash);
        }

        [Fact]
        public async Task ComputeHashAsync_WithEmptyFile_ReturnsCorrectSha256()
        {
            // Arrange
            // SHA-256 for empty file/string is e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
            var expectedHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            await File.WriteAllTextAsync(_tempFile, string.Empty);
            var hasher = new Sha256FileHasher();

            // Act
            var hash = await hasher.ComputeHashAsync(_tempFile);

            // Assert
            Assert.Equal(expectedHash, hash);
        }

        [Fact]
        public async Task ComputeHashAsync_WithStream_ReturnsCorrectSha256()
        {
            // Arrange
            var input = "Hello SmartBin";
            var expectedHash = "33ed1d1f6c550a9fd90c32620de2d669f1471d7b2148e031710ad23719909f4f";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            var hasher = new Sha256FileHasher();

            // Act
            var hash = await hasher.ComputeHashAsync(stream);

            // Assert
            Assert.Equal(expectedHash, hash);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task ComputeHashAsync_WithInvalidFilePath_ThrowsArgumentException(string? invalidPath)
        {
            // Arrange
            var hasher = new Sha256FileHasher();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => hasher.ComputeHashAsync(invalidPath!));
        }

        [Fact]
        public async Task ComputeHashAsync_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var hasher = new Sha256FileHasher();

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => hasher.ComputeHashAsync(nonExistentPath));
        }
    }
}
