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
    }
}
