using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Parameters;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.Runtime.Utilities;
using SharpFossilBenchmarks.Benchmarks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SharpFossilBenchmarks
{

    class Program
    {
        
        public static void Main(string[] args)
        {
#if DEBUG
            var a = new RoundtripOldVsNew() { Bytes = 1024*64 };
            a.Setup();
            a.Old();
            a.New();
#endif

            BenchmarkRunner.Run(typeof(RoundtripFormats));

            Console.ReadKey();
        }

    }

   


}
