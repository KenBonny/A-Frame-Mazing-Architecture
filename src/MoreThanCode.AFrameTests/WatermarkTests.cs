using MoreThanCode.AFrameExample.Walk;

namespace MoreThanCode.AFrameTests;

public class WatermarkTests
{
    [Test]
    [Explicit]
    public void Add_watermark_to_picture()
    {
        var imageBytes = File.ReadAllBytes(@"C:\Users\kenbo\Downloads\Yuna on leash.jpg");
        var imageWithWatermark = new Watermark().Add(imageBytes);
        File.WriteAllBytes(@"C:\Users\kenbo\Downloads\Yuna on leash - Watermark.jpg", imageWithWatermark);
    }

    [Test]
    public async Task Ignore_empty_picture()
    {
        var emptyResponse = new Watermark().Add([]);
        await Assert.That(emptyResponse).IsEmpty();
    }
}