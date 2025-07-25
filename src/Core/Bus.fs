namespace HamicomEmu.Bus

module Bus =

    open HamicomEmu.Cartridge
    open HamicomEmu.Mapper
    open HamicomEmu.Ppu.Types
    open HamicomEmu.Ppu
    open HamicomEmu.Apu.Types
    open HamicomEmu.Apu
    open HamicomEmu.Input.Types
    open HamicomEmu.Input

    module Ram =
        let Begin = 0x0000us
        let MirrorsEnd = 0x1FFFus

    module PpuRegisters =
        let Begin = 0x2000us
        let MirrorsEnd = 0x3FFFus

    module ApuRegisters =
        let Begin = 0x4000us
        let End = 0x4017us
    
    module PrgRom =
        let Begin = 0x8000us
        let End = 0xFFFFus

    type BusState = {
        cpuVram: byte array // 0x0000 - 0x1FFF
        cartridge: Cartridge
        ppu: PpuState
        apu: ApuState
        joy1: JoypadState
        joy2: JoypadState
        mutable cycleTotal: uint
        oamDmaCyclesRemaining: uint option // OAM DMA 中に DMC に割り込まれたときの残りサイクル数
        mutable pendingStallCpuCycles: uint option
    }

    let init cart = {
        cpuVram = Array.create 0x2000 0uy
        cartridge = cart
        ppu = Ppu.init cart
        apu = Apu.init
        joy1 = Joypad.init
        joy2 = Joypad.init
        cycleTotal = 0u
        oamDmaCyclesRemaining = None
        pendingStallCpuCycles = None
    }

    let inline inRange startAddr endAddr addr = addr >= startAddr && addr <= endAddr

    let rec memRead addr bus =
        match addr with
        | addr when addr |> inRange Ram.Begin Ram.MirrorsEnd ->
            let mirrorDownAddr = addr &&& 0b0000_0111_1111_1111us
            bus.cpuVram[int mirrorDownAddr], bus

        | 0x2000us
        | 0x2001us
        | 0x2003us
        | 0x2005us
        | 0x2006us
        | 0x4014us ->
            printfn "Attempt to read from write-only PPU address: %04X" addr
            0uy, bus

        | 0x2002us ->
            let beforePpu, ppu = Ppu.readFromStatusRegister bus.ppu
            beforePpu.status, { bus with ppu = ppu }

        | 0x2004us ->
            let data = Ppu.readFromOamData bus.ppu
            data, bus

        | 0x2007us ->
            let data, ppu = Ppu.readFromDataRegister bus.ppu
            data, { bus with ppu = ppu }

        | addr when addr |> inRange 0x2008us PpuRegisters.MirrorsEnd ->
            let mirrorDownAddr = addr &&& 0b0010_0000_0000_0111us
            memRead mirrorDownAddr bus

        | 0x4016us ->
            let data, joy = Joypad.read bus.joy1
            data, { bus with joy1 = joy }

        | 0x4017us -> // TODO: Joypad
            //   let data, joy = readJoypad bus.joy2
            //   data, {bus with joy2 = joy}
            0uy, bus

        | addr when addr |> inRange ApuRegisters.Begin ApuRegisters.End ->
            let data, apu = Apu.read addr bus.apu
            data, { bus with apu = apu }
        
        | addr when addr |> inRange 0x6000us 0x7FFFus ->
            let data = Mapper.readPrgRam addr bus.cartridge
            data, bus

        | addr when addr |> inRange PrgRom.Begin PrgRom.End ->
            let data = Mapper.cpuRead addr bus.cartridge
            data, bus

        | _ ->
            printfn "Invalid Memory access at: %04X" addr
            0uy, bus

    let updatePendingStallCpuCycles newStall bus =
        bus.pendingStallCpuCycles <- if newStall = 0u then None else Some newStall
        bus

    let pollNmiStatus bus =
        if bus.ppu.clearNmiInterrupt then
            {
                bus with
                    ppu.clearNmiInterrupt = false
                    ppu.nmiInterrupt = None
            },
            None
        else
            let res = bus.ppu.nmiInterrupt
            { bus with ppu.nmiInterrupt = None }, res

    let pollIrqStatus bus =
        bus.apu.dmc.irqRequested || bus.apu.frameCounter.irqRequested

    let tick bus =
        let mutable apu = bus.apu
        let mutable ppu = bus.ppu
        let cyc = bus.cycleTotal + 1u

        ppu <- Ppu.tickN 3u ppu

        let result = Apu.tick apu
        let p = result.stallCpuCycles

        apu <- result.apu

        // DMC の読み込み要求を処理
        match result.dmcRead with
        | Some req ->
            let value, _ = memRead req.addr bus
            let dmc = req.onRead value
            apu.dmc <- dmc

        | None -> ()

        bus.cycleTotal <- cyc
        bus.pendingStallCpuCycles <- p
        // NOTE: 副作用でおかしくなったときは tick 内で留めるようにレコードの再生成
        // { bus with apu = apu; ppu = ppu }
        bus

    let rec tickNTimes n bus =
        if n <= 0 then
            bus
        else
            let bus' = tick bus
            tickNTimes (n - 1) bus'

    let rec memWrite addr value bus =
        match addr with
        | addr when addr |> inRange Ram.Begin Ram.MirrorsEnd ->
            let mirrorDownAddr = addr &&& 0b0000_0111_1111_1111us
            bus.cpuVram[int mirrorDownAddr] <- value
            bus

        | 0x2000us ->
            let ppu = Ppu.writeToControlRegister value bus.ppu
            { bus with ppu = ppu }

        | 0x2001us ->
            let ppu = Ppu.writeToMaskRegister value bus.ppu
            { bus with ppu = ppu }

        | 0x2003us ->
            let ppu = Ppu.writeToOamAddress value bus.ppu
            { bus with ppu = ppu }

        | 0x2004us ->
            let ppu = Ppu.writeToOamData value bus.ppu
            { bus with ppu = ppu }

        | 0x2005us ->
            // printfn "WRITE Scroll Data: %02X" value
            let ppu = Ppu.writeToScrollRegister value bus.ppu
            { bus with ppu = ppu }

        | 0x2006us ->
            let ppu = Ppu.writeToAddressRegister value bus.ppu
            // printfn "WRITE Addr Reg: %02X" value
            { bus with ppu = ppu }

        | 0x2007us ->
            let ppu = Ppu.writeToDataRegister value bus.ppu
            // printfn "WRITE PPU Data: %02X" value
            { bus with ppu = ppu }

        | addr when addr |> inRange 0x2008us PpuRegisters.MirrorsEnd ->
            let mirrorDownAddr = addr &&& 0b0010_0000_0000_0111us
            bus |> memWrite mirrorDownAddr value

        // DMA 転送はページを指定して 0x100 バイト分転送する
        // https://www.nesdev.org/wiki/DMA
        | 0x4014us -> // TODO: OAM DMA の正確なティック加算処理
            let hi = uint16 value <<< 8
            let mutable data = Array.create 0x100 0uy

            for i in 0..0xFF do
                data[i] <- memRead (hi + uint16 i) bus |> fst

            let ppu = Ppu.writeToOamDma data bus.ppu
            let bus' = tickNTimes 114 bus
            { bus' with ppu = ppu }

        | 0x4016us ->
            let joy = bus.joy1 |> Joypad.write value
            { bus with joy1 = joy }

        | addr when addr |> inRange ApuRegisters.Begin ApuRegisters.End ->
            let apu = Apu.write addr value bus.apu
            { bus with apu = apu }

        | addr when addr |> inRange 0x6000us 0x7FFFus ->
            let mapper, _ = Mapper.writePrgRam addr value bus.cartridge

            {
                bus with
                    cartridge.mapper = mapper
                    ppu.cartridge.mapper = mapper
            }

        | addr when addr |> inRange PrgRom.Begin PrgRom.End ->
            let mapper, _ = Mapper.cpuWrite addr value bus.cartridge

            {
                bus with
                    cartridge.mapper = mapper
                    ppu.cartridge.mapper = mapper
            }

        | _ ->
            printfn "Invalid Memory write-access at: %04X" addr
            bus

    let memRead16 pos bus =
        let lo, bus1 = memRead pos bus
        let hi, bus2 = memRead (pos + 1us) bus1
        (uint16 hi <<< 8) ||| uint16 lo, bus2

    /// ゼロページの 16 ビットデータ読み込み（リトルエンディアンをデコード）
    let memRead16ZeroPage (pos: byte) bus =
        let loPos = pos |> uint16
        let hiPos = pos + 1uy |> uint16
        let lo, bus1 = memRead loPos bus
        let hi, bus2 = memRead hiPos bus1
        (uint16 hi <<< 8) ||| uint16 lo, bus2

    /// 16 ビットデータ読み込み（リトルエンディアンをデコード、ページ境界バグ対応）
    let memRead16Wrap pos bus =
        let lo, bus1 = memRead pos bus

        let hiPos =
            if pos &&& 0x00FFus = 0x00FFus then
                pos &&& 0xFF00us
            else
                pos + 1us

        let hi, bus2 = memRead hiPos bus1
        (uint16 hi <<< 8) ||| uint16 lo, bus2

    /// 16ビットデータ書き込み（リトルエンディアン化）
    let memWrite16 addr pos bus =
        let hi = pos >>> 8 |> byte
        let lo = pos &&& 0xFFus |> byte
        bus |> memWrite addr lo |> memWrite (addr + 1us) hi
