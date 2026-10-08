using System.Linq;
using System.Net;
using Application.Common;
using Microsoft.AspNetCore.Http;

namespace WebAPI.Services
{
    /// <summary>
    /// IP клиента. В Azure App Service запрос приходит через фронтенд-прокси, и
    /// RemoteIpAddress — это адрес прокси, а настоящий клиент — в X-Forwarded-For
    /// (в виде "ip:port"). Берём ПОСЛЕДНЕЕ значение: его дописывает сам App Service,
    /// а всё, что левее, мог прислать клиент, чтобы подставить чужой IP.
    /// Локально заголовка нет — используется RemoteIpAddress.
    /// </summary>
    public static class ClientIp
    {
        public static string? Resolve(HttpContext? context)
        {
            if (context == null) return null;

            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var last = forwarded.Split(',').Select(s => s.Trim()).LastOrDefault(s => s.Length > 0);
                var parsed = Parse(last);
                if (parsed != null) return parsed;
            }

            var remote = context.Connection.RemoteIpAddress;
            if (remote == null) return null;
            return (remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote).ToString();
        }

        // "1.2.3.4", "1.2.3.4:5678", "[2001:db8::1]:443", "2001:db8::1"
        private static string? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var candidate = value;
            if (candidate.StartsWith('['))
            {
                var end = candidate.IndexOf(']');
                if (end > 0) candidate = candidate.Substring(1, end - 1);
            }
            else if (candidate.Count(c => c == ':') == 1)
            {
                candidate = candidate.Substring(0, candidate.IndexOf(':'));
            }

            if (!IPAddress.TryParse(candidate, out var ip)) return null;
            return (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
        }
    }

    /// <summary>IClientContext поверх текущего HTTP-запроса (в том числе соединения хаба).</summary>
    public sealed class HttpClientContext : IClientContext
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpClientContext(IHttpContextAccessor accessor) => _accessor = accessor;

        public string? IpAddress => ClientIp.Resolve(_accessor.HttpContext);
    }
}
