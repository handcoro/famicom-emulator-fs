module Bus

open Cartridge
open Ppu
open Joypad

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

type Bus = {
  cpuVram: byte array // 0x0000 - 0x1FFF
  rom: Rom
  ppu: NesPpu
  joy1: Joypad
  joy2: Joypad
  cycles: uint
  cyclePenalty: uint
}

let initialBus rom = {
  cpuVram = Array.create 0x2000 0uy
  rom = rom
  ppu = initialPpu rom
  joy1 = initialJoypad
  joy2 = initialJoypad
  cycles = 0u
  cyclePenalty = 0u
}

module Bus =
  let resetPenalty bus =
    { bus with cyclePenalty = 0u }
  let addCyclePenalty n bus =
    { bus with cyclePenalty = bus.cyclePenalty + n }

let readPrgRom bus addr = // PRG ROM の読み込み
  let addr' = addr - 0x8000us // 0x8000 - 0xFFFF の範囲を 0x0000 - 0x7FFF に変換
  let addr2 = if bus.rom.prgRom.Length = 0x4000 && addr' >= 0x4000us then addr' % 0x4000us else addr' // 16KB ROM の場合はミラーリング
  bus.rom.prgRom[int addr2]

let inline inRange startAddr endAddr addr =
  addr >= startAddr && addr <= endAddr
let rec memRead addr bus = 
  match addr with
  | addr when addr |> inRange Ram.Begin Ram.MirrorsEnd ->
    let mirrorDownAddr = addr &&& 0b0000_0111_1111_1111us
    bus.cpuVram[int mirrorDownAddr], bus

  | 0x2000us | 0x2001us | 0x2003us | 0x2005us | 0x2006us | 0x4014us ->
    // printfn "Attempt to read from write-only PPU address: %04X" addr
    0uy, bus

  | 0x2002us -> // TODO: Status
    let before, data = readFromStatusRegister bus.ppu.status
    // printfn "READ PPU Status: before: %02X after: %02X" before data
    before, { bus with ppu.status = data }

  | 0x2004us ->
    let data = readFromOamData bus.ppu
    data, bus

  | 0x2007us ->
    let data, ppu = readFromDataRegister bus.ppu
    data, { bus with ppu = ppu }

  | addr when addr |> inRange 0x2008us PpuRegisters.MirrorsEnd ->
    let mirrorDownAddr = addr &&& 0b0010_0000_0000_0111us
    memRead mirrorDownAddr bus

  | 0x4016us -> // TODO: Joypad
    let data, joy = readJoypad bus.joy1
    data, {bus with joy1 = joy}

  // | 0x4017us -> // TODO: Joypad
  //   let data, joy = readJoypad bus.joy2
  //   data, {bus with joy2 = joy}

  | addr when addr |> inRange ApuRegisters.Begin ApuRegisters.End ->
    // printfn "APU is not implemented yet. addr: %04X" addr
    0uy, bus

  | addr when addr |> inRange PrgRom.Begin PrgRom.End ->
    readPrgRom bus addr, bus

  | _ -> failwithf "Invalid Memory access at: %04X" addr

let rec memWrite addr value bus =
  match addr with
  | addr when addr |> inRange Ram.Begin Ram.MirrorsEnd ->
    let mirrorDownAddr = addr &&& 0b0000_0111_1111_1111us
    bus.cpuVram[int mirrorDownAddr] <- value
    bus

  | 0x2000us ->
    let ppu = writeToControlRegister value bus.ppu
    { bus with ppu = ppu }

  | 0x2001us ->
    let ppu = writeToMaskRegister value bus.ppu
    { bus with ppu = ppu}

  | 0x2003us ->
    let ppu = writeToOamAddress value bus.ppu
    { bus with ppu = ppu }

  | 0x2004us ->
    let ppu = writeToOamData value bus.ppu
    { bus with ppu = ppu }

  | 0x2005us -> // TODO: Scroll
    bus

  | 0x2006us ->
    let ppu = writeToAddressRegister value bus.ppu
    // printfn "WRITE Addr Reg: %02X" value
    { bus with ppu = ppu }

  | 0x2007us ->
    let ppu = writeToDataRegister value bus.ppu
    // printfn "WRITE PPU Data: %02X" value
    { bus with ppu = ppu }

  | addr when addr |> inRange 0x2008us PpuRegisters.MirrorsEnd ->
    let mirrorDownAddr = addr &&& 0b0010_0000_0000_0111us
    bus |> memWrite mirrorDownAddr value

  | 0x4014us -> // TODO: OAM DMA ティック加算処理
    let hi = uint16 value <<< 8
    let mutable data = Array.create 0x100 0uy
    for i in 0 .. 0xFF do
      data[i] <- memRead (hi + uint16 i) bus |> fst
    let ppu = writeToOamDma data bus.ppu
    { bus with ppu = ppu }

  | 0x4016us -> // TODO: Joypad
    let joy = bus.joy1 |> writeJoypad value
    { bus with joy1 = joy }

  | addr when addr |> inRange ApuRegisters.Begin ApuRegisters.End ->
    // printfn "APU is not implemented yet. addr: %04X" addr
    bus

  | addr when addr |> inRange PrgRom.Begin PrgRom.End -> // PRG ROM は書き込み禁止
    failwithf "Attempt to write to Cartridge Rom space. addr: %04X\n" addr

  | _ -> printfn "Invalid Memory write-access at: %04X" addr; bus

let memRead16 pos bus =
  let lo, bus1 = memRead pos bus
  let hi, bus2 = memRead (pos + 1us) bus1
  (uint16 hi <<< 8) ||| uint16 lo, bus2

let memRead16ZeroPage (pos: byte) bus = // ゼロページの 16 ビットデータ読み込み（リトルエンディアンをデコード）
  let loPos = pos |> uint16
  let hiPos = pos + 1uy |> uint16
  let lo, bus1 = memRead loPos bus
  let hi, bus2 = memRead hiPos bus1
  (uint16 hi <<< 8) ||| uint16 lo, bus2

let memRead16Wrap pos bus = // 16 ビットデータ読み込み（リトルエンディアンをデコード、ページ境界バグ対応）
  let lo, bus1 = memRead pos bus
  let hiPos = if pos &&& 0x00FFus = 0x00FFus then pos &&& 0xFF00us else pos + 1us
  let hi, bus2 = memRead hiPos bus1
  (uint16 hi <<< 8) ||| uint16 lo, bus2

let memWrite16 addr pos bus = // 16ビットデータ書き込み（リトルエンディアン化）
  let hi = pos >>> 8 |> byte
  let lo = pos &&& 0xFFus |> byte
  bus |> memWrite addr lo |> memWrite (addr + 1us) hi

let pollNmiStatus bus =
  bus.ppu.nmiInterrupt

let tick cycles bus =
  let cyc = bus.cycles + uint cycles
  let nmiBefore = bus.ppu.nmiInterrupt.IsSome
  let ppu' = ppuTick (cycles * 3u) bus.ppu
  let nmiAfter = ppu'.nmiInterrupt.IsSome

  // NMI の立ち上がり検出
  let nmiEdge = not nmiBefore && nmiAfter
  let bus' = { bus with cycles = cyc; ppu = ppu' }
  
  bus', if nmiEdge then Some ppu' else None
