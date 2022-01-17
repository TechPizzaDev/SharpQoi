using System.Runtime.CompilerServices;

namespace SharpQoi
{
    public struct QoiRgba
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public bool Equals(QoiRgba other)
        {
            return Unsafe.As<QoiRgba, int>(ref this) == Unsafe.As<QoiRgba, int>(ref other);
        }
    }
}
