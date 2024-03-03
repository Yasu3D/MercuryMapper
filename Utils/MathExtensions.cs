using System;

namespace MercuryMapper.Utils;

public static class MathExtensions
{
    public static int Modulo(int x, int m)
    {
        int r = x % m;
        return r < 0 ? r + m : r;
    }
    
    public static float Modulo(float x, int m)
    {
        float r = x % m;
        return r < 0 ? r + m : r;
    }

    public static double DegToRad(double degrees) => degrees * Math.PI / 180.0;

    public static float Lerp(float a, float b, float t)
    {
        return a * (1 - t) + b * t;
    }

    public static float InverseLerp(float v, float a, float b)
    {
        return (v - a) / (b - a);
    }

    public static float Perspective(float x)
    {
        // Huge thanks to CG505 for figuring out the perspective math:
        // https://www.desmos.com/calculator/9a0srmgktj
        return 3.325f * x / (13.825f - 10.5f * x);
    }

    public static float InversePerspective(float x)
    {
        // Ima be real I used a lil chatGPT for this one
        // I need to refresh my math skills... my math professor wouldn't be proud.
        return 13.825f * x / (10.5f * x + 3.325f);
    }

    public static float GetTheta(float x, float y)
    {
        float t = MathF.Atan2(y, x) * 180.0f / MathF.PI;
        return t < 0 ? t + 360 : t;
    }

    public static int GetThetaNotePosition(float x, float y)
    {
        return (int)(360 - GetTheta(x, y)) / 6;
    }
}