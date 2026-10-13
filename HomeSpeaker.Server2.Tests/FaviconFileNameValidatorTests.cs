using HomeSpeaker.Server2.Services;

namespace HomeSpeaker.Server2.Tests;

public sealed class FaviconFileNameValidatorTests : IDisposable
{
    private readonly string faviconsDirectory = Path.Combine(Path.GetTempPath(), "hs-favicon-tests-" + Guid.NewGuid().ToString("N"));

    public FaviconFileNameValidatorTests()
    {
        Directory.CreateDirectory(faviconsDirectory);
        File.WriteAllBytes(Path.Combine(faviconsDirectory, "kbaq.png"), [1, 2, 3]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankMeansLeaveUnchanged(string? fileName)
    {
        Assert.Null(FaviconFileNameValidator.Validate(fileName, faviconsDirectory));
    }

    [Fact]
    public void ExistingBareFileNameIsAccepted()
    {
        Assert.Null(FaviconFileNameValidator.Validate("kbaq.png", faviconsDirectory));
    }

    [Fact]
    public void MissingFileIsRejected()
    {
        Assert.NotNull(FaviconFileNameValidator.Validate("nope.png", faviconsDirectory));
    }

    [Theory]
    [InlineData("../kbaq.png")]
    [InlineData("..\\kbaq.png")]
    [InlineData("..")]
    [InlineData("sub/kbaq.png")]
    [InlineData("sub\\kbaq.png")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("kbaq..png")]
    public void PathsAndTraversalAreRejected(string fileName)
    {
        Assert.NotNull(FaviconFileNameValidator.Validate(fileName, faviconsDirectory));
    }

    [Fact]
    public void TraversalIsRejectedEvenWhenTheTargetExists()
    {
        var parentFile = Path.Combine(Path.GetDirectoryName(faviconsDirectory)!, Path.GetFileName(faviconsDirectory) + ".png");
        File.WriteAllBytes(parentFile, [1]);
        try
        {
            Assert.NotNull(FaviconFileNameValidator.Validate("../" + Path.GetFileName(parentFile), faviconsDirectory));
        }
        finally
        {
            File.Delete(parentFile);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/logo.png")]
    [InlineData("http://example.com/logo.png")]
    public void BlankOrHttpUrlIsAccepted(string? url)
    {
        Assert.Null(FaviconFileNameValidator.ValidateDownloadUrl(url));
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("logo.png")]
    [InlineData("ftp://example.com/logo.png")]
    public void NonHttpUrlIsRejected(string url)
    {
        Assert.NotNull(FaviconFileNameValidator.ValidateDownloadUrl(url));
    }

    public void Dispose()
    {
        Directory.Delete(faviconsDirectory, recursive: true);
    }
}
