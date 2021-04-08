namespace JPEG
{
    public struct Complex
    {
        public double Re;

        public double Im;

        public Complex(double re, double im = 0)
        {
            Re = re;
            Im = im;
        }

        public static explicit operator sbyte(Complex complex) => (sbyte)complex.Re;
        public static explicit operator Complex(sbyte real) => new Complex(real);
        
        public override int GetHashCode() => Re.GetHashCode() ^ Im.GetHashCode();

        public override bool Equals(object obj) => obj is Complex complex && this == complex;
        
        public static bool operator ==(Complex u, Complex v) => u.Re == v.Re && u.Im == v.Im;
        
        public static bool operator !=(Complex u, Complex v) => !(u == v);
    }
}