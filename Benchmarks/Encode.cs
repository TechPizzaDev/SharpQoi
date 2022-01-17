using System.IO;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using SharpQoi;

namespace Benchmarks
{
    public unsafe class Encode
    {
        public (byte[], qoi_desc)[] images;

        private (byte[], qoi_desc) LoadImage(string fileName)
        {
            using var reader = new BinaryReader(File.OpenRead(fileName));

            uint width = reader.ReadUInt32();
            uint height = reader.ReadUInt32();
            byte channels = reader.ReadByte();
            byte colorspace = reader.ReadByte();
            byte[] pixels = reader.ReadBytes((int)(width * height * channels));

            return (pixels, new qoi_desc()
            {
                width = width,
                height = height,
                channels = channels,
                colorspace = colorspace
            });
        }

        [GlobalSetup]
        public void Setup()
        {
            images = new (byte[], qoi_desc)[]
            {
                LoadImage("0.raw")
            };
        }

        [Benchmark]
        public void RGB()
        {
            (byte[] pixels, qoi_desc desc) = images[0];
            fixed (byte* data = pixels)
            {
                void* result = Qoi.qoi_encode(data, desc, out uint length);
                NativeMemory.Free(result);
            }
        }
    }
}
