module HamicomEmu.Trace

open HamicomEmu.Cpu.Instructions
open HamicomEmu.Cpu.Cpu
open HamicomEmu.Bus.Bus
open HamicomEmu.EmulatorCore.EmulatorCore

open Microsoft.FSharp.Reflection

let inline formatAddress pc mode (args: byte[]) =
    match mode with
    | Accumulator -> "A"
    | Immediate -> sprintf "#$%02X" args[0]
    | ZeroPage -> sprintf "$%02X" args[0]
    | ZeroPage_X -> sprintf "$%02X,X" args[0]
    | ZeroPage_Y -> sprintf "$%02X,Y" args[0]
    | Absolute -> sprintf "$%02X%02X" args[1] args[0]
    | Absolute_X -> sprintf "$%02X%02X,X" args[1] args[0]
    | Absolute_Y -> sprintf "$%02X%02X,Y" args[1] args[0]
    | Indirect_X -> sprintf "($%02X,X)" args[0]
    | Indirect_Y -> sprintf "($%02X),Y" args[0]
    | Indirect -> sprintf "($%02X%02X)" args[1] args[0]
    | Relative ->
        let pc' = pc + 2us |> int
        sprintf "$%04X" (args[0] |> sbyte |> int |> (+) pc' |> uint16)
    | Implied -> ""

let inline formatMemoryAccess cpu bus op mode (args: byte[]) =
    match mode with
    | ZeroPage ->
        let addr = args[0] |> uint16
        let value, _ = memRead addr bus
        sprintf "= %02X" value
    | ZeroPage_X ->
        let addr = args[0] + cpu.x |> uint16
        let value, _ = memRead addr bus
        sprintf "@ %02X = %02X" addr value
    | ZeroPage_Y ->
        let addr = args[0] + cpu.y |> uint16
        let value, _ = memRead addr bus
        sprintf "@ %02X = %02X" addr value
    | Absolute ->
        match op with
        | JMP
        | JSR -> ""
        | _ ->
            let hi = args[1] |> uint16
            let lo = args[0] |> uint16
            let addr = hi <<< 8 ||| lo
            let value, _ = memRead addr bus
            sprintf "= %02X" value
    | Absolute_X ->
        let hi = args[1] |> uint16
        let lo = args[0] |> uint16
        let addr = (hi <<< 8 ||| lo) + uint16 cpu.x
        let value, _ = memRead addr bus
        sprintf "@ %04X = %02X" addr value
    | Absolute_Y ->
        let hi = args[1] |> uint16
        let lo = args[0] |> uint16
        let addr = (hi <<< 8 ||| lo) + uint16 cpu.y
        let value, _ = memRead addr bus
        sprintf "@ %04X = %02X" addr value
    | Indirect_X ->
        let bpos = args[0]
        let ptr = bpos + cpu.x
        let addr, _ = memRead16ZeroPage ptr bus
        let value, _ = memRead addr bus
        sprintf "@ %02X = %04X = %02X" ptr addr value
    | Indirect_Y ->
        let bpos = args[0]
        let deRefBase, _ = memRead16ZeroPage bpos bus
        let deRef = deRefBase + (cpu.y |> uint16)
        let value, _ = memRead deRef bus
        sprintf "= %04X @ %04X = %02X" deRefBase deRef value
    | Indirect ->
        let hi = args[1] |> uint16
        let lo = args[0] |> uint16
        let bpos = hi <<< 8 ||| lo
        let addr, _ = memRead16Wrap bpos bus // JMP のページ境界バグ
        sprintf "= %04X" addr
    | _ -> ""

let inline formatInstructionBytes opcode (args: byte[]) =
    Array.concat [ [| opcode |]; args ]
    |> Array.map (fun b -> b.ToString("X2"))
    |> String.concat " "

let inline formatCpuStatus cpu =
    sprintf "A:%02X X:%02X Y:%02X P:%02X SP:%02X" cpu.a cpu.x cpu.y cpu.p cpu.sp

let inline getMnemonicName (x: 'T) =
    match FSharpValue.GetUnionFields(x, typeof<'T>) with
    | case, _ ->
        let name = case.Name

        if name.EndsWith "_" then
            "*" + name.TrimEnd '_'
        else
            " " + name

let inline readArgs bus start count =
    let folder (acc, b) i =
        let data, b' = memRead (start + uint16 i) b
        (data :: acc, b')

    let result, finalBus = List.fold folder ([], bus) [ 1..count ]
    (List.rev result |> List.toArray), finalBus

/// FIXME: トレース処理が重いので改善したい
let trace emu =
    let opcode, _ = memRead emu.cpu.pc emu.bus
    let op, mode, size, _, _ = decodeOpcode opcode
    let args, bus' = readArgs emu.bus emu.cpu.pc (int size - 1)
    let bin = formatInstructionBytes opcode args
    let mn = getMnemonicName op
    let addr = formatAddress emu.cpu.pc mode args
    let mem = formatMemoryAccess emu.cpu emu.bus op mode args
    let asm = [| mn; addr; mem |] |> String.concat " "

    let pc = sprintf "%04X" emu.cpu.pc
    let st = formatCpuStatus emu.cpu
    let ppu = sprintf "PPU:%3d,%3d" bus'.ppu.scanline bus'.ppu.cycle
    let cyc = sprintf "CYC:%d" emu.bus.cycleTotal
    sprintf "%-6s%-9s%-33s%-26s%-12s %s" pc bin asm st ppu cyc

let traceAndPrint emu = printfn "%s" (trace emu)
