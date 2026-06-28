using BenchmarkDotNet.Running;
using Benchmark;

// 画像からの色取得 (ParseScaleFromImage) のベンチマーク。
// 実行: dotnet run --project src/Benchmark -c Release
// 検証: dotnet run --project src/Benchmark -c Release -- verify
if (args.Length > 0 && args[0] == "verify")
	return Verification.Run();

BenchmarkRunner.Run<ColorParsingBenchmark>();
return 0;
