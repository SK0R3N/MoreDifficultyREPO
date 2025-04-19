using System;
using System.Collections.Generic;
using System.Text;

namespace MyMOD
{
    public static class DifficultyManager
    {
        public static float DifficultyMultiplier = 1f;

        public enum DifficultyLevel
        {
            Custom,
            Normal,
            Hard,
            Hardcore,
            Nightmare,
            IsThatEvenPossible
        }

        public static DifficultyLevel CurrentDifficulty = DifficultyLevel.Normal;
        public static int EnemyMultiplier = 1;
        public static float ShopMultiplier = 1;
        public static int ExtractionMultiplier = 1;
        public static int ExtractionMaxMultiplier = 1;
        public static int ValuableMultiplier = 1;
        public static int RoomNumber = 30;
        public static int PourcentageRoom1 = 100;
        public static int PourcentageRoom2 = 0;
        public static int PourcentageRoom3 = 0;
        public static string DifficultyPreset = "None";
    }

}
