namespace Application.Moderation
{
    /// <summary>Секция "Moderation" в appsettings.</summary>
    public sealed class ModerationSettings
    {
        public const string SectionName = "Moderation";

        public bool Enabled { get; set; } = true;

        /// <summary>На каком нарушении бан: 3 = два предупреждения, третье — бан.</summary>
        public int MaxStrikes { get; set; } = 3;

        /// <summary>Сколько дней без нарушений, чтобы предупреждения сгорели.</summary>
        public int StrikeDecayDays { get; set; } = 30;

        /// <summary>
        /// Автобан IP: столько разных аккаунтов должны получить страйк с одного IP
        /// за StrikeDecayDays дней. 0 — автобан по IP выключен.
        /// </summary>
        public int IpBanUserThreshold { get; set; } = 3;

        /// <summary>
        /// На сколько дней ставится автобан IP (0 — бессрочно). Временный по умолчанию:
        /// за одним IP бывает целый офис или абоненты мобильного оператора.
        /// </summary>
        public int IpBanDays { get; set; } = 7;
    }
}
