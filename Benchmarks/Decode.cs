﻿using System.IO;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using SharpQoi;

namespace Benchmarks
{
    public unsafe class Decode
    {
        public byte[][] images;

        [GlobalSetup]
        public void Setup()
        {
            images = new byte[][]
            {
                File.ReadAllBytes("0.qoi")
            };
        }

        [Benchmark]
        public void RGB()
        {
            byte[] qoi = images[0];

            fixed (byte* qoiData = qoi)
            {
                void* raw = Qoi.qoi_decode(qoiData, (uint)qoi.Length, out var desc, 0);
                NativeMemory.Free(raw);
            }
        }
    }
}