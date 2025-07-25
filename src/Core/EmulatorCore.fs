namespace HamicomEmu.EmulatorCore

module EmulatorCore =

    open HamicomEmu.Cpu
    open HamicomEmu.Bus
    open HamicomEmu.Ppu
    open HamicomEmu.Ppu.Types

    type EmulatorState = {
        cpu: Cpu.CpuState
        bus: Bus.BusState
        mutable ppuSnapshot: PpuPublicState
    }

    let init cart =
        let bus = Bus.init cart

        {
            cpu = Cpu.init
            bus = bus
            ppuSnapshot = PpuPublicState.init bus.cartridge.mapper
        }

    let reset emu =
        let cpu', bus' = Cpu.reset emu.cpu emu.bus

        {
            cpu = cpu'
            bus = bus'
            ppuSnapshot = PpuPublicState.init bus'.cartridge.mapper
        }

    let updateSnapshot (ppu: PpuState) (emu: EmulatorState) : EmulatorState =
        { emu with ppuSnapshot = PpuPublicState.fromPpu ppu }

    /// ステップの実行回数指定をできるようにしてあるけど使わないかも
    /// TODO: Bus.tick を 1 ずつ回すようにした影響で再現度は上がったけど実行速度が犠牲に！適宜各種状態の mutable 化を検討中
    let rec tickN n emu (trace: EmulatorState -> unit) =
        let rec loop n emu consumedTotal =
            if n <= 0 then
                emu, consumedTotal
            else
                match emu.bus.pendingStallCpuCycles with
                | Some stall when stall > 0u ->
                    // ストール中は CPU 実行を止めて Bus/APU/PPU だけ進める
                    let bus' = Bus.tick emu.bus
                    let newStall = stall - 1u
                    let bus'' = Bus.updatePendingStallCpuCycles newStall bus'
                    loop (n - 1) { emu with bus = bus'' } (consumedTotal + 1u)
                | _ ->
                    // NOTE: ここで割り込み判定をしているけど正確にはもっと複雑らしい？ https://www.nesdev.org/wiki/CPU_interrupts
                    let irq = Bus.pollIrqStatus emu.bus
                    let interruptDisabled = Cpu.interruptDisabled emu.cpu
                    let suppressIrq = Cpu.isSuppressIrq emu.cpu

                    let cpu, bus, consumed =
                        match Bus.pollNmiStatus emu.bus, irq && not interruptDisabled && not suppressIrq with
                        | (b, Some _), _ -> // NMI
                            Cpu.interruptNmi emu.cpu b
                        | (b, None), true -> // IRQ
                            Cpu.irq emu.cpu b
                        | (b, None), false -> // 通常進行
                            // 通常進行の場合はトレース実行
                            trace emu
                            // FIXME: CLI, SEI, PLP の IRQ 抑制だけど後でもっといい仕組みを考える
                            let c = if suppressIrq then Cpu.clearSuppressIrq emu.cpu else emu.cpu
                            let c', b, con = Cpu.step c b
                            c', b, con

                    let bus = Bus.tickNTimes (int consumed) bus
                    let ppu = bus.ppu
                    // PPU が 1 フレーム分処理したら非同期描画のためにスナップショットをコピーする
                    if ppu.frameJustCompleted then
                        emu.ppuSnapshot <- PpuPublicState.fromPpu ppu
                        ppu.frameJustCompleted <- false

                    let emu' = { emu with cpu = cpu; bus = bus }

                    loop (n - 1) emu' (consumedTotal + consumed)

        loop n emu 0u

    let tick emu trace =
        let emu', cycles = tickN 1 emu trace
        emu', cycles
