import Foundation
import Testing
@testable import LGACCore

/// Guarda as requisições que o cliente tentou fazer.
actor RequestLog {
    private(set) var requests: [URLRequest] = []
    func append(_ request: URLRequest) { requests.append(request) }
}

private let credentials = ThinQCredentials(token: "tok", country: "BR", clientId: "lgac-test")

/// Cliente com transporte falso: nunca acessa a rede.
private func makeClient(status: Int = 200, body: String, log: RequestLog = RequestLog()) throws -> ThinQClient {
    try ThinQClient(credentials: credentials) { request in
        await log.append(request)
        let response = HTTPURLResponse(url: request.url!, statusCode: status, httpVersion: nil, headerFields: nil)!
        return (Data(body.utf8), response)
    }
}

@Suite struct ThinQClientTests {
    @Test func listsDevicesWithAllHeaders() async throws {
        let log = RequestLog()
        let body = """
            {"response": [
              {"deviceId": "ac-1", "deviceInfo": {"deviceType": "DEVICE_AIR_CONDITIONER", "modelName": "M1", "alias": "Ar do Quarto", "reportable": true}},
              {"deviceId": "w-1", "deviceInfo": {"deviceType": "DEVICE_WASHER", "modelName": "W1", "alias": "Lavadora"}}
            ]}
            """
        let devices = try await makeClient(body: body, log: log).devices()

        #expect(devices == [
            ThinQDevice(id: "ac-1", type: "DEVICE_AIR_CONDITIONER", alias: "Ar do Quarto", modelName: "M1"),
            ThinQDevice(id: "w-1", type: "DEVICE_WASHER", alias: "Lavadora", modelName: "W1"),
        ])
        #expect(devices.map(\.isAirConditioner) == [true, false])

        let request = try #require(await log.requests.first)
        #expect(request.httpMethod == "GET")
        #expect(request.url?.absoluteString == "https://api-aic.lgthinq.com/devices")
        #expect(request.value(forHTTPHeaderField: "Authorization") == "Bearer tok")
        #expect(request.value(forHTTPHeaderField: "x-country") == "BR")
        #expect(request.value(forHTTPHeaderField: "x-client-id") == "lgac-test")
        #expect(request.value(forHTTPHeaderField: "x-api-key") == ThinQClient.apiKey)
        #expect(request.value(forHTTPHeaderField: "x-service-phase") == "OP")
        #expect(request.value(forHTTPHeaderField: "x-message-id")?.count == 22)
        #expect(request.value(forHTTPHeaderField: "x-conditional-control") == nil)
    }

    @Test func messageIdsAreUnique() {
        #expect(ThinQClient.makeMessageId() != ThinQClient.makeMessageId())
    }

    @Test func profileAndStateReturnResponseContent() async throws {
        let log = RequestLog()
        let client = try makeClient(body: #"{"response": {"property": {"x": 1}}}"#, log: log)
        #expect(try await client.profile(deviceId: "ac-1") == ["property": ["x": 1]])
        _ = try await client.state(deviceId: "ac-1")
        let urls = await log.requests.map { $0.url?.absoluteString }
        #expect(urls == [
            "https://api-aic.lgthinq.com/devices/ac-1/profile",
            "https://api-aic.lgthinq.com/devices/ac-1/state",
        ])
    }

    @Test func controlPostsBodyWithConditionalHeader() async throws {
        let log = RequestLog()
        let payload: JSONValue = ["display": ["light": "OFF"]]
        try await makeClient(body: #"{"response": {}}"#, log: log).control(deviceId: "ac-1", payload: payload)

        let request = try #require(await log.requests.first)
        #expect(request.httpMethod == "POST")
        #expect(request.url?.absoluteString == "https://api-aic.lgthinq.com/devices/ac-1/control")
        #expect(request.value(forHTTPHeaderField: "x-conditional-control") == "true")
        let sent = try JSONDecoder().decode(JSONValue.self, from: try #require(request.httpBody))
        #expect(sent == payload)
    }

    @Test func errorBodyBecomesThinQError() async throws {
        let client = try makeClient(status: 400, body: #"{"error": {"code": "1306", "message": "limit"}}"#)
        await #expect(throws: ThinQError.rateLimited) { try await client.state(deviceId: "ac-1") }
    }

    @Test func numericErrorCodeIsAccepted() async throws {
        let client = try makeClient(status: 400, body: #"{"error": {"code": 1222, "message": "offline"}}"#)
        await #expect(throws: ThinQError.deviceOffline) { try await client.state(deviceId: "ac-1") }
    }

    @Test func nonJSONErrorBodyDoesNotCrash() async throws {
        let client = try makeClient(status: 502, body: "<html>Bad Gateway</html>")
        await #expect(throws: ThinQError.unexpected(code: "HTTP 502", message: "")) {
            try await client.state(deviceId: "ac-1")
        }
    }

    @Test func transportFailureBecomesNetworkError() async throws {
        let client = try ThinQClient(credentials: credentials) { _ in throw URLError(.notConnectedToInternet) }
        await #expect(throws: ThinQError.network(.notConnectedToInternet)) { try await client.devices() }
    }

    @Test func unsupportedCountryIsRejected() {
        #expect(throws: ThinQError.unsupportedCountry) {
            try ThinQClient(credentials: ThinQCredentials(token: "t", country: "XX", clientId: "c"))
        }
    }

    @Test func serverFollowsCountry() async throws {
        let log = RequestLog()
        let client = try ThinQClient(credentials: ThinQCredentials(token: "t", country: "DE", clientId: "c")) { request in
            await log.append(request)
            return (Data(#"{"response": []}"#.utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
        }
        _ = try await client.devices()
        #expect(await log.requests.first?.url?.host == "api-eic.lgthinq.com")
    }
}
