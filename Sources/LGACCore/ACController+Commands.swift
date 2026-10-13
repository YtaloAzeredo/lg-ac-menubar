import Foundation

extension ACController {
    /// Aplica o comando na tela na hora e envia; se a LG recusar, volta ao estado anterior.
    public func send(_ command: ACCommand) async {
        guard let caps = capabilities, let current = state, let payload = command.payload(using: caps) else { return }
        state = command.apply(to: current)
        await deliver(payload, rollbackTo: current)
    }

    /// `−`/`+`: muda na tela na hora e envia um único comando 0,6 s depois do último clique.
    public func stepTargetTemp(by steps: Int) {
        guard let temp = capabilities?.targetTemp, let current = state,
            let value = pendingTargetTemp ?? current.targetTemp
        else { return }
        let next = temp.clamp(value + Double(steps) * temp.step)
        guard next != value else { return }
        if tempBaseline == nil { tempBaseline = current }
        pendingTargetTemp = next
        state = ACCommand.targetTemp(next).apply(to: current)
        tempTask?.cancel()
        tempTask = Task { [weak self] in
            guard let self else { return }
            do { try await self.sleep(Self.tempDebounce) } catch { return }
            await self.flushTargetTemp()
        }
    }

    func flushTargetTemp() async {
        guard let target = pendingTargetTemp, let baseline = tempBaseline else { return }
        pendingTargetTemp = nil
        tempBaseline = nil
        guard let caps = capabilities, let payload = ACCommand.targetTemp(target).payload(using: caps) else { return }
        await deliver(payload, rollbackTo: baseline)
    }

    func deliver(_ payload: JSONValue, rollbackTo previous: ACState) async {
        guard let client, let deviceId = store.deviceId else { return }
        do {
            try await client.control(deviceId: deviceId, payload: payload)
            statusMessage = nil
            scheduleConfirmation()
        } catch {
            if !(error is CancellationError) { state = previous }
            handle(error)
        }
    }

    /// Relê o estado ~1,5 s depois do comando, para confirmar que o aparelho aplicou.
    func scheduleConfirmation() {
        confirmTask?.cancel()
        confirmTask = Task { [weak self] in
            guard let self else { return }
            do { try await self.sleep(Self.confirmDelay) } catch { return }
            await self.refresh(force: true)
        }
    }
}
