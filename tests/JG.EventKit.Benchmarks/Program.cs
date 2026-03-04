using BenchmarkDotNet.Running;
using JG.EventKit.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(PublishBenchmarks).Assembly)
    .Run(args);
