using KyoshinMonitorLib;
using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Benchmark;

/// <summary>
/// ベンチマーク用のテストデータ生成。
/// 実物の強震モニタ画像 (352x400 / GIF / パレットカラー) を模した合成画像と、観測点を生成する。
/// ネットワークや外部ファイルに依存せず、毎回同じ結果になるよう乱数シードを固定している。
/// </summary>
internal static class TestData
{
	// 実際の強震モニタリアルタイム画像と同じサイズ
	public const int Width = 352;
	public const int Height = 400;

	/// <summary>
	/// パレット (8bpp インデックスカラー) を持つ Bitmap を生成する。
	/// 0..220: 青→赤のビビッドなグラデーション (実スケールの色域を模擬)
	/// 221..254: グレースケール
	/// 255: 透過 (データなし)
	/// </summary>
	public static Bitmap CreateBitmap(int seed = 12345)
	{
		var bmp = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);

		var palette = bmp.Palette;
		for (var i = 0; i < 256; i++)
		{
			if (i <= 220)
			{
				// Hue 240(青) → 0(赤)。彩度・明度は最大にして変換器の有効色判定 (s>0.75, v>0.1) を通す
				var hue = 240.0 - (i / 220.0 * 240.0);
				palette.Entries[i] = HsvToColor(hue, 1.0, 1.0);
			}
			else if (i < 255)
			{
				var g = (byte)((i - 221) * 7);
				palette.Entries[i] = Color.FromArgb(255, g, g, g);
			}
			else
			{
				palette.Entries[i] = Color.FromArgb(0, 0, 0, 0);
			}
		}
		bmp.Palette = palette; // 反映には再代入が必要

		var rng = new System.Random(seed);
		var data = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
		unsafe
		{
			var scan0 = (byte*)data.Scan0;
			for (var y = 0; y < Height; y++)
				for (var x = 0; x < Width; x++)
					// 9 割は有効色、1 割は透過にして alpha 分岐も計測対象にする
					scan0[(y * data.Stride) + x] = (byte)(rng.Next(100) < 90 ? rng.Next(0, 221) : 255);
		}
		bmp.UnlockBits(data);
		return bmp;
	}

	/// <summary>
	/// 上記 Bitmap を GIF にエンコードして SKBitmap にデコードする (同一画像から両ライブラリ用を用意する)。
	/// </summary>
	public static SKBitmap CreateSkBitmap(Bitmap source)
	{
		using var ms = new MemoryStream();
		source.Save(ms, ImageFormat.Gif);
		return SKBitmap.Decode(ms.ToArray());
	}

	/// <summary>
	/// 画像範囲内に散らばる観測点を生成する。約 3% を休止、約 2% を座標なしにして分岐も計測する。
	/// </summary>
	public static ObservationPoint[] CreatePoints(int count, int seed = 67890)
	{
		var rng = new System.Random(seed);
		var points = new ObservationPoint[count];
		for (var i = 0; i < count; i++)
		{
			var op = new ObservationPoint
			{
				Code = i.ToString(),
				Name = "point" + i,
				Region = "region",
				Location = new Location(35f, 135f),
				Point = new Point2(rng.Next(0, Width), rng.Next(0, Height)),
				IsSuspended = rng.Next(100) < 3,
			};
			if (rng.Next(100) < 2)
				op.Point = null;
			points[i] = op;
		}
		return points;
	}

	private static Color HsvToColor(double h, double s, double v)
	{
		h = (h % 360 + 360) % 360;
		var c = v * s;
		var x = c * (1 - System.Math.Abs((h / 60 % 2) - 1));
		var m = v - c;
		double r, g, b;
		if (h < 60) { r = c; g = x; b = 0; }
		else if (h < 120) { r = x; g = c; b = 0; }
		else if (h < 180) { r = 0; g = c; b = x; }
		else if (h < 240) { r = 0; g = x; b = c; }
		else if (h < 300) { r = x; g = 0; b = c; }
		else { r = c; g = 0; b = x; }
		return Color.FromArgb(255, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
	}
}
