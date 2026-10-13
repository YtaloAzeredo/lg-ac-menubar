import Foundation
import Testing
@testable import LGACCore

@MainActor @Suite struct ACControllerRefreshTests {
    @Test func networkErrorKeepsLastState() async throws {
        let fake = try FakeClient.full()
        let controller = makeController(fake: fake)
        await controller.bootstrap()

        await fake.setState(.failure(.network(.notConnectedToInternet)))
        await controller.refresh(force: true)

        #expect(controller.state?.targetTemp == 23)
        #expect(controller.statusMessage == "Sem conexão")
        #expect(controller.phase == .ready)
    }

    @Test func noNetworkAtStartupStaysLoadingThenRecovers() async throws {
        let fake = try FakeClient.full()
        await fake.setProfile(.failure(.network(.notConnectedToInternet)))
        let controller = makeController(fake: fake)

        await controller.bootstrap()
        #expect(controller.phase == .loading)
        #expect(controller.statusMessage == "Sem conexão")

        await fake.setProfile(.success(try Fixture.json("profile-full")))
        await controller.refresh(force: true)
        #expect(controller.phase == .ready)
        #expect(controller.statusMessage == nil)
    }

    @Test func invalidTokenDuringRefreshReturnsToSetup() async throws {
        let fake = try FakeClient.full()
        let store = InMemoryCredentialStore.configured()
        let controller = makeController(fake: fake, store: store)
        await controller.bootstrap()

        await fake.setState(.failure(.invalidToken))
        await controller.refresh(force: true)

        #expect(controller.phase == .needsSetup(message: "Token inválido, gere um novo"))
        #expect(store.token == nil)
    }

    @Test func deviceOfflineFlagClearsOnSuccess() async throws {
        let fake = try FakeClient.full()
        let controller = makeController(fake: fake)
        await controller.bootstrap()

        await fake.setState(.failure(.deviceOffline))
        await controller.refresh(force: true)
        #expect(controller.isDeviceOffline)
        #expect(controller.statusMessage == "Ar-condicionado offline")

        await fake.setState(.success(try Fixture.json("state-full")))
        await controller.refresh(force: true)
        #expect(!controller.isDeviceOffline)
    }

    @Test func rateLimitBacksOffUpToThirtyMinutesAndResets() async throws {
        let fake = try FakeClient.full()
        let controller = makeController(fake: fake)
        await controller.bootstrap()
        await fake.setState(.failure(.rateLimited))

        var intervals: [Duration] = []
        for _ in 0..<4 {
            await controller.refresh(force: true)
            intervals.append(controller.pollInterval)
        }
        #expect(intervals == [.seconds(600), .seconds(1200), .seconds(1800), .seconds(1800)])

        await fake.setState(.success(try Fixture.json("state-full")))
        await controller.refresh(force: true)
        #expect(controller.pollInterval == .seconds(300))
    }

    @Test func refreshWithinFiveSecondsIsSkipped() async throws {
        let fake = try FakeClient.full()
        let clock = TestNow()
        let controller = makeController(fake: fake, clock: clock)
        await controller.bootstrap()
        #expect(await fake.stateCalls == 1)

        clock.date += 3
        await controller.refresh()
        #expect(await fake.stateCalls == 1)

        clock.date += 3
        await controller.refresh()
        #expect(await fake.stateCalls == 2)
    }
}
