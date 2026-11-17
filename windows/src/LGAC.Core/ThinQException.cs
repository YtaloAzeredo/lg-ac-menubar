namespace LGAC.Core;

public enum ThinQErrorKind
{
    InvalidToken,
    RateLimited,
    DeviceOffline,
    CommandRejected,
    Network,
    Unexpected,
    UnsupportedCountry,
}

public sealed class ThinQException : Exception, IEquatable<ThinQException>
{
    public ThinQException(ThinQErrorKind kind, string code = "", string apiMessage = "")
        : base(BuildUserMessage(kind, code, apiMessage))
    {
        Kind = kind;
        Code = code;
        ApiMessage = apiMessage;
    }

    public ThinQErrorKind Kind { get; }

    public string Code { get; }

    public string ApiMessage { get; }

    public string UserMessage => Message;

    public static ThinQException From(string? code, string? message, int httpStatus)
    {
        var text = message ?? "";
        if (code is null)
        {
            return httpStatus == 401
                ? new ThinQException(ThinQErrorKind.InvalidToken)
                : new ThinQException(ThinQErrorKind.Unexpected, $"HTTP {httpStatus}", text);
        }

        switch (code)
        {
            case "1103":
            case "1218":
            case "1302":
                return new ThinQException(ThinQErrorKind.InvalidToken);
            case "1306":
                return new ThinQException(ThinQErrorKind.RateLimited);
            case "1222":
                return new ThinQException(ThinQErrorKind.DeviceOffline);
            case "1102":
            case "2205":
            case "2207":
            case "2208":
            case "2301":
            case "2302":
            case "2303":
            case "2304":
            case "2305":
                return new ThinQException(ThinQErrorKind.CommandRejected, code, text);
            default:
                return httpStatus == 401
                    ? new ThinQException(ThinQErrorKind.InvalidToken)
                    : new ThinQException(ThinQErrorKind.Unexpected, code, text);
        }
    }

    public bool Equals(ThinQException? other)
    {
        return other is not null && Kind == other.Kind && Code == other.Code && ApiMessage == other.ApiMessage;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ThinQException);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Kind, Code, ApiMessage);
    }

    private static string BuildUserMessage(ThinQErrorKind kind, string code, string apiMessage)
    {
        return kind switch
        {
            ThinQErrorKind.InvalidToken => "Token inválido, gere um novo",
            ThinQErrorKind.RateLimited => "Limite de chamadas atingido, atualizando mais devagar",
            ThinQErrorKind.DeviceOffline => "Ar-condicionado offline",
            ThinQErrorKind.CommandRejected => apiMessage.Length == 0
                ? "Comando recusado pelo aparelho"
                : $"Comando recusado: {apiMessage}",
            ThinQErrorKind.Network => "Sem conexão",
            ThinQErrorKind.Unexpected => apiMessage.Length == 0
                ? $"Erro LG {code}"
                : $"Erro LG {code}: {apiMessage}",
            ThinQErrorKind.UnsupportedCountry => "País não suportado pela API da LG",
            _ => "",
        };
    }
}
