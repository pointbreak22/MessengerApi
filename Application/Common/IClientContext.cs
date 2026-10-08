namespace Application.Common
{
    /// <summary>
    /// Сведения о текущем запросе, которые нужны Application-слою, но живут в
    /// HTTP: IP клиента для журнала нарушений и автобана по IP.
    /// </summary>
    public interface IClientContext
    {
        /// <summary>null — вне HTTP-запроса (фоновая задача) или IP не определён.</summary>
        string? IpAddress { get; }
    }
}
