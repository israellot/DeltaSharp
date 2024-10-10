using BenchmarkDotNet.Running;
using DeltaSharp;
using SharpFossilBenchmarks.Benchmarks;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SharpFossilBenchmarks;

class Program
{
    
    public static async Task Main(string[] args)
    {
                
        BenchmarkRunner.Run(typeof(RoundtripFormats));

        var k = Console.ReadKey();
           
    }      

}
