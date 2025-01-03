using System;

namespace MercuryMapper.Utils;

public static class MathExtensions
{
    public static int Modulo(int x, int m)
    {
        if (m <= 0) return 0;
        
        int r = x % m;
        return r < 0 ? r + m : r;
    }
    
    public static float Modulo(float x, int m)
    {
        if (m <= 0) return 0;
        
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

    public static float ShortLerp(bool shortPath, int a, int b, float t)
    {
        if (shortPath)
        {
            if (a > b) a -= 60;
            else b -= 60;
        }

        return Lerp(a, b, t);
    }

    public static float RoundToInterval(float x, float interval)
    {
        return float.Round(x / interval) * interval;
    }
    
    public static float Perspective(float x, bool clamp = false)
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

    public static int GreatestCommonDivisor(int x, int y)
    {
        if (x < 0 || y < 0) return 1;
        
        while (x != 0 && y != 0)
        {
            if (x > y) x %= y;
            else y %= x;
        }
        return x | y;
    }
    
    public static bool GreaterAlmostEqual(float input, float comparison, float threshold = 0.001f)
    {
        bool greater = input > comparison;
        bool almostEqual = float.Abs(input - comparison) < threshold;

        return greater || almostEqual;
    }

    public static bool LessAlmostEqual(float input, float comparison)
    {
        if (input < comparison) return true;
        return float.Abs(input - comparison) < 0.001f;
    }

    public enum HoldEaseType
    {
        Linear = 0,
        LinearPlusIn = 1,
        LinearPlusOut = 2,
        SmoothIn = 3,
        SmoothOut = 4,
        SharpIn = 5,
        SharpOut = 6,
        SineIn = 7,
        SineOut = 8,
        QuadIn = 9,
        QuadOut = 10,
    }

    public static float HoldBakeEase(float x, HoldEaseType type)
    {
        const float a = 0.44f;
        
        return type switch
        {
            HoldEaseType.Linear => x,
            HoldEaseType.LinearPlusIn => -0.575f * (x * x) + 1.575f * x,
            HoldEaseType.LinearPlusOut => 1.0f / 46.0f * (63 - float.Sqrt(3969 - 3680 * x)),
            HoldEaseType.SmoothIn => float.Sin(x * float.Pi * a) / float.Sin(float.Pi * a),
            HoldEaseType.SmoothOut => float.Asin(x * float.Sin(float.Pi * a)) / (float.Pi * a),
            HoldEaseType.SharpIn => 5f * x / (4f * x + 1),
            HoldEaseType.SharpOut => -(x / (4f * x - 5f)),
            HoldEaseType.SineIn => float.Sin(x * float.Pi * 0.5f),
            HoldEaseType.SineOut => 1 - float.Cos(x * float.Pi * 0.5f),
            HoldEaseType.QuadIn => 1 - (1 - x) * (1 - x),
            HoldEaseType.QuadOut => x * x,
            _ => x,
        };
    }

    public static bool IsFullyOverlapping(int startLeftEdge, int startRightEdge, int endLeftEdge, int endRightEdge)
    {
        // Start and End are identical
        if (startLeftEdge == endLeftEdge && startRightEdge == endRightEdge) return true;
        
        // Size 60 start
        if (startLeftEdge == startRightEdge)
        {
            startRightEdge -= 1;
        }
        
        // Size 60 end
        if (endLeftEdge == endRightEdge)
        {
            endRightEdge -= 1;
        }
        
        // Start overflows - End does not overflow
        if (startRightEdge >= 60 && endRightEdge < 60 && int.Abs(startRightEdge - endRightEdge) >= 60)
        {
            endLeftEdge += 60;
        }
        
        // End overflows - Start does not overflow
        else if (endRightEdge >= 60 && startRightEdge < 60 && int.Abs(startRightEdge - endRightEdge) >= 60)
        {
            startLeftEdge += 60;
        }
        
        bool caseA = startLeftEdge >= endLeftEdge && startRightEdge <= endRightEdge; // start smaller than end
        bool caseB = startLeftEdge <= endLeftEdge && startRightEdge >= endRightEdge; // start bigger than end
        
        return caseA || caseB;
    }

    public static bool IsPartiallyOverlapping(int startLeftEdge, int startRightEdge, int endLeftEdge, int endRightEdge)
    {
        startLeftEdge %= 60;
        startRightEdge %= 60;
        endLeftEdge %= 60;
        endRightEdge %= 60;

        // one or either notes are size 60, they must be overlapping.
        if (startLeftEdge == startRightEdge || endLeftEdge == endRightEdge) return true;
        
        if (startLeftEdge > startRightEdge)
        {
            return endLeftEdge > endRightEdge || IsPartiallyOverlapping(endLeftEdge, endRightEdge, startLeftEdge, startRightEdge);
        }

        if (endLeftEdge> endRightEdge) return endLeftEdge < startRightEdge || endRightEdge > startLeftEdge;
        return endLeftEdge < startRightEdge && endRightEdge > startLeftEdge;
    }
}