using System.Drawing;
using System.Drawing.Imaging;

namespace MoreThanCode.AFrameExample.Walk;

#pragma warning disable CA1416
public class Watermark
{
    private const string WatermarkText = "DogWalking";
    private static readonly Font ArialFont = new("Arial", 24, FontStyle.Bold);
    private static readonly Color SemiTransparentWhite = Color.FromArgb(128, 255, 255, 255);
    private static readonly Brush SoldiBrush = new SolidBrush(SemiTransparentWhite);

    public byte[] Add(byte[] imageBytes)
    {
        var image = ToImage(imageBytes);
        using var graphics = Graphics.FromImage(image);


        Point bottomLeftCorner = new(10, image.Height - 50);
        graphics.DrawString(WatermarkText, ArialFont, SoldiBrush, bottomLeftCorner);

        return ToBytes(image);
    }

    private static Image ToImage(byte[] imageBytes)
    {
        var imageStream = new MemoryStream(imageBytes.Length);
        imageStream.Write(imageBytes, 0, imageBytes.Length);
        return Image.FromStream(imageStream);
    }

    private static byte[] ToBytes(Image image)
    {
        var resultStream = new MemoryStream();
        image.Save(resultStream, ImageFormat.Jpeg);
        return resultStream.ToArray();
    }
}
#pragma warning restore CA1416