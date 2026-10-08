namespace Application.Common
{
    /// <summary>
    /// Сброс закэшированного статуса бана — чтобы бан от автомодерации начал
    /// действовать сразу, а не через TTL кэша.
    /// </summary>
    public interface IBanStatusCache
    {
        void Invalidate(string userId);
    }

    /// <summary>То же для бана по IP: новый бан начинает действовать сразу.</summary>
    public interface IIpBanCache
    {
        void InvalidateIp(string ipAddress);
    }
}
