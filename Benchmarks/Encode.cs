using System.IO;
using BenchmarkDotNet.Attributes;
using PizzaQoi;
using SharpQoi;

namespace Benchmarks
{
    public unsafe class Encode
    {
        public (byte[], QoiDesc)[] images;

        private (byte[], QoiDesc) LoadImage(string fileName)
        {
            using var reader = new BinaryReader(File.OpenRead(fileName));

            uint width = reader.ReadUInt32();
            uint height = reader.ReadUInt32();
            byte channels = reader.ReadByte();
            byte colorspace = reader.ReadByte();
            byte[] pixels = reader.ReadBytes((int)(width * height * channels));

            return (pixels, new QoiDesc()
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
            images = new (byte[], QoiDesc)[]
            {
                LoadImage("0.raw")
            };
        }

        [Benchmark]
        public void Qoi_RGB()
        {
            (byte[] pixels, QoiDesc desc) = images[0];
            fixed (byte* data = pixels)
            {
                void* result = Qoi.Encode(data, desc, out uint length);
                Qoi.Free(result);
            }
        }

        [Benchmark]
        public void Pqoi_RGB()
        {
            (byte[] pixels, QoiDesc desc) = images[0];
            fixed (byte* data = pixels)
            {
                void* result = Pqoi.Encode(data, new PqoiDesc()
                {
                    width = desc.width,
                    height = desc.height,
                    channels = desc.channels,
                    colorspace = desc.colorspace
                }, out uint length);
                Pqoi.Free(result);
            }
        }
    }
}
