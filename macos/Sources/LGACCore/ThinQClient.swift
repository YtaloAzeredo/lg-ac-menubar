import Foundation

/// Um aparelho da conta LG (item de `GET /devices`).
public struct ThinQDevice: Sendable, Equatable, Identifiable {
    public let id: String
    public let type: String
    public let alias: String
    public let modelName: String

    public init(id: String, type: String, alias: String, modelName: String) {
        self.id = id
        self.type = type
        self.alias = alias
        self.modelName = modelName
    }

    public init?(json: JSONValue) {
        guard let id = json["deviceId"]?.stringValue else { return nil }
        let info = json["deviceInfo"]
        self.init(
            id: id,
            type: info?["deviceType"]?.stringValue ?? "",
            alias: info?["alias"]?.stringValue ?? "",
            modelName: info?["modelName"]?.stringValue ?? "")
    }

    public var isAirConditioner: Bool { type == "DEVICE_AIR_CONDITIONER" }
}

public struct ThinQCredentials: Sendable, Equatable {
    public let token: String
    public let country: String
    public let clientId: String

    public init(token: String, country: String, clientId: String) {
        self.token = token
        self.country = country
        self.clientId = clientId
    }
}

public protocol ThinQClientProtocol: Sendable {
    func devices() async throws -> [ThinQDevice]
    func profile(deviceId: String) async throws -> JSONValue
    func state(deviceId: String) async throws -> JSONValue
    func control(deviceId: String, payload: JSONValue) async throws
}

/// Cliente HTTP da API LG ThinQ Connect.
public struct ThinQClient: ThinQClientProtocol {
    public typealias Transport = @Sendable (URLRequest) async throws -> (Data, URLResponse)

    /// Constante pública do SDK oficial (`thinqconnect/const.py`), a mesma para todos os clientes.
    static let apiKey = "v6GFvkweNo7DK7yD3ylIZ9w52aKBU0eJ7wLXkSR3"

    let credentials: ThinQCredentials
    let baseURL: URL
    let transport: Transport

    public init(
        credentials: ThinQCredentials,
        transport: @escaping Transport = { try await URLSession.shared.data(for: $0) }
    ) throws {
        guard let baseURL = ThinQRegion.baseURL(forCountry: credentials.country) else {
            throw ThinQError.unsupportedCountry
        }
        self.credentials = credentials
        self.baseURL = baseURL
        self.transport = transport
    }

    public func devices() async throws -> [ThinQDevice] {
        try await rawGET("devices").arrayValue?.compactMap(ThinQDevice.init(json:)) ?? []
    }

    public func profile(deviceId: String) async throws -> JSONValue {
        try await rawGET("devices/\(deviceId)/profile")
    }

    public func state(deviceId: String) async throws -> JSONValue {
        try await rawGET("devices/\(deviceId)/state")
    }

    public func control(deviceId: String, payload: JSONValue) async throws {
        _ = try await send("POST", "devices/\(deviceId)/control", body: payload)
    }

    /// GET cru, sem interpretar a resposta. Usado também pela ferramenta `lgac-dump`.
    public func rawGET(_ path: String) async throws -> JSONValue {
        try await send("GET", path)
    }

    func send(_ method: String, _ path: String, body: JSONValue? = nil) async throws -> JSONValue {
        let request = try makeRequest(method, path, body: body)
        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await transport(request)
        } catch let error as URLError where error.code == .cancelled {
            // URLSession reporta cancelamento como URLError; para o chamador é um CancellationError.
            throw CancellationError()
        } catch let error as URLError {
            throw ThinQError.network(error.code)
        }
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        let json = try? JSONDecoder().decode(JSONValue.self, from: data)
        guard (200..<300).contains(status) else {
            let error = json?["error"]
            let code = error?["code"].flatMap { $0.stringValue ?? $0.intValue.map(String.init) }
            throw ThinQError.from(code: code, message: error?["message"]?.stringValue, httpStatus: status)
        }
        return json?["response"] ?? .null
    }

    func makeRequest(_ method: String, _ path: String, body: JSONValue? = nil) throws -> URLRequest {
        var request = URLRequest(url: baseURL.appendingPathComponent(path))
        request.httpMethod = method
        request.timeoutInterval = 15
        request.setValue("Bearer \(credentials.token)", forHTTPHeaderField: "Authorization")
        request.setValue(credentials.country.uppercased(), forHTTPHeaderField: "x-country")
        request.setValue(Self.makeMessageId(), forHTTPHeaderField: "x-message-id")
        request.setValue(credentials.clientId, forHTTPHeaderField: "x-client-id")
        request.setValue(Self.apiKey, forHTTPHeaderField: "x-api-key")
        request.setValue("OP", forHTTPHeaderField: "x-service-phase")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if let body {
            // Só o POST de controle tem corpo, e é ele que leva este header.
            request.setValue("true", forHTTPHeaderField: "x-conditional-control")
            request.httpBody = try JSONEncoder().encode(body)
        }
        return request
    }

    /// UUID em base64url sem padding (22 caracteres), como o SDK oficial.
    static func makeMessageId() -> String {
        let bytes = withUnsafeBytes(of: UUID().uuid) { Data($0) }
        return bytes.base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }
}
