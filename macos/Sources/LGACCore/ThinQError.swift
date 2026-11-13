import Foundation

/// Erros da API ThinQ Connect, agrupados pelo que o app faz com cada um.
/// Códigos da tabela `ThinQAPIErrorCodes` do SDK oficial.
public enum ThinQError: Error, Equatable, Sendable {
    case invalidToken
    case rateLimited
    case deviceOffline
    case commandRejected(code: String, message: String)
    case network(URLError.Code)
    case unexpected(code: String, message: String)
    case unsupportedCountry

    /// Converte o corpo `{"error": {"code", "message"}}` (ou só o status HTTP) no caso certo.
    public static func from(code: String?, message: String?, httpStatus: Int) -> ThinQError {
        let message = message ?? ""
        guard let code else {
            return httpStatus == 401 ? .invalidToken : .unexpected(code: "HTTP \(httpStatus)", message: message)
        }
        switch code {
        case "1103", "1218", "1302":
            return .invalidToken
        case "1306":
            return .rateLimited
        case "1222":
            return .deviceOffline
        case "1102", "2205", "2207", "2208", "2301", "2302", "2303", "2304", "2305":
            return .commandRejected(code: code, message: message)
        default:
            return httpStatus == 401 ? .invalidToken : .unexpected(code: code, message: message)
        }
    }

    /// Texto curto em português para o rodapé do painel.
    public var userMessage: String {
        switch self {
        case .invalidToken:
            "Token inválido, gere um novo"
        case .rateLimited:
            "Limite de chamadas atingido, atualizando mais devagar"
        case .deviceOffline:
            "Ar-condicionado offline"
        case .commandRejected(_, let message):
            message.isEmpty ? "Comando recusado pelo aparelho" : "Comando recusado: \(message)"
        case .network:
            "Sem conexão"
        case .unexpected(let code, let message):
            message.isEmpty ? "Erro LG \(code)" : "Erro LG \(code): \(message)"
        case .unsupportedCountry:
            "País não suportado pela API da LG"
        }
    }
}
