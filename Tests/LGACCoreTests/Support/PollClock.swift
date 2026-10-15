import Foundation
import Testing
@testable import LGACCore

/// Espera do polling controlada pelo teste: cada intervalo pedido pelo laço fica registrado e
/// suspenso até `tick()`. Atrasos curtos (debounce, confirmação) passam na hora, como em `testSleep`.
actor PollClock {
    /// Intervalos pedidos pelo laço de polling, em ordem.
    private(set) var intervals: [Duration] = []
    private var waiting: CheckedContinuation<Void, any Error>?

    nonisolated func sleep(_ duration: Duration) async throws {
        guard duration >= ACController.basePollInterval else { return try await testSleep(duration) }
        try await withTaskCancellationHandler {
            try await wait(duration)
        } onCancel: {
            Task { await self.cancelWaiting() }
        }
    }

    /// Libera a espera atual: o laço faz uma atualização e volta a esperar.
    func tick() {
        waiting?.resume()
        waiting = nil
    }

    /// Aguarda o laço chegar à `count`-ésima espera. Tem prazo, para um bug não travar a suíte.
    func waitForSleeps(_ count: Int, sourceLocation: SourceLocation = #_sourceLocation) async {
        let deadline = ContinuousClock.now + .seconds(5)
        while intervals.count < count, ContinuousClock.now < deadline { await Task.yield() }
        #expect(intervals.count >= count, "o polling não voltou a esperar", sourceLocation: sourceLocation)
    }

    private func wait(_ duration: Duration) async throws {
        intervals.append(duration)
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, any Error>) in
            if Task.isCancelled {
                continuation.resume(throwing: CancellationError())
            } else {
                waiting = continuation
            }
        }
    }

    private func cancelWaiting() {
        waiting?.resume(throwing: CancellationError())
        waiting = nil
    }
}
