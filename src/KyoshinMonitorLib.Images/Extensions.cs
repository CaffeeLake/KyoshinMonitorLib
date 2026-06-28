using KyoshinMonitorLib.UrlGenerator;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinMonitorLib.Images
{
	/// <summary>
	/// 拡張メソッドたち
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// 与えられた情報から強震モニタの画像を取得し、そこから観測点情報を使用しスケールを取得します。
		/// <para>asyncなのはStream取得部分のみなので注意してください。</para>
		/// </summary>
		/// <param name="webApi">WebApiインスタンス</param>
		/// <param name="points">使用する観測点情報の配列</param>
		/// <param name="datetime">参照する日付</param>
		/// <param name="dataType">取得する情報の種類</param>
		/// <param name="isBehole">地中の情報を取得するかどうか</param>
		/// <returns>震度情報が追加された観測点情報の配列 取得に失敗した場合null</returns>
		public static async Task<ApiResult<IEnumerable<ImageAnalysisResult>?>> ParseScaleFromParameterAsync(this WebApi webApi, IEnumerable<ObservationPoint> points, DateTime datetime, RealtimeDataType dataType = RealtimeDataType.Shindo, bool isBehole = false)
			=> await webApi.ParseScaleFromParameterAsync(points.Select(p => new ImageAnalysisResult(p)).ToArray(), datetime, dataType, isBehole);

		/// <summary>
		/// 与えられた情報から強震モニタの画像を取得し、そこから観測点情報を使用しスケールを取得します。
		/// <para>asyncなのはStream取得部分のみなので注意してください。</para>
		/// </summary>
		/// <param name="webApi">WebApiインスタンス</param>
		/// <param name="points">使用する観測点情報の配列</param>
		/// <param name="datetime">参照する日付</param>
		/// <param name="dataType">取得する情報の種類</param>
		/// <param name="isBehole">地中の情報を取得するかどうか</param>
		/// <returns>震度情報が追加された観測点情報の配列 取得に失敗した場合null</returns>
		public static async Task<ApiResult<IEnumerable<ImageAnalysisResult>?>> ParseScaleFromParameterAsync(this WebApi webApi, IEnumerable<ImageAnalysisResult> points, DateTime datetime, RealtimeDataType dataType = RealtimeDataType.Shindo, bool isBehole = false)
		{
			var imageResult = await webApi.GetRealtimeImageData(datetime, dataType, isBehole);
			if (imageResult.Data == null)
				return new(imageResult.StatusCode, null);

			using var stream = new MemoryStream(imageResult.Data);
			using var bitmap = new Bitmap(stream);
			return new(imageResult.StatusCode, points.ParseScaleFromImage(bitmap));
		}

		/// <summary>
		/// 与えられた情報から強震モニタの画像を取得し、そこから観測点情報を使用しスケールを取得します。
		/// <para>asyncなのはStream取得部分のみなので注意してください。</para>
		/// </summary>
		/// <param name="webApi">WebApiインスタンス</param>
		/// <param name="points">使用する観測点情報の配列</param>
		/// <param name="datetime">参照する日付</param>
		/// <param name="dataType">取得する情報の種類</param>
		/// <param name="isBehole">地中の情報を取得するかどうか</param>
		/// <returns>震度情報が追加された観測点情報の配列 取得に失敗した場合null</returns>
		public static async Task<ApiResult<IEnumerable<ImageAnalysisResult>?>> ParseScaleFromParameterAsync(this LpgmWebApi webApi, IEnumerable<ObservationPoint> points, DateTime datetime, RealtimeDataType dataType = RealtimeDataType.Shindo, bool isBehole = false)
			=> await webApi.ParseScaleFromParameterAsync(points.Select(p => new ImageAnalysisResult(p)).ToArray(), datetime, dataType, isBehole);

		/// <summary>
		/// 与えられた情報から強震モニタの画像を取得し、そこから観測点情報を使用しスケールを取得します。
		/// <para>asyncなのはStream取得部分のみなので注意してください。</para>
		/// </summary>
		/// <param name="webApi">WebApiインスタンス</param>
		/// <param name="points">使用する観測点情報の配列</param>
		/// <param name="datetime">参照する日付</param>
		/// <param name="dataType">取得する情報の種類</param>
		/// <param name="isBehole">地中の情報を取得するかどうか</param>
		/// <returns>震度情報が追加された観測点情報の配列 取得に失敗した場合null</returns>
		public static async Task<ApiResult<IEnumerable<ImageAnalysisResult>?>> ParseScaleFromParameterAsync(this LpgmWebApi webApi, IEnumerable<ImageAnalysisResult> points, DateTime datetime, RealtimeDataType dataType = RealtimeDataType.Shindo, bool isBehole = false)
		{
			var imageResult = await webApi.GetRealtimeImageData(datetime, dataType, isBehole);
			if (imageResult.Data == null)
				return new(imageResult.StatusCode, null);

			using var stream = new MemoryStream(imageResult.Data);
			using var bitmap = new Bitmap(stream);
			return new(imageResult.StatusCode, points.ParseScaleFromImage(bitmap));
		}

		/// <summary>
		/// 与えられた画像から観測点情報を使用しスケールを取得します。
		/// </summary>
		/// <param name="points">使用する観測点情報の配列</param>
		/// <param name="bitmap">参照する画像</param>
		/// <returns>震度情報が追加された観測点情報の配列</returns>
		public static IEnumerable<ImageAnalysisResult> ParseScaleFromImage(this IEnumerable<ImageAnalysisResult> points, Bitmap bitmap)
		{
			var width = bitmap.Width;
			var height = bitmap.Height;
			var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
			try
			{
				// 8bpp インデックスカラーは行ごとに 4 バイト境界へパディングされるため Stride を使う (幅とは限らない)
				var stride = data.Stride;
				Span<byte> pixelData;
				unsafe
				{
					pixelData = new Span<byte>(data.Scan0.ToPointer(), stride * height);
				}

				// 強震モニタの画像は GIF (パレット最大 256 色)。色→スケール変換は重いので
				// 観測点ごとではなくパレットエントリごとに 1 度だけ計算してテーブル化する。
				// あわせて bitmap.Palette (取得のたびに GDI+ 呼び出し + 配列確保が発生) をループ外で 1 度だけ参照する。
				var entries = bitmap.Palette.Entries;
				var scaleTable = new double?[entries.Length];
				for (var i = 0; i < entries.Length; i++)
				{
					var entry = entries[i];
					scaleTable[i] = entry.A == 255 ? ColorConverter.ConvertToScaleAtPolynomialInterpolation(entry) : (double?)null;
				}

				foreach (var point in points)
				{
					var op = point.ObservationPoint;
					if (op.Point is not Point2 p || op.IsSuspended)
					{
						point.AnalysisResult = null;
						continue;
					}
					// 画像範囲外の座標は無視する
					if ((uint)p.X >= (uint)width || (uint)p.Y >= (uint)height)
					{
						point.AnalysisResult = null;
						continue;
					}

					var index = pixelData[(stride * p.Y) + p.X];
					point.Color = entries[index];
					point.AnalysisResult = scaleTable[index];
				}
			}
			finally
			{
				bitmap.UnlockBits(data);
			}
			return points;
		}
	}
}
