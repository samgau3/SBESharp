using BenchmarkDotNet.Running;
using SBESharp.Benchmarks;

// Pass-through args let you control the run, e.g.:
//   dotnet run -c Release -- --job short
//   dotnet run -c Release -- --filter *Decode*
BenchmarkSwitcher.FromTypes([typeof(CarBenchmarks)]).Run(args);
