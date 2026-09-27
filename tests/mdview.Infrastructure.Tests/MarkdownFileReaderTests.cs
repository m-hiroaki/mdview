using System.Text;
using mdview.Infrastructure.FileSystem;

namespace mdview.Infrastructure.Tests;

public class MarkdownFileReaderTests
{
    [Fact]
    public async Task ReadTextAsync_ReadsUtf8WithBom()
    {
        var path = Path.GetTempFileName();
        var content = "# UTF-8";
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await File.WriteAllBytesAsync(path, encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray(), cancellationToken);

            var result = await new MarkdownFileReader().ReadTextAsync(path, cancellationToken);

            Assert.Equal(content, result);
    }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadTextAsync_RejectsInvalidUtf8()
    {
        var path = Path.GetTempFileName();
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await File.WriteAllBytesAsync(path, [0xC3, 0x28], cancellationToken);

            await Assert.ThrowsAsync<DecoderFallbackException>(() => new MarkdownFileReader().ReadTextAsync(path, cancellationToken));
    }
        finally
        {
            File.Delete(path);
        }
    }
}
