using Geometry.Primitives;
using TiledImage.Core.Types;

namespace TiledImage.Transforms.Sampling;

/// <summary>
/// Pixel resampling for image transformation.
/// Optimized for Gray8 and Gray16 formats commonly used in industrial imaging.
/// </summary>
public static class PixelResampler
{
    /// <summary>
    /// Resample a region of pixels using the inverse transform matrix.
    /// Uses backward mapping: for each output pixel, find the corresponding source pixel.
    /// </summary>
    /// <param name="sampler">Source pixel sampler</param>
    /// <param name="inverseTransform">Matrix to map output coordinates to source coordinates</param>
    /// <param name="outputBuffer">Buffer to write resampled pixels</param>
    /// <param name="outputX">Output region X offset</param>
    /// <param name="outputY">Output region Y offset</param>
    /// <param name="outputWidth">Output region width</param>
    /// <param name="outputHeight">Output region height</param>
    /// <param name="outputStride">Output buffer stride (bytes per row)</param>
    /// <param name="bytesPerPixel">Bytes per pixel</param>
    /// <param name="pixelFormat">Pixel format for proper interpolation</param>
    /// <param name="mode">Interpolation mode</param>
    public static void ResampleRegion(
        IPixelSampler sampler,
        Matrix3x3 inverseTransform,
        byte[] outputBuffer,
        int outputX,
        int outputY,
        int outputWidth,
        int outputHeight,
        int outputStride,
        int bytesPerPixel,
        PixelFormat pixelFormat,
        InterpolationMode mode)
    {
        for (int row = 0; row < outputHeight; row++)
        {
            int destY = outputY + row;
            int destRowOffset = row * outputStride;

            for (int col = 0; col < outputWidth; col++)
            {
                int destX = outputX + col;

                // Map output pixel to source coordinates using inverse transform
                var srcPoint = inverseTransform.MapPoint(new Point2F(destX, destY));
                float srcX = srcPoint.X;
                float srcY = srcPoint.Y;

                // Resample based on mode and format
                int destOffset = destRowOffset + col * bytesPerPixel;

                switch (pixelFormat)
                {
                    case PixelFormat.Gray8:
                        outputBuffer[destOffset] = ResampleGray8(sampler, srcX, srcY, mode);
                        break;

                    case PixelFormat.Gray16:
                        ushort value16 = ResampleGray16(sampler, srcX, srcY, mode);
                        outputBuffer[destOffset] = (byte)(value16 & 0xFF);
                        outputBuffer[destOffset + 1] = (byte)(value16 >> 8);
                        break;

                    case PixelFormat.Rgb24:
                    case PixelFormat.Bgr24:
                        ResampleRgb24(sampler, srcX, srcY, mode, outputBuffer.AsSpan(destOffset, 3));
                        break;

                    default:
                        // For other formats, use nearest neighbor
                        ResampleGeneric(sampler, srcX, srcY, bytesPerPixel, outputBuffer.AsSpan(destOffset, bytesPerPixel));
                        break;
                }
            }
        }
    }

    private static byte ResampleGray8(IPixelSampler sampler, float x, float y, InterpolationMode mode)
    {
        return mode switch
        {
            InterpolationMode.NearestNeighbor => sampler.GetGray8(
                (int)MathF.Round(x),
                (int)MathF.Round(y)),

            InterpolationMode.Bilinear => BilinearGray8(sampler, x, y),

            InterpolationMode.Bicubic => BicubicGray8(sampler, x, y),

            _ => sampler.GetGray8((int)MathF.Round(x), (int)MathF.Round(y))
        };
    }

    private static ushort ResampleGray16(IPixelSampler sampler, float x, float y, InterpolationMode mode)
    {
        return mode switch
        {
            InterpolationMode.NearestNeighbor => sampler.GetGray16(
                (int)MathF.Round(x),
                (int)MathF.Round(y)),

            InterpolationMode.Bilinear => BilinearGray16(sampler, x, y),

            InterpolationMode.Bicubic => BicubicGray16(sampler, x, y),

            _ => sampler.GetGray16((int)MathF.Round(x), (int)MathF.Round(y))
        };
    }

    private static void ResampleRgb24(
        IPixelSampler sampler,
        float x,
        float y,
        InterpolationMode mode,
        Span<byte> dest)
    {
        if (mode == InterpolationMode.NearestNeighbor)
        {
            sampler.GetRgb24((int)MathF.Round(x), (int)MathF.Round(y), dest);
        }
        else
        {
            BilinearRgb24(sampler, x, y, dest);
        }
    }

    private static void ResampleGeneric(
        IPixelSampler sampler,
        float x,
        float y,
        int bytesPerPixel,
        Span<byte> dest)
    {
        sampler.GetPixelBytes((int)MathF.Round(x), (int)MathF.Round(y), dest);
    }

    #region Bilinear Interpolation

    private static byte BilinearGray8(IPixelSampler sampler, float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = x - x0;
        float fy = y - y0;

        byte v00 = sampler.GetGray8(x0, y0);
        byte v10 = sampler.GetGray8(x0 + 1, y0);
        byte v01 = sampler.GetGray8(x0, y0 + 1);
        byte v11 = sampler.GetGray8(x0 + 1, y0 + 1);

        float result = v00 * (1 - fx) * (1 - fy) +
                       v10 * fx * (1 - fy) +
                       v01 * (1 - fx) * fy +
                       v11 * fx * fy;

        return (byte)Math.Clamp(result, 0, 255);
    }

    private static ushort BilinearGray16(IPixelSampler sampler, float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = x - x0;
        float fy = y - y0;

        ushort v00 = sampler.GetGray16(x0, y0);
        ushort v10 = sampler.GetGray16(x0 + 1, y0);
        ushort v01 = sampler.GetGray16(x0, y0 + 1);
        ushort v11 = sampler.GetGray16(x0 + 1, y0 + 1);

        float result = v00 * (1 - fx) * (1 - fy) +
                       v10 * fx * (1 - fy) +
                       v01 * (1 - fx) * fy +
                       v11 * fx * fy;

        return (ushort)Math.Clamp(result, 0, 65535);
    }

    private static void BilinearRgb24(
        IPixelSampler sampler,
        float x,
        float y,
        Span<byte> dest)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = x - x0;
        float fy = y - y0;

        Span<byte> p00 = stackalloc byte[3];
        Span<byte> p10 = stackalloc byte[3];
        Span<byte> p01 = stackalloc byte[3];
        Span<byte> p11 = stackalloc byte[3];

        sampler.GetRgb24(x0, y0, p00);
        sampler.GetRgb24(x0 + 1, y0, p10);
        sampler.GetRgb24(x0, y0 + 1, p01);
        sampler.GetRgb24(x0 + 1, y0 + 1, p11);

        for (int i = 0; i < 3; i++)
        {
            float result = p00[i] * (1 - fx) * (1 - fy) +
                           p10[i] * fx * (1 - fy) +
                           p01[i] * (1 - fx) * fy +
                           p11[i] * fx * fy;
            dest[i] = (byte)Math.Clamp(result, 0, 255);
        }
    }

    #endregion

    #region Bicubic Interpolation

    private static byte BicubicGray8(IPixelSampler sampler, float x, float y)
    {
        int xi = (int)MathF.Floor(x);
        int yi = (int)MathF.Floor(y);
        float fx = x - xi;
        float fy = y - yi;

        float result = 0;
        for (int j = -1; j <= 2; j++)
        {
            float wy = CubicWeight(fy - j);
            for (int i = -1; i <= 2; i++)
            {
                float wx = CubicWeight(fx - i);
                result += sampler.GetGray8(xi + i, yi + j) * wx * wy;
            }
        }

        return (byte)Math.Clamp(result, 0, 255);
    }

    private static ushort BicubicGray16(IPixelSampler sampler, float x, float y)
    {
        int xi = (int)MathF.Floor(x);
        int yi = (int)MathF.Floor(y);
        float fx = x - xi;
        float fy = y - yi;

        float result = 0;
        for (int j = -1; j <= 2; j++)
        {
            float wy = CubicWeight(fy - j);
            for (int i = -1; i <= 2; i++)
            {
                float wx = CubicWeight(fx - i);
                result += sampler.GetGray16(xi + i, yi + j) * wx * wy;
            }
        }

        return (ushort)Math.Clamp(result, 0, 65535);
    }

    /// <summary>
    /// Cubic interpolation weight function (Catmull-Rom spline).
    /// </summary>
    private static float CubicWeight(float t)
    {
        t = MathF.Abs(t);
        if (t <= 1)
            return (1.5f * t - 2.5f) * t * t + 1;
        if (t < 2)
            return ((-0.5f * t + 2.5f) * t - 4) * t + 2;
        return 0;
    }

    #endregion
}
