using KyoshinMonitorLib;
using System;
using System.Linq;
using DImageAnalysisResult = KyoshinMonitorLib.Images.ImageAnalysisResult;
using SImageAnalysisResult = KyoshinMonitorLib.SkiaImages.ImageAnalysisResult;

namespace Benchmark;

/// <summary>
/// 最適化版が現行実装 (Legacy) と同じ解析結果を返すことを確認する簡易検証。
/// 実行: dotnet run --project src/Benchmark -c Release -- verify
/// </summary>
internal static class Verification
{
	public static int Run()
	{
		const int count = 1300;
		const double epsilon = 1e-6;

		using var bitmap = TestData.CreateBitmap();
		using var skBitmap = TestData.CreateSkBitmap(bitmap);
		var basePoints = TestData.CreatePoints(count);

		var ok = true;

		// --- Images ---
		var imgLegacy = basePoints.Select(p => new DImageAnalysisResult(p)).ToArray();
		var imgOpt = basePoints.Select(p => new DImageAnalysisResult(p)).ToArray();
		LegacyImages.ParseScaleFromImage(imgLegacy, bitmap);
		KyoshinMonitorLib.Images.Extensions.ParseScaleFromImage(imgOpt, bitmap);
		var imgMaxDiff = Compare(imgLegacy.Select(r => r.AnalysisResult).ToArray(), imgOpt.Select(r => r.AnalysisResult).ToArray(), epsilon, "Images");
		ok &= imgMaxDiff >= 0;
		Console.WriteLine($"[Images] legacy vs optimized: maxDiff={imgMaxDiff:G6}");

		// --- Skia ---
		var skLegacy = basePoints.Select(p => new SImageAnalysisResult(p)).ToArray();
		var skOpt = basePoints.Select(p => new SImageAnalysisResult(p)).ToArray();
		LegacySkia.ParseScaleFromImage(skLegacy, skBitmap);
		KyoshinMonitorLib.SkiaImages.Extensions.ParseScaleFromImage(skOpt, skBitmap);
		var skMaxDiff = Compare(skLegacy.Select(r => r.AnalysisResult).ToArray(), skOpt.Select(r => r.AnalysisResult).ToArray(), epsilon, "Skia");
		ok &= skMaxDiff >= 0;
		Console.WriteLine($"[Skia]   legacy vs optimized: maxDiff={skMaxDiff:G6}");

		Console.WriteLine(ok ? "RESULT: OK (一致)" : "RESULT: NG (不一致あり)");
		return ok ? 0 : 1;
	}

	/// <summary>
	/// 2 つの結果列を比較し最大差を返す。null の一致/不一致が出た場合や閾値超過は -1 を返す。
	/// </summary>
	private static double Compare(double?[] a, double?[] b, double epsilon, string label)
	{
		var maxDiff = 0.0;
		for (var i = 0; i < a.Length; i++)
		{
			if (a[i] is null && b[i] is null)
				continue;
			if (a[i] is not double va || b[i] is not double vb)
			{
				Console.WriteLine($"[{label}] index {i}: null 不一致 legacy={a[i]?.ToString() ?? "null"} optimized={b[i]?.ToString() ?? "null"}");
				return -1;
			}
			var diff = Math.Abs(va - vb);
			if (diff > maxDiff)
				maxDiff = diff;
			if (diff > epsilon)
			{
				Console.WriteLine($"[{label}] index {i}: 値不一致 legacy={va} optimized={vb} diff={diff}");
				return -1;
			}
		}
		return maxDiff;
	}
}
