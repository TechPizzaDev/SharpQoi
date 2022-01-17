using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharpQoi
{
    public static unsafe class Qoi
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

        /* Encode raw RGB or RGBA pixels into a QOI image in memory.
        The function either returns null on failure (invalid parameters or malloc
        failed) or a pointer to the encoded data on success. On success the out_len
        is set to the size in bytes of the encoded data.
        The returned qoi data should be free()d after use. */
        //void* qoi_encode(void* data, qoi_desc desc, out int out_len)
        //{
        //}


        /* Decode a QOI image from memory.
        The function either returns null on failure (invalid parameters or malloc
        failed) or a pointer to the decoded pixels. On success, the qoi_desc struct
        is filled with the description from the file header.
        The returned pixel data should be free()d after use. */
        //void* qoi_decode(void* data, int size, out qoi_desc desc, int channels)
        //{
        //}

        /* -----------------------------------------------------------------------------
        Implementation */

        public static void QOI_ZEROARR<T>(T* a)
            where T : unmanaged
        {
            Unsafe.InitBlockUnaligned((byte*)a, 0, (uint)Unsafe.SizeOf<T>());
        }

        public const int QOI_OP_INDEX = 0x00; // 00xxxxxx
        public const int QOI_OP_DIFF = 0x40;  // 01xxxxxx
        public const int QOI_OP_LUMA = 0x80;  // 10xxxxxx
        public const int QOI_OP_RUN = 0xc0;   // 11xxxxxx
        public const int QOI_OP_RGB = 0xfe;   // 11111110
        public const int QOI_OP_RGBA = 0xff;  // 11111111

        public const int QOI_MASK_2 = 0xc0;// 11000000

        public static int QOI_COLOR_HASH(qoi_rgba_t rgba)
        {
            return (rgba.r * 3 + rgba.g * 5 + rgba.b * 7 + rgba.a * 11);
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

        public struct qoi_rgba_t
        {
            public byte r, g, b, a;

            public bool Equals(qoi_rgba_t other)
            {
                return Unsafe.As<qoi_rgba_t, int>(ref this) == Unsafe.As<qoi_rgba_t, int>(ref other);
            }
        }

        static ReadOnlySpan<byte> qoi_padding => new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };

        static void qoi_write_32(byte* bytes, uint* p, uint v)
        {
            bytes[(*p)++] = (byte)((0xff000000 & v) >> 24);
            bytes[(*p)++] = (byte)((0x00ff0000 & v) >> 16);
            bytes[(*p)++] = (byte)((0x0000ff00 & v) >> 8);
            bytes[(*p)++] = (byte)(0x000000ff & v);
        }

        static uint qoi_read_32(byte* bytes, int* p)
        {
            uint a = bytes[(*p)++];
            uint b = bytes[(*p)++];
            uint c = bytes[(*p)++];
            uint d = bytes[(*p)++];
            return a << 24 | b << 16 | c << 8 | d;
        }

        public static void* qoi_encode(void* data, qoi_desc desc, out uint out_len)
        {
            uint max_size, p, run;
            uint px_len, px_end, px_pos, channels;
            byte* bytes;
            byte* pixels; // const
            qoi_rgba_t* index = stackalloc qoi_rgba_t[64];
            qoi_rgba_t px, px_prev;

            if (data == null ||
                desc.width == 0 || desc.height == 0 ||
                desc.channels < 3 || desc.channels > 4 ||
                desc.colorspace > 1 ||
                desc.height >= QOI_PIXELS_MAX / desc.width)
            {
                out_len = 0;
                return null;
            }

            max_size =
                desc.width * desc.height * (uint)(desc.channels + 1) +
                QOI_HEADER_SIZE + (uint)qoi_padding.Length;

            p = 0;
            bytes = (byte*)NativeMemory.Alloc(max_size);

            qoi_write_32(bytes, &p, QOI_MAGIC);
            qoi_write_32(bytes, &p, desc.width);
            qoi_write_32(bytes, &p, desc.height);
            bytes[p++] = desc.channels;
            bytes[p++] = desc.colorspace;


            pixels = (byte*)data;

            QOI_ZEROARR(index);

            run = 0;
            px_prev.r = 0;
            px_prev.g = 0;
            px_prev.b = 0;
            px_prev.a = 255;
            px = px_prev;

            px_len = desc.width * desc.height * (uint)desc.channels;
            px_end = px_len - desc.channels;
            channels = desc.channels;

            for (px_pos = 0; px_pos < px_len; px_pos += channels)
            {
                if (channels == 4)
                {
                    px = *(qoi_rgba_t*)(pixels + px_pos);
                }
                else
                {
                    px.r = pixels[px_pos + 0];
                    px.g = pixels[px_pos + 1];
                    px.b = pixels[px_pos + 2];
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
                    int index_pos;

                    if (run > 0)
                    {
                        bytes[p++] = (byte)(QOI_OP_RUN | (run - 1));
                        run = 0;
                    }

                    index_pos = QOI_COLOR_HASH(px) % 64;

                    if (index[index_pos].Equals(px))
                    {
                        bytes[p++] = (byte)(QOI_OP_INDEX | index_pos);
                    }
                    else
                    {
                        index[index_pos] = px;

                        if (px.a == px_prev.a)
                        {
                            int vr = px.r - px_prev.r;
                            int vg = px.g - px_prev.g;
                            int vb = px.b - px_prev.b;

                            int vg_r = vr - vg;
                            int vg_b = vb - vg;

                            if (
                                vr > -3 && vr < 2 &&
                                vg > -3 && vg < 2 &&
                                vb > -3 && vb < 2
                            )
                            {
                                bytes[p++] = (byte)(QOI_OP_DIFF | (vr + 2) << 4 | (vg + 2) << 2 | (vb + 2));
                            }
                            else if (
                                vg_r > -9 && vg_r < 8 &&
                                vg > -33 && vg < 32 &&
                                vg_b > -9 && vg_b < 8
                            )
                            {
                                bytes[p++] = (byte)(QOI_OP_LUMA | (vg + 32));
                                bytes[p++] = (byte)((vg_r + 8) << 4 | (vg_b + 8));
                            }
                            else
                            {
                                bytes[p++] = QOI_OP_RGB;
                                bytes[p++] = px.r;
                                bytes[p++] = px.g;
                                bytes[p++] = px.b;
                            }
                        }
                        else
                        {
                            bytes[p++] = QOI_OP_RGBA;
                            bytes[p++] = px.r;
                            bytes[p++] = px.g;
                            bytes[p++] = px.b;
                            bytes[p++] = px.a;
                        }
                    }
                }
                px_prev = px;
            }

            ReadOnlySpan<byte> padding = qoi_padding;
            for (int i = 0; i < padding.Length; i++)
            {
                bytes[p++] = padding[i];
            }

            out_len = p;
            return bytes;
        }

        public static void* qoi_decode(void* data, uint size, out qoi_desc desc, uint channels)
        {
            byte* bytes;
            uint header_magic;
            byte* pixels;
            qoi_rgba_t* index = stackalloc qoi_rgba_t[64];
            qoi_rgba_t px;
            uint px_len, chunks_len, px_pos;
            int p = 0, run = 0;

            if (data == null ||
                (channels != 0 && channels != 3 && channels != 4) ||
                size < QOI_HEADER_SIZE + qoi_padding.Length)
            {
                desc = default;
                return null;
            }

            bytes = (byte*)data;

            header_magic = qoi_read_32(bytes, &p);
            desc.width = qoi_read_32(bytes, &p);
            desc.height = qoi_read_32(bytes, &p);
            desc.channels = bytes[p++];
            desc.colorspace = bytes[p++];

            if (
                desc.width == 0 || desc.height == 0 ||
                desc.channels < 3 || desc.channels > 4 ||
                desc.colorspace > 1 ||
                header_magic != QOI_MAGIC ||
                desc.height >= QOI_PIXELS_MAX / desc.width
            )
            {
                return null;
            }

            if (channels == 0)
            {
                channels = desc.channels;
            }

            px_len = desc.width * desc.height * channels;
            pixels = (byte*)NativeMemory.Alloc(px_len);

            QOI_ZEROARR(index);
            px.r = 0;
            px.g = 0;
            px.b = 0;
            px.a = 255;

            chunks_len = size - (uint)qoi_padding.Length;
            for (px_pos = 0; px_pos < px_len; px_pos += channels)
            {
                if (run > 0)
                {
                    run--;
                }
                else if (p < chunks_len)
                {
                    int b1 = bytes[p++];

                    if (b1 == QOI_OP_RGB)
                    {
                        px.r = bytes[p++];
                        px.g = bytes[p++];
                        px.b = bytes[p++];
                    }
                    else if (b1 == QOI_OP_RGBA)
                    {
                        px.r = bytes[p++];
                        px.g = bytes[p++];
                        px.b = bytes[p++];
                        px.a = bytes[p++];
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_INDEX)
                    {
                        px = index[b1];
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_DIFF)
                    {
                        px.r += (byte)(((b1 >> 4) & 0x03) - 2);
                        px.g += (byte)(((b1 >> 2) & 0x03) - 2);
                        px.b += (byte)((b1 & 0x03) - 2);
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_LUMA)
                    {
                        int b2 = bytes[p++];
                        int vg = (b1 & 0x3f) - 32;
                        px.r += (byte)(vg - 8 + ((b2 >> 4) & 0x0f));
                        px.g += (byte)(vg);
                        px.b += (byte)(vg - 8 + (b2 & 0x0f));
                    }
                    else if ((b1 & QOI_MASK_2) == QOI_OP_RUN)
                    {
                        run = (b1 & 0x3f);
                    }

                    index[QOI_COLOR_HASH(px) % 64] = px;
                }

                if (channels == 4)
                {
                    *(qoi_rgba_t*)(pixels + px_pos) = px;
                }
                else
                {
                    pixels[px_pos + 0] = px.r;
                    pixels[px_pos + 1] = px.g;
                    pixels[px_pos + 2] = px.b;
                }
            }

            return pixels;
        }
    }
}
