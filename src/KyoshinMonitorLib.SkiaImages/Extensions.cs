using KyoshinMonitorLib.UrlGenerator;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KyoshinMonitorLib.SkiaImages
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
			if (imageResult.Data is not byte[] data)
				return new(imageResult.StatusCode, null);

			using var bitmap = SKBitmap.Decode(data);
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
			if (imageResult.Data is not byte[] data)
				return new(imageResult.StatusCode, null);

			using var bitmap = SKBitmap.Decode(data);
			return new(imageResult.StatusCode, points.ParseScaleFromImage(bitmap));
		}

		/// <summary>
		/// 与えられた画像から観測点情報を使用しスケールを取得します。
		/// </summary>
		/// <param name="points">使用する観測点情報の配列</param>
		/// <param name="bitmap">参照する画像</param>
		/// <returns>震度情報が追加された観測点情報の配列</returns>
		public static IEnumerable<ImageAnalysisResult> ParseScaleFromImage(this IEnumerable<ImageAnalysisResult> points, SKBitmap bitmap)
		{
			var width = bitmap.Width;
			var height = bitmap.Height;
			var rowBytes = bitmap.RowBytes;
			var bytesPerPixel = bitmap.BytesPerPixel;
			var colorType = bitmap.ColorType;
			// GetPixel は呼び出しごとにネイティブ呼び出しが入るため、ピクセルバッファを直接読む。
			// 一般的にデコード結果は Bgra8888 / Rgba8888 (4byte/px) なので、その場合のみ高速パスを使う。
			var fastPath = bytesPerPixel == 4 && (colorType == SKColorType.Bgra8888 || colorType == SKColorType.Rgba8888);
			var pixels = fastPath ? bitmap.GetPixelSpan() : default;

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

				SKColor color;
				if (fastPath)
				{
					var offset = (rowBytes * p.Y) + (p.X * bytesPerPixel);
					var b0 = pixels[offset];
					var b1 = pixels[offset + 1];
					var b2 = pixels[offset + 2];
					var a = pixels[offset + 3];
					// Bgra8888 は B,G,R,A の順。Rgba8888 は R,G,B,A の順。
					color = colorType == SKColorType.Bgra8888
						? new SKColor(b2, b1, b0, a)
						: new SKColor(b0, b1, b2, a);
				}
				else
				{
					color = bitmap.GetPixel(p.X, p.Y);
				}
				point.Color = color;
				point.AnalysisResult = color.Alpha == 255 ? ColorConverter.ConvertToScaleAtPolynomialInterpolation(color) : (double?)null;
			}
			return points;
		}
	}
}
