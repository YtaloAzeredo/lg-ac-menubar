import Foundation
import Testing
@testable import LGACCore

@Suite struct ThinQErrorTests {
    @Test(arguments: ["1103", "1218", "1302"])
    func tokenCodes(code: String) {
        #expect(ThinQError.from(code: code, message: "x", httpStatus: 400) == .invalidToken)
    }

    @Test func http401WithoutBodyIsInvalidToken() {
        #expect(ThinQError.from(code: nil, message: nil, httpStatus: 401) == .invalidToken)
    }

    @Test func rateLimit() {
        #expect(ThinQError.from(code: "1306", message: "x", httpStatus: 429) == .rateLimited)
    }

    @Test func deviceOffline() {
        #expect(ThinQError.from(code: "1222", message: "x", httpStatus: 400) == .deviceOffline)
    }

    @Test(arguments: ["1102", "2205", "2207", "2208", "2301", "2302", "2303", "2304", "2305"])
    func rejectedCommands(code: String) {
        #expect(
            ThinQError.from(code: code, message: "não pode", httpStatus: 400)
                == .commandRejected(code: code, message: "não pode"))
    }

    @Test func unknownCode() {
        #expect(ThinQError.from(code: "9999", message: "m", httpStatus: 500) == .unexpected(code: "9999", message: "m"))
    }

    @Test func noBodyUsesHTTPStatus() {
        #expect(ThinQError.from(code: nil, message: nil, httpStatus: 502) == .unexpected(code: "HTTP 502", message: ""))
    }

    @Test func userMessages() {
        #expect(ThinQError.network(.notConnectedToInternet).userMessage == "Sem conexão")
        #expect(ThinQError.invalidToken.userMessage == "Token inválido, gere um novo")
        #expect(ThinQError.deviceOffline.userMessage == "Ar-condicionado offline")
        #expect(ThinQError.commandRejected(code: "2304", message: "").userMessage == "Comando recusado pelo aparelho")
        #expect(ThinQError.commandRejected(code: "2304", message: "Desligado").userMessage == "Comando recusado: Desligado")
    }
}
