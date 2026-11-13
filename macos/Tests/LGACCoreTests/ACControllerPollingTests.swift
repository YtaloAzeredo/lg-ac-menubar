import Foundation
import Testing
@testable import LGACCore

/// O laço de polling de verdade, com a espera controlada pelo teste (`PollClock`).
@MainActor @Suite struct ACControllerPollingTests {
    private func pollingController(_ fake: FakeClient, clock: PollClock) async -> ACController {
        let controller = makeController(fake: fake, sleep: { try await clock.sleep($0) })
        await controller.bootstrap()
        await clock.waitForSleeps(1)
        return controller
    }

    /// Mudança feita pelo controle remoto aparece no próximo ciclo, sem abrir o painel.
    @Test func eachCycleReadsTheStateAgain() async throws {
        let fake = try FakeClient.full()
        let clock = PollClock()
        let controller = await pollingController(fake, clock: clock)
        #expect(controller.state?.isOn == true)

        await fake.setState(.success(try Fixture.json("state-basic")))
        await clock.tick()
        await clock.waitForSleeps(2)

        #expect(controller.state?.isOn == false)
        #expect(controller.state?.targetTemp == 22.5)
        #expect(await clock.intervals == [.seconds(300), .seconds(300)])
    }

    @Test func rateLimitMakesTheLoopWaitLonger() async throws {
        let fake = try FakeClient.full()
        let clock = PollClock()
        // Guardado até o fim: o laço só tem referência fraca e para se o controlador sumir.
        let controller = await pollingController(fake, clock: clock)

        await fake.setState(.failure(.rateLimited))
        await clock.tick()
        await clock.waitForSleeps(2)
        await clock.tick()
        await clock.waitForSleeps(3)

        #expect(await clock.intervals == [.seconds(300), .seconds(600), .seconds(1200)])
        #expect(controller.statusMessage == ThinQError.rateLimited.userMessage)
    }

    /// Sem internet ao abrir o app, o próprio polling conecta quando a rede volta.
    @Test func noNetworkAtStartupRecoversOnTheNextCycle() async throws {
        let fake = try FakeClient.full()
        await fake.setProfile(.failure(.network(.notConnectedToInternet)))
        let clock = PollClock()
        let controller = await pollingController(fake, clock: clock)
        #expect(controller.phase == .loading)

        await fake.setProfile(.success(try Fixture.json("profile-full")))
        await clock.tick()
        await clock.waitForSleeps(2)

        #expect(controller.phase == .ready)
        #expect(controller.state?.targetTemp == 23)
    }
}
