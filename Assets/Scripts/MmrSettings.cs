using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Единая настройка сезонного рейтинга. Очки забега сравниваются с текущим MMR.</summary>
    public static class MmrSettings
    {
        // Revision resets the original prototype 1000-MMR start once, so the
        // new compact rank ladder is fair for existing installs too.
        public const int RatingRevision = 2;
        public const int StartingMmr = 25;
        public const int MinimumMmr = 0;

        public const int NavigatorThreshold = 0;
        public const int GuardianThreshold = 1000;
        public const int LegendThreshold = 2000;
        public const int OverlordThreshold = 3000;
        public const int DivinityThreshold = 4000;

        public const int MinimumGain = 25;
        public const int MaximumGain = 150;
        public const int MinimumLoss = 15;
        public const int MaximumLoss = 150;

        // Разница в один текущий рейтинг уже даёт максимальный эффект.
        public static int CalculateChange(int runScore, int currentMmr)
        {
            var rating = Mathf.Max(1, currentMmr);
            if (runScore >= currentMmr)
            {
                var advantage = Mathf.Clamp01((runScore - currentMmr) / (float)rating);
                return Mathf.RoundToInt(Mathf.Lerp(MinimumGain, MaximumGain, advantage));
            }

            var shortfall = Mathf.Clamp01((currentMmr - runScore) / (float)rating);
            var calculatedLoss = Mathf.RoundToInt(Mathf.Lerp(MinimumLoss, MaximumLoss, shortfall));
            // Дельта всегда совпадает с фактическим изменением: ниже нуля MMR
            // уйти не может, поэтому игрок с рейтингом 25 не увидит ложные -150.
            var availableMmr = Mathf.Max(0, currentMmr - MinimumMmr);
            return -Mathf.Min(calculatedLoss, availableMmr);
        }
    }
}
