using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Utilities;

namespace MercuryMapper.Config;

public class NoteColorPalette : IColorPalette
    {
        private static readonly Color[,] ColorChart = {
            {
                Color.FromArgb(255, 255,   0, 255),
                Color.FromArgb(255, 204, 190,  45),
                Color.FromArgb(255, 255, 128,   0),
                Color.FromArgb(255,  50, 205,  50),
                Color.FromArgb(255, 255,   0,   0),
                Color.FromArgb(255,   0, 255, 255),
            },

            {

                Color.FromArgb(255, 140, 100,   0),
                Color.FromArgb(255, 220, 185,  50),
                Color.FromArgb(190, 237, 174,  42),
                Color.FromArgb(190, 220, 185,  50),
                Color.FromArgb(190, 178, 178, 178),
                Color.FromArgb(190, 255, 255, 255),
            },

            {
                Color.FromArgb(  0,   0,   0,   0),
                Color.FromArgb(  0,   0,   0,   0),
                Color.FromArgb(  0,   0,   0,   0),
                Color.FromArgb(  0,   0,   0,   0),
                Color.FromArgb(  0,   0,   0,   0),
                Color.FromArgb(  0,   0,   0,   0),
            },

            {
                Color.FromArgb(255, 222,  16,  16),
                Color.FromArgb(255, 255, 154,   0),
                Color.FromArgb(255, 255, 241,   0),
                Color.FromArgb(255,  53, 161, 107),
                Color.FromArgb(255,   0,  65, 255),
                Color.FromArgb(255, 255,  53, 239),

            },

            {
                Color.FromArgb(255, 255,  75,   0),
                Color.FromArgb(255, 155, 139,   0),
                Color.FromArgb(255, 255, 228,  67),
                Color.FromArgb(255,  31, 211,  23),
                Color.FromArgb(255,  24, 173, 255),
                Color.FromArgb(255, 102, 204, 255),
            },
        };

        public int ColorCount => ColorChart.GetLength(0);
        public int ShadeCount => ColorChart.GetLength(1);

        public Color GetColor(int colorIndex, int shadeIndex)
        {
            return ColorChart[
                MathUtilities.Clamp(colorIndex, 0, ColorChart.GetLength(0) - 1),
                MathUtilities.Clamp(shadeIndex, 0, ColorChart.GetLength(1) - 1)];
        }
    }
