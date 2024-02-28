using System;

namespace MercuryMapper.Utils;

public class MathExtensions
{
    public static int Modulo(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }

    public static double DegToRad(double degrees) => degrees * Math.PI / 180.0;
}