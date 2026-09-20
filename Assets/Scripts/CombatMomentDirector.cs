using UnityEngine;

namespace OrbitalRift
{
    /// <summary>
    /// A compact, rule-driven moment director.  It never invents a situation:
    /// the caption is triggered only after the matching combat event occurs.
    /// This is the safe base for later clips/screenshots and social sharing.
    /// </summary>
    public sealed class CombatMomentDirector
    {
        public string Caption { get; private set; } = string.Empty;
        public Color Color { get; private set; } = Color.white;
        public float TimeLeft { get; private set; }

        public bool IsVisible => TimeLeft > 0f && !string.IsNullOrEmpty(Caption);

        public void Tick(float dt)
        {
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);
        }

        public void Clear()
        {
            Caption = string.Empty;
            TimeLeft = 0f;
        }

        public void EchoCast()
        {
            Show(new[]
            {
                "ЭХО ВЫШЛО НА СМЕНУ",
                "ВТОРОЙ КОРАБЛЬ?\nЭТО ПРОСТО ТЫ ВЧЕРА",
                "РАЗЛОМ ОДОБРЯЕТ ДУБЛЬ"
            }, new Color(.36f, .92f, 1f));
        }

        public void EchoKill()
        {
            Show(new[]
            {
                "ЭХО ЗАКРЫЛО ВОПРОС",
                "ПОКА ТЫ ПОВОРАЧИВАЛСЯ...",
                "ДВОЙНИК ВЗЯЛ ФРАГ"
            }, new Color(.55f, .88f, 1f));
        }

        public void VectorSnap(bool escapedBeam)
        {
            Show(escapedBeam
                ? new[] { "ВАУ, ЭТО БЫЛО ПО ПЛАНУ", "ЛУЧ ПРОЛЕТЕЛ МИМО КАРЬЕРЫ" }
                : new[] { "ГЕОМЕТРИЯ ОТМЕНЕНА", "ТАКСИ ДО ДРУГОГО СЕКТОРА" },
                new Color(.84f, .58f, 1f));
        }

        public void GravrootCaught()
        {
            Show(new[]
            {
                "КОРНИ? В КОСМОСЕ?",
                "ГРАВКОРНИ: КОРАБЛЬ ОТМЕНЁН",
                "ПАРКОВКА НА ОДНУ СЕКУНДУ"
            }, new Color(1f, .36f, .76f));
        }

        public void GravrootDodged()
        {
            Show(new[]
            {
                "ГРАВКОРНИ ПОЙМАЛИ ВАКУУМ",
                "ТУТ БЫЛ КОРАБЛЬ. ЧЕСТНО.",
                "ПАРКОВКА ОТМЕНЯЕТСЯ"
            }, new Color(.72f, .56f, 1f));
        }

        private void Show(string[] choices, Color color)
        {
            if (choices == null || choices.Length == 0) return;
            Caption = choices[Random.Range(0, choices.Length)];
            Color = color;
            TimeLeft = 1.85f;
        }
    }
}
