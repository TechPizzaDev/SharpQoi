using System.Runtime.CompilerServices;

namespace PizzaQoi
{
    public struct PqoiRgba
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public bool Equals(PqoiRgba other)
        {
            return Unsafe.As<PqoiRgba, int>(ref this) == Unsafe.As<PqoiRgba, int>(ref other);
        }
    }
}
