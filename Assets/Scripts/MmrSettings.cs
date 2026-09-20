using UnityEngine;

namespace OrbitalRift
{
    /// <summary>Единая настройка сезонного рейтинга. Очки забега сравниваются с текущим MMR.</summary>
    public static class MmrSettings
    {
        // Revision resets the original prototype 1000-MMR start once, so the
        // new compact rank ladder is fair for existing installs too.
        public const int RatingRevision = 2;
        public static int StartingMmr => MmrSettingsProfile.Current.StartingMmr;
        public static int MinimumMmr => MmrSettingsProfile.Current.MinimumMmr;

        public static int NavigatorThreshold => MmrSettingsProfile.Current.NavigatorThreshold;
        public static int GuardianThreshold => MmrSettingsProfile.Current.GuardianThreshold;
        public static int LegendThreshold => MmrSettingsProfile.Current.LegendThreshold;
        public static int OverlordThreshold => MmrSettingsProfile.Current.OverlordThreshold;
        public static int DivinityThreshold => MmrSettingsProfile.Current.DivinityThreshold;

        public static int MinimumGain => MmrSettingsProfile.Current.MinimumGain;
        public static int MaximumGain => MmrSettingsProfile.Current.MaximumGain;
        public static int MinimumLoss => MmrSettingsProfile.Current.MinimumLoss;
        public static int MaximumLoss => MmrSettingsProfile.Current.MaximumLoss;

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
