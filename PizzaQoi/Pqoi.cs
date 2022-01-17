using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PizzaQoi
{
    public static unsafe class Pqoi
    {
        public const int QOI_SRGB = 0;
        public const int QOI_LINEAR = 1;

        /* Encode raw RGB or RGBA pixels into a QOI image and write it to the file
        system. The qoi_desc struct must be filled with the image width, height,
        number of channels (3 = RGB, 4 = RGBA) and the colorspace.
        The function returns 0 on failure (invalid parameters, or fopen or malloc
        failed) or the number of bytes written on success. */
        //int qoi_write(string filename, void* data, qoi_desc desc)
        //{
        //}

        /* Read and decode a QOI image from the file system. If channels is 0, the
        number of channels from the file header is used. If channels is 3 or 4 the
        output format will be forced into this number of channels.
        The function either returns null on failure (invalid data, or malloc or fopen
        failed) or a pointer to the decoded pixels. On success, the qoi_desc struct
        will be filled with the description from the file header.
        The returned pixel data should be free()d after use. */
        //void* qoi_read(string filename, out qoi_desc desc, int channels)
        //{
        //}

        /* -----------------------------------------------------------------------------
        Implementation */

        static void QOI_ZEROARR<T>(T* a, uint count)
            where T : unmanaged
        {
            Unsafe.InitBlockUnaligned((byte*)a, 0, (uint)Unsafe.SizeOf<T>() * count);
        }

        public const int QOI_OP_INDEX = 0x00; // 00xxxxxx
        public const int QOI_OP_DIFF = 0x40;  // 01xxxxxx
        public const int QOI_OP_LUMA = 0x80;  // 10xxxxxx
        public const int QOI_OP_RUN = 0xc0;   // 11xxxxxx
        public const int QOI_OP_RGB = 0xfe;   // 11111110
        public const int QOI_OP_RGBA = 0xff;  // 11111111

        public const int QOI_MASK_2 = 0xc0; // 11000000

        public static int QOI_COLOR_HASH(PqoiRgba rgba)
        {
            return rgba.R * 3 + rgba.G * 5 + rgba.B * 7 + rgba.A * 11;
        }

        public const uint QOI_MAGIC =
            ((uint)'q') << 24 |
            ((uint)'o') << 16 |
            ((uint)'i') << 8 |
            'f';

        public const int QOI_HEADER_SIZE = 14;

        /* 2GB is the max file size that this implementation can safely handle. We guard
        against anything larger than that, assuming the worst case with 5 bytes per
        pixel, rounded down to a nice clean value. 400 million pixels ought to be
        enough for anybody. */
        public const uint QOI_PIXELS_MAX = 400000000;

        static ReadOnlySpan<byte> Padding => new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };

        static void Write_32(byte* bytes, uint* p, uint v)
        {
            bytes[(*p)++] = (byte)((0xff000000 & v) >> 24);
            bytes[(*p)++] = (byte)((0x00ff0000 & v) >> 16);
            bytes[(*p)++] = (byte)((0x0000ff00 & v) >> 8);
            bytes[(*p)++] = (byte)(0x000000ff & v);
        }

        static uint Read_32(byte* bytes, uint* p)
        {
            uint a = bytes[(*p)++];
            uint b = bytes[(*p)++];
            uint c = bytes[(*p)++];
            uint d = bytes[(*p)++];
            return a << 24 | b << 16 | c << 8 | d;
        }

        /// <summary>
        /// Encode pixels into a QOI image in memory.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="desc"></param>
        /// <param name="out_len">
        /// On success, set to the size in bytes of the encoded data.
        /// </param>
        /// <returns>
        /// On success, returns a pointer to the encoded data.
        /// On failure (invalid parameters), returns null.
        /// </returns>
        /// <remarks>
        /// The returned QOI data needs to be freed with <see cref="Free"/>.
        /// </remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        public static void* Encode(void* data, PqoiDesc desc, out uint out_len)
        {
            PqoiRgba* index = stackalloc PqoiRgba[64];

            if (data == null ||
                desc.width == 0 || desc.height == 0 ||
                desc.channels < 3 || desc.channels > 4 ||
                desc.colorspace > 1 ||
                desc.height >= QOI_PIXELS_MAX / desc.width)
            {
                out_len = 0;
                return null;
            }

            uint max_size =
                desc.width * desc.height * (uint)(desc.channels + 1) +
                QOI_HEADER_SIZE + (uint)Padding.Length;

            uint p = 0;
            byte* bytes = (byte*)Alloc(max_size);

            Write_32(bytes, &p, QOI_MAGIC);
            Write_32(bytes, &p, desc.width);
            Write_32(bytes, &p, desc.height);
            bytes[p++] = desc.channels;
            bytes[p++] = desc.colorspace;

            byte* pixels = (byte*)data;

            QOI_ZEROARR(index, 64);

            uint run = 0;
            PqoiRgba px_prev;
            px_prev.R = 0;
            px_prev.G = 0;
            px_prev.B = 0;
            px_prev.A = 255;
            PqoiRgba px = px_prev;

            uint px_len = desc.width * desc.height * (uint)desc.channels;
            uint px_end = px_len - desc.channels;
            uint channels = desc.channels;

            for (uint px_pos = 0; px_pos < px_len; px_pos += channels)
            {
                if (channels == 4)
                {
                    px = *(PqoiRgba*)(pixels + px_pos);
                }
                else
                {
                    px.R = pixels[px_pos + 0];
                    px.G = pixels[px_pos + 1];
                    px.B = pixels[px_pos + 2];
                }

                if (px.Equals(px_prev))
                {
                    run++;
                    if (run == 62 || px_pos == px_end)
                    {
                        bytes[p++] = (byte)(QOI_OP_RUN | (run - 1));
                        run = 0;
                    }
                }
                else
                {
                    if (run > 0)
                    {
                        bytes[p++] = (byte)(QOI_OP_RUN | (run - 1));
                        run = 0;
                    }

                    uint index_pos = (uint)QOI_COLOR_HASH(px) % 64;

                    if (index[index_pos].Equals(px))
                    {
                        bytes[p++] = (byte)(QOI_OP_INDEX | index_pos);
                    }
                    else
                    {
                        index[index_pos] = px;

                        if (px.A == px_prev.A)
                        {
                            int vr = px.R - px_prev.R;
                            int vg = px.G - px_prev.G;
                            int vb = px.B - px_prev.B;

                            int vg_r = vr - vg;
                            int vg_b = vb - vg;

                            if (vr > -3 && vr < 2 &&
                                vg > -3 && vg < 2 &&
                                vb > -3 && vb < 2
                            )
                            {
                                bytes[p++] = (byte)(QOI_OP_DIFF | (vr + 2) << 4 | (vg + 2) << 2 | (vb + 2));
                            }
                            else if (
                                vg_r > -9 && vg_r < 8 &&
                                vg > -33 && vg < 32 &&
                                vg_b > -9 && vg_b < 8)
                            {
                                bytes[p++] = (byte)(QOI_OP_LUMA | (vg + 32));
                                bytes[p++] = (byte)((vg_r + 8) << 4 | (vg_b + 8));
                            }
                            else
                            {
                                bytes[p++] = QOI_OP_RGB;
                                bytes[p++] = px.R;
                                bytes[p++] = px.G;
                                bytes[p++] = px.B;
                            }
                        }
                        else
                        {
                            bytes[p++] = QOI_OP_RGBA;
                            bytes[p++] = px.R;
                            bytes[p++] = px.G;
                            bytes[p++] = px.B;
                            bytes[p++] = px.A;
                        }
                    }
                }
                px_prev = px;
            }

            ReadOnlySpan<byte> padding = Padding;
            for (int i = 0; i < padding.Length; i++)
            {
                bytes[p++] = padding[i];
            }

            out_len = p;
            return bytes;
        }

        /// <summary>
        /// Decode a QOI image from memory.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="size"></param>
        /// <param name="desc">Filled with the description from the file header.</param>
        /// <param name="channels">Target amount of channels. Can be zero.</param>
        /// <returns>
        /// On success, returns a pointer to the decoded pixels.
        /// On failure (invalid parameters), returns null.
        /// </returns>
        /// <remarks>
        /// The returned pixel data needs to be freed with <see cref="Free"/>.
        /// </remarks>
        /// <exception cref="OutOfMemoryException"></exception>
        public static void* Decode(void* data, uint size, out PqoiDesc desc, uint channels)
        {
            PqoiRgba* index = stackalloc PqoiRgba[64];

            if (data == null ||
                (channels != 0 && channels != 3 && channels != 4) ||
                size < QOI_HEADER_SIZE + Padding.Length)
            {
                desc = default;
                return null;
            }

            uint p = 0;
            byte* bytes = (byte*)data;

            uint header_magic = Read_32(bytes, &p);
            desc.width = Read_32(bytes, &p);
            desc.height = Read_32(bytes, &p);
            desc.channels = bytes[p++];
            desc.colorspace = bytes[p++];

            if (desc.width == 0 || desc.height == 0 ||
                desc.channels < 3 || desc.channels > 4 ||
                desc.colorspace > 1 ||
                header_magic != QOI_MAGIC ||
                desc.height >= QOI_PIXELS_MAX / desc.width)
            {
                return null;
            }

            if (channels == 0)
            {
                channels = desc.channels;
            }

            uint px_len = desc.width * desc.height * channels;
            byte* pixels = (byte*)Alloc(px_len);

            QOI_ZEROARR(index, 64);

            PqoiRgba px;
            px.R = 0;
            px.G = 0;
            px.B = 0;
            px.A = 255;

            uint run = 0;
            uint chunks_len = size - (uint)Padding.Length;

            for (uint px_pos = 0; px_pos < px_len; px_pos += channels)
            {
                if (run > 0)
                {
                    run--;
                }
                else if (p < chunks_len)
                {
                    uint b1 = bytes[p++];

                    if (b1 == QOI_OP_RGB)
                    {
                        px.R = bytes[p++];
                        px.G = bytes[p++];
                        px.B = bytes[p++];
                    }
                    else if (b1 == QOI_OP_RGBA)
                    {
                        px.R = bytes[p++];
                        px.G = bytes[p++];
                        px.B = bytes[p++];
                        px.A = bytes[p++];
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_INDEX)
                    {
                        px = index[b1];
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_DIFF)
                    {
                        px.R += (byte)(((b1 >> 4) & 0x03) - 2);
                        px.G += (byte)(((b1 >> 2) & 0x03) - 2);
                        px.B += (byte)((b1 & 0x03) - 2);
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_LUMA)
                    {
                        int b2 = bytes[p++];
                        int vg = (int)((b1 & 0x3f) - 32);
                        px.R += (byte)(vg - 8 + ((b2 >> 4) & 0x0f));
                        px.G += (byte)vg;
                        px.B += (byte)(vg - 8 + (b2 & 0x0f));
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_RUN)
                    {
                        run = b1 & 0x3f;
                    }

                    index[(uint)QOI_COLOR_HASH(px) % 64] = px;
                }

                if (channels == 4)
                {
                    *(PqoiRgba*)(pixels + px_pos) = px;
                }
                else
                {
                    pixels[px_pos + 0] = px.R;
                    pixels[px_pos + 1] = px.G;
                    pixels[px_pos + 2] = px.B;
                }
            }

            return pixels;
        }

        public static void* Alloc(nuint byteCount)
        {
            return NativeMemory.Alloc(byteCount);
        }

        public static void* ReAlloc(void* data, nuint byteCount)
        {
            return NativeMemory.Realloc(data, byteCount);
        }

        public static void Free(void* data)
        {
            NativeMemory.Free(data);
        }
    }
}
