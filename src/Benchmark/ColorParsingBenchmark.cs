using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SkiaSharp;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DImageAnalysisResult = KyoshinMonitorLib.Images.ImageAnalysisResult;
using SImageAnalysisResult = KyoshinMonitorLib.SkiaImages.ImageAnalysisResult;

namespace Benchmark;

/// <summary>
/// 画像からの色取得 (ParseScaleFromImage) について、最適化前 (Legacy) と最適化後 (Optimized) を
/// System.Drawing 版 / SkiaSharp 版それぞれで比較する。画像デコードは対象外で、色取得部分のみを計測する。
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ColorParsingBenchmark
{
	private Bitmap _bitmap = null!;
	private SKBitmap _skBitmap = null!;
	private DImageAnalysisResult[] _imagesPoints = null!;
	private SImageAnalysisResult[] _skiaPoints = null!;

	/// <summary>観測点数 (実運用は約 1300 点)</summary>
	[Params(1300)]
	public int PointCount;

	[GlobalSetup]
	public void Setup()
	{
		_bitmap = TestData.CreateBitmap();
		_skBitmap = TestData.CreateSkBitmap(_bitmap);
		var points = TestData.CreatePoints(PointCount);
		_imagesPoints = points.Select(p => new DImageAnalysisResult(p)).ToArray();
		_skiaPoints = points.Select(p => new SImageAnalysisResult(p)).ToArray();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		_bitmap.Dispose();
		_skBitmap.Dispose();
	}

	[BenchmarkCategory("Images (System.Drawing)"), Benchmark(Baseline = true)]
	public double Images_Legacy()
		=> Consume(LegacyImages.ParseScaleFromImage(_imagesPoints, _bitmap));

	[BenchmarkCategory("Images (System.Drawing)"), Benchmark]
	public double Images_Optimized()
		=> Consume(KyoshinMonitorLib.Images.Extensions.ParseScaleFromImage(_imagesPoints, _bitmap));

	[BenchmarkCategory("Skia (SkiaSharp)"), Benchmark(Baseline = true)]
	public double Skia_Legacy()
		=> Consume(LegacySkia.ParseScaleFromImage(_skiaPoints, _skBitmap));

	[BenchmarkCategory("Skia (SkiaSharp)"), Benchmark]
	public double Skia_Optimized()
		=> Consume(KyoshinMonitorLib.SkiaImages.Extensions.ParseScaleFromImage(_skiaPoints, _skBitmap));

	// 結果を集計して返すことで JIT のデッドコード除去を防ぐ
	private static double Consume(IEnumerable<DImageAnalysisResult> results)
	{
		double sum = 0;
		foreach (var r in results)
			sum += r.AnalysisResult ?? 0;
		return sum;
	}

	private static double Consume(IEnumerable<SImageAnalysisResult> results)
	{
		double sum = 0;
		foreach (var r in results)
			sum += r.AnalysisResult ?? 0;
		return sum;
	}
}
