using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using DImageAnalysisResult = KyoshinMonitorLib.Images.ImageAnalysisResult;
using SImageAnalysisResult = KyoshinMonitorLib.SkiaImages.ImageAnalysisResult;

namespace Benchmark;

/// <summary>
/// 最適化前 (現行コミット時点) の実装を、A/B 比較のため自己完結で複製したもの。
/// ライブラリ本体を最適化しても比較基準がぶれないよう、変換ロジックもここに複製している。
/// </summary>
internal static class LegacyImages
{
	public static IEnumerable<DImageAnalysisResult> ParseScaleFromImage(IEnumerable<DImageAnalysisResult> points, Bitmap bitmap)
	{
		var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
		// 注意: 現行コードは UnlockBits を呼ばず Bitmap の Dispose に任せている。
		// ベンチマークでは同一 Bitmap を使い回すため、ここでは finally で解放する (lock/unlock コストは最適化版にも同様に含まれる)。
		try
		{
			Span<byte> pixelData;
			unsafe
			{
				pixelData = new Span<byte>(data.Scan0.ToPointer(), bitmap.Width * bitmap.Height);
			}
			foreach (var point in points)
			{
				if (point.ObservationPoint.Point == null || point.ObservationPoint.IsSuspended)
				{
					point.AnalysisResult = null;
					continue;
				}

				try
				{
					var color = bitmap.Palette.Entries[pixelData[bitmap.Width * point.ObservationPoint.Point.Value.Y + point.ObservationPoint.Point.Value.X]];
					point.Color = color;
					if (color.A != 255)
					{
						point.AnalysisResult = null;
						continue;
					}

					point.AnalysisResult = ConvertToScaleAtPolynomialInterpolation(color);
				}
				catch
				{
					point.AnalysisResult = null;
				}
			}
		}
		finally
		{
			bitmap.UnlockBits(data);
		}
		return points;
	}

	private static double ConvertToScaleAtPolynomialInterpolation(Color color)
		=> Math.Max(0, ConvertToScaleAtPolynomialInterpolationInternal(color));

	private static double ConvertToScaleAtPolynomialInterpolationInternal(Color color)
	{
		(var h, var s, var v) = GetHsv(color);
		h /= 360;

		if (v <= 0.1 || s <= 0.75)
			return 0;

		if (h > 0.1476)
			return 280.31 * Math.Pow(h, 6) - 916.05 * Math.Pow(h, 5) + 1142.6 * Math.Pow(h, 4) - 709.95 * Math.Pow(h, 3) + 234.65 * Math.Pow(h, 2) - 40.27 * h + 3.2217;
		else if (h > 0.001)
			return 151.4 * Math.Pow(h, 4) - 49.32 * Math.Pow(h, 3) + 6.753 * Math.Pow(h, 2) - 2.481 * h + 0.9033;
		else
			return -0.005171 * Math.Pow(v, 2) - 0.3282 * v + 1.2236;
	}

	private static (double h, double s, double v) GetHsv(Color rgb)
	{
		var max = Math.Max(rgb.R, Math.Max(rgb.G, rgb.B));
		var min = Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));

		if (min == max)
			return (0, 0, max / 255d);
		double w = max - min;
		var h = 0d;
		if (rgb.R == max)
			h = (rgb.G - rgb.B) / w;
		if (rgb.G == max)
			h = ((rgb.B - rgb.R) / w) + 2;
		if (rgb.B == max)
			h = ((rgb.R - rgb.G) / w) + 4;
		if ((h *= 60) < 0)
			h += 360;
		return (h, (double)(max - min) / max, max / 255d);
	}
}

/// <summary>
/// 最適化前 (現行コミット時点) の SkiaImages 実装の複製。
/// </summary>
internal static class LegacySkia
{
	public static IEnumerable<SImageAnalysisResult> ParseScaleFromImage(IEnumerable<SImageAnalysisResult> points, SKBitmap bitmap)
	{
		foreach (var point in points)
		{
			if (point.ObservationPoint.Point == null || point.ObservationPoint.IsSuspended)
			{
				point.AnalysisResult = null;
				continue;
			}

			try
			{
				var color = bitmap.GetPixel(point.ObservationPoint.Point.Value.X, point.ObservationPoint.Point.Value.Y);
				point.Color = color;
				if (color.Alpha != 255)
				{
					point.AnalysisResult = null;
					continue;
				}

				point.AnalysisResult = ConvertToScaleAtPolynomialInterpolation(color);
			}
			catch
			{
				point.AnalysisResult = null;
			}
		}
		return points;
	}

	private static double ConvertToScaleAtPolynomialInterpolation(SKColor color)
		=> Math.Max(0, ConvertToScaleAtPolynomialInterpolationInternal(color));

	private static double ConvertToScaleAtPolynomialInterpolationInternal(SKColor color)
	{
		(var h, var s, var v) = GetHsv(color);
		h /= 360;

		if (v <= 0.1 || s <= 0.75)
			return 0;

		if (h > 0.1476)
			return 280.31 * Math.Pow(h, 6) - 916.05 * Math.Pow(h, 5) + 1142.6 * Math.Pow(h, 4) - 709.95 * Math.Pow(h, 3) + 234.65 * Math.Pow(h, 2) - 40.27 * h + 3.2217;
		else if (h > 0.001)
			return 151.4 * Math.Pow(h, 4) - 49.32 * Math.Pow(h, 3) + 6.753 * Math.Pow(h, 2) - 2.481 * h + 0.9033;
		else
			return -0.005171 * Math.Pow(v, 2) - 0.3282 * v + 1.2236;
	}

	private static (double h, double s, double v) GetHsv(SKColor rgb)
	{
		var max = Math.Max(rgb.Red, Math.Max(rgb.Green, rgb.Blue));
		var min = Math.Min(rgb.Red, Math.Min(rgb.Green, rgb.Blue));

		if (min == max)
			return (0, 0, max / 255d);
		double w = max - min;
		var h = 0d;
		if (rgb.Red == max)
			h = (rgb.Green - rgb.Blue) / w;
		if (rgb.Green == max)
			h = ((rgb.Blue - rgb.Red) / w) + 2;
		if (rgb.Blue == max)
			h = ((rgb.Red - rgb.Green) / w) + 4;
		if ((h *= 60) < 0)
			h += 360;
		return (h, (double)(max - min) / max, max / 255d);
	}
}
