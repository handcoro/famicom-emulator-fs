﻿namespace HamicomEmu.Cpu

module Instructions =

    type mnemonics =
        | ADC | AND | ASL
        | BCC | BCS | BEQ | BIT | BMI | BNE | BPL | BRK | BVC | BVS
        | CLC | CLD | CLI | CLV | CMP | CPX | CPY
        | DEC | DEX | DEY
        | EOR
        | INC | INX | INY
        | JMP | JSR
        | LDA | LDX | LDY | LSR
        | NOP
        | ORA
        | PHA | PHP | PLA | PLP
        | ROL | ROR | RTI | RTS
        | SBC | SEC | SED | SEI | STA | STX | STY
        | TAX | TAY | TSX | TXA | TXS | TYA

// 非公式命令
        | ALR_ | ANC_ | ARR_ | ATX_ | AXA_ | AXS_
        | DCP_ | DOP_
        | ISC_
        | KIL_
        | LAR_ | LAX_
        | NOP_
        | RLA_ | RRA_
        | SAX_ | SBC_ | SLO_ | SRE_ | SXA_ | SYA_
        | TOP_
        | XAA_ | XAS_

    type addressingMode =
        | Absolute
        | Absolute_X
        | Absolute_Y
        | Accumulator
        | Immediate
        | Implied
        | Indirect
        | Indirect_X
        | Indirect_Y
        | Relative
        | ZeroPage
        | ZeroPage_X
        | ZeroPage_Y

    /// 命令情報テーブル
    // コード, (ニーモニック, アドレッシングモード, バイト長, サイクル数, 追加サイクルが発生しうるかのフラグ)
    let opcodeTable : Map<byte, mnemonics * addressingMode * uint16 * uint * bool> =
        Map [
            0x00uy, (BRK, Implied, 1us, 7u, false)
            0x01uy, (ORA, Indirect_X, 2us, 6u, false)
            0x02uy, (KIL_, Implied, 1us, 0u, false)
            0x03uy, (SLO_, Indirect_X, 2us, 8u, false)
            0x04uy, (DOP_, ZeroPage, 2us, 3u, false)
            0x05uy, (ORA, ZeroPage, 2us, 3u, false)
            0x06uy, (ASL, ZeroPage, 2us, 5u, false)
            0x07uy, (SLO_, ZeroPage, 2us, 5u, false)
            0x08uy, (PHP, Implied, 1us, 3u, false)
            0x09uy, (ORA, Immediate, 2us, 2u, false)
            0x0Auy, (ASL, Accumulator, 1us, 2u, false)
            0x0Buy, (ANC_, Immediate, 2us, 2u, false)
            0x0Cuy, (TOP_, Absolute, 3us, 4u, false)
            0x0Duy, (ORA, Absolute, 3us, 4u, false)
            0x0Euy, (ASL, Absolute, 3us, 6u, false)
            0x0Fuy, (SLO_, Absolute, 3us, 6u, false)
            0x10uy, (BPL, Relative, 2us, 2u, true)
            0x11uy, (ORA, Indirect_Y, 2us, 5u, true)
            0x12uy, (KIL_, Implied, 1us, 0u, false)
            0x13uy, (SLO_, Indirect_Y, 2us, 8u, false)
            0x14uy, (DOP_, ZeroPage_X, 2us, 4u, false)
            0x15uy, (ORA, ZeroPage_X, 2us, 4u, false)
            0x16uy, (ASL, ZeroPage_X, 2us, 6u, false)
            0x17uy, (SLO_, ZeroPage_X, 2us, 6u, false)
            0x18uy, (CLC, Implied, 1us, 2u, false)
            0x19uy, (ORA, Absolute_Y, 3us, 4u, true)
            0x1Auy, (NOP_, Implied, 1us, 2u, false)
            0x1Buy, (SLO_, Absolute_Y, 3us, 7u, false)
            0x1Cuy, (TOP_, Absolute_X, 3us, 4u, true)
            0x1Duy, (ORA, Absolute_X, 3us, 4u, true)
            0x1Euy, (ASL, Absolute_X, 3us, 7u, false)
            0x1Fuy, (SLO_, Absolute_X, 3us, 7u, false)
            0x20uy, (JSR, Absolute, 3us, 6u, false)
            0x21uy, (AND, Indirect_X, 2us, 6u, false)
            0x22uy, (KIL_, Implied, 1us, 0u, false)
            0x23uy, (RLA_, Indirect_X, 2us, 8u, false)
            0x24uy, (BIT, ZeroPage, 2us, 3u, false)
            0x25uy, (AND, ZeroPage, 2us, 3u, false)
            0x26uy, (ROL, ZeroPage, 2us, 5u, false)
            0x27uy, (RLA_, ZeroPage, 2us, 5u, false)
            0x28uy, (PLP, Implied, 1us, 4u, false)
            0x29uy, (AND, Immediate, 2us, 2u, false)
            0x2Auy, (ROL, Accumulator, 1us, 2u, false)
            0x2Buy, (ANC_, Immediate, 2us, 2u, false)
            0x2Cuy, (BIT, Absolute, 3us, 4u, false)
            0x2Duy, (AND, Absolute, 3us, 4u, false)
            0x2Euy, (ROL, Absolute, 3us, 6u, false)
            0x2Fuy, (RLA_, Absolute, 3us, 6u, false)
            0x30uy, (BMI, Relative, 2us, 2u, true)
            0x31uy, (AND, Indirect_Y, 2us, 5u, true)
            0x32uy, (KIL_, Implied, 1us, 0u, false)
            0x33uy, (RLA_, Indirect_Y, 2us, 8u, false)
            0x34uy, (DOP_, ZeroPage_X, 2us, 4u, false)
            0x35uy, (AND, ZeroPage_X, 2us, 4u, false)
            0x36uy, (ROL, ZeroPage_X, 2us, 6u, false)
            0x37uy, (RLA_, ZeroPage_X, 2us, 6u, false)
            0x38uy, (SEC, Implied, 1us, 2u, false)
            0x39uy, (AND, Absolute_Y, 3us, 4u, true)
            0x3Auy, (NOP_, Implied, 1us, 2u, false)
            0x3Buy, (RLA_, Absolute_Y, 3us, 7u, false)
            0x3Cuy, (TOP_, Absolute_X, 3us, 4u, true)
            0x3Duy, (AND, Absolute_X, 3us, 4u, true)
            0x3Euy, (ROL, Absolute_X, 3us, 7u, false)
            0x3Fuy, (RLA_, Absolute_X, 3us, 7u, false)
            0x40uy, (RTI, Implied, 1us, 6u, false)
            0x41uy, (EOR, Indirect_X, 2us, 6u, false)
            0x42uy, (KIL_, Implied, 1us, 0u, false)
            0x43uy, (SRE_, Indirect_X, 2us, 8u, false)
            0x44uy, (DOP_, ZeroPage, 2us, 3u, false)
            0x45uy, (EOR, ZeroPage, 2us, 3u, false)
            0x46uy, (LSR, ZeroPage, 2us, 5u, false)
            0x47uy, (SRE_, ZeroPage, 2us, 5u, false)
            0x48uy, (PHA, Implied, 1us, 3u, false)
            0x49uy, (EOR, Immediate, 2us, 2u, false)
            0x4Auy, (LSR, Accumulator, 1us, 2u, false)
            0x4Buy, (ALR_, Immediate, 2us, 2u, false)
            0x4Cuy, (JMP, Absolute, 3us, 3u, false)
            0x4Duy, (EOR, Absolute, 3us, 4u, false)
            0x4Euy, (LSR, Absolute, 3us, 6u, false)
            0x4Fuy, (SRE_, Absolute, 3us, 6u, false)
            0x50uy, (BVC, Relative, 2us, 2u, true)
            0x51uy, (EOR, Indirect_Y, 2us, 5u, true)
            0x52uy, (KIL_, Implied, 1us, 0u, false)
            0x53uy, (SRE_, Indirect_Y, 2us, 8u, false)
            0x54uy, (DOP_, ZeroPage_X, 2us, 4u, false)
            0x55uy, (EOR, ZeroPage_X, 2us, 4u, false)
            0x56uy, (LSR, ZeroPage_X, 2us, 6u, false)
            0x57uy, (SRE_, ZeroPage_X, 2us, 6u, false)
            0x58uy, (CLI, Implied, 1us, 2u, false)
            0x59uy, (EOR, Absolute_Y, 3us, 4u, true)
            0x5Auy, (NOP_, Implied, 1us, 2u, false)
            0x5Buy, (SRE_, Absolute_Y, 3us, 7u, false)
            0x5Cuy, (TOP_, Absolute_X, 3us, 4u, true)
            0x5Duy, (EOR, Absolute_X, 3us, 4u, true)
            0x5Euy, (LSR, Absolute_X, 3us, 7u, false)
            0x5Fuy, (SRE_, Absolute_X, 3us, 7u, false)
            0x60uy, (RTS, Implied, 1us, 6u, false)
            0x61uy, (ADC, Indirect_X, 2us, 6u, false)
            0x62uy, (KIL_, Implied, 1us, 0u, false)
            0x63uy, (RRA_, Indirect_X, 2us, 8u, false)
            0x64uy, (DOP_, ZeroPage, 2us, 3u, false)
            0x65uy, (ADC, ZeroPage, 2us, 3u, false)
            0x66uy, (ROR, ZeroPage, 2us, 5u, false)
            0x67uy, (RRA_, ZeroPage, 2us, 5u, false)
            0x68uy, (PLA, Implied, 1us, 4u, false)
            0x69uy, (ADC, Immediate, 2us, 2u, false)
            0x6Auy, (ROR, Accumulator, 1us, 2u, false)
            0x6Buy, (ARR_, Immediate, 2us, 2u, false)
            0x6Cuy, (JMP, Indirect, 3us, 5u, false)
            0x6Duy, (ADC, Absolute, 3us, 4u, false)
            0x6Euy, (ROR, Absolute, 3us, 6u, false)
            0x6Fuy, (RRA_, Absolute, 3us, 6u, false)
            0x70uy, (BVS, Relative, 2us, 2u, true)
            0x71uy, (ADC, Indirect_Y, 2us, 5u, true)
            0x72uy, (KIL_, Implied, 1us, 0u, false)
            0x73uy, (RRA_, Indirect_Y, 2us, 8u, false)
            0x74uy, (DOP_, ZeroPage_X, 2us, 4u, false)
            0x75uy, (ADC, ZeroPage_X, 2us, 4u, false)
            0x76uy, (ROR, ZeroPage_X, 2us, 6u, false)
            0x77uy, (RRA_, ZeroPage_X, 2us, 6u, false)
            0x78uy, (SEI, Implied, 1us, 2u, false)
            0x79uy, (ADC, Absolute_Y, 3us, 4u, true)
            0x7Auy, (NOP_, Implied, 1us, 2u, false)
            0x7Buy, (RRA_, Absolute_Y, 3us, 7u, false)
            0x7Cuy, (TOP_, Absolute_X, 3us, 4u, true)
            0x7Duy, (ADC, Absolute_X, 3us, 4u, true)
            0x7Euy, (ROR, Absolute_X, 3us, 7u, false)
            0x7Fuy, (RRA_, Absolute_X, 3us, 7u, false)
            0x80uy, (DOP_, Immediate, 2us, 2u, false)
            0x81uy, (STA, Indirect_X, 2us, 6u, false)
            0x82uy, (DOP_, Immediate, 2us, 2u, false)
            0x83uy, (SAX_, Indirect_X, 2us, 6u, false)
            0x84uy, (STY, ZeroPage, 2us, 3u, false)
            0x85uy, (STA, ZeroPage, 2us, 3u, false)
            0x86uy, (STX, ZeroPage, 2us, 3u, false)
            0x87uy, (SAX_, ZeroPage, 2us, 3u, false)
            0x88uy, (DEY, Implied, 1us, 2u, false)
            0x89uy, (DOP_, Immediate, 2us, 2u, false)
            0x8Auy, (TXA, Implied, 1us, 2u, false)
            0x8Buy, (XAA_, Immediate, 2us, 2u, false)
            0x8Cuy, (STY, Absolute, 3us, 4u, false)
            0x8Duy, (STA, Absolute, 3us, 4u, false)
            0x8Euy, (STX, Absolute, 3us, 4u, false)
            0x8Fuy, (SAX_, Absolute, 3us, 4u, false)
            0x90uy, (BCC, Relative, 2us, 2u, true)
            0x91uy, (STA, Indirect_Y, 2us, 6u, false)
            0x92uy, (KIL_, Implied, 1us, 0u, false)
            0x93uy, (AXA_, Indirect_Y, 2us, 6u, false)
            0x94uy, (STY, ZeroPage_X, 2us, 4u, false)
            0x95uy, (STA, ZeroPage_X, 2us, 4u, false)
            0x96uy, (STX, ZeroPage_Y, 2us, 4u, false)
            0x97uy, (SAX_, ZeroPage_Y, 2us, 4u, false)
            0x98uy, (TYA, Implied, 1us, 2u, false)
            0x99uy, (STA, Absolute_Y, 3us, 5u, false)
            0x9Auy, (TXS, Implied, 1us, 2u, false)
            0x9Buy, (XAS_, Absolute_Y, 3us, 5u, false)
            0x9Cuy, (SYA_, Absolute_X, 3us, 5u, false)
            0x9Duy, (STA, Absolute_X, 3us, 5u, false)
            0x9Euy, (SXA_, Absolute_Y, 3us, 5u, false)
            0x9Fuy, (AXA_, Absolute_Y, 3us, 5u, false)
            0xA0uy, (LDY, Immediate, 2us, 2u, false)
            0xA1uy, (LDA, Indirect_X, 2us, 6u, false)
            0xA2uy, (LDX, Immediate, 2us, 2u, false)
            0xA3uy, (LAX_, Indirect_X, 2us, 6u, false)
            0xA4uy, (LDY, ZeroPage, 2us, 3u, false)
            0xA5uy, (LDA, ZeroPage, 2us, 3u, false)
            0xA6uy, (LDX, ZeroPage, 2us, 3u, false)
            0xA7uy, (LAX_, ZeroPage, 2us, 3u, false)
            0xA8uy, (TAY, Implied, 1us, 2u, false)
            0xA9uy, (LDA, Immediate, 2us, 2u, false)
            0xAAuy, (TAX, Implied, 1us, 2u, false)
            0xABuy, (ATX_, Immediate, 2us, 2u, false)
            0xACuy, (LDY, Absolute, 3us, 4u, false)
            0xADuy, (LDA, Absolute, 3us, 4u, false)
            0xAEuy, (LDX, Absolute, 3us, 4u, false)
            0xAFuy, (LAX_, Absolute, 3us, 4u, false)
            0xB0uy, (BCS, Relative, 2us, 2u, true)
            0xB1uy, (LDA, Indirect_Y, 2us, 5u, true)
            0xB2uy, (KIL_, Implied, 1us, 0u, false)
            0xB3uy, (LAX_, Indirect_Y, 2us, 5u, true)
            0xB4uy, (LDY, ZeroPage_X, 2us, 4u, false)
            0xB5uy, (LDA, ZeroPage_X, 2us, 4u, false)
            0xB6uy, (LDX, ZeroPage_Y, 2us, 4u, false)
            0xB7uy, (LAX_, ZeroPage_Y, 2us, 4u, false)
            0xB8uy, (CLV, Implied, 1us, 2u, false)
            0xB9uy, (LDA, Absolute_Y, 3us, 4u, true)
            0xBAuy, (TSX, Implied, 1us, 2u, false)
            0xBBuy, (LAR_, Absolute_Y, 3us, 4u, true)
            0xBCuy, (LDY, Absolute_X, 3us, 4u, true)
            0xBDuy, (LDA, Absolute_X, 3us, 4u, true)
            0xBEuy, (LDX, Absolute_Y, 3us, 4u, true)
            0xBFuy, (LAX_, Absolute_Y, 3us, 4u, true)
            0xC0uy, (CPY, Immediate, 2us, 2u, false)
            0xC1uy, (CMP, Indirect_X, 2us, 6u, false)
            0xC2uy, (DOP_, Immediate, 2us, 2u, false)
            0xC3uy, (DCP_, Indirect_X, 2us, 8u, false)
            0xC4uy, (CPY, ZeroPage, 2us, 3u, false)
            0xC5uy, (CMP, ZeroPage, 2us, 3u, false)
            0xC6uy, (DEC, ZeroPage, 2us, 5u, false)
            0xC7uy, (DCP_, ZeroPage, 2us, 5u, false)
            0xC8uy, (INY, Implied, 1us, 2u, false)
            0xC9uy, (CMP, Immediate, 2us, 2u, false)
            0xCAuy, (DEX, Implied, 1us, 2u, false)
            0xCBuy, (AXS_, Immediate, 2us, 2u, false)
            0xCCuy, (CPY, Absolute, 3us, 4u, false)
            0xCDuy, (CMP, Absolute, 3us, 4u, false)
            0xCEuy, (DEC, Absolute, 3us, 6u, false)
            0xCFuy, (DCP_, Absolute, 3us, 6u, false)
            0xD0uy, (BNE, Relative, 2us, 2u, true)
            0xD1uy, (CMP, Indirect_Y, 2us, 5u, true)
            0xD2uy, (KIL_, Implied, 1us, 0u, false)
            0xD3uy, (DCP_, Indirect_Y, 2us, 8u, false)
            0xD4uy, (DOP_, ZeroPage_X, 2us, 4u, false)
            0xD5uy, (CMP, ZeroPage_X, 2us, 4u, false)
            0xD6uy, (DEC, ZeroPage_X, 2us, 6u, false)
            0xD7uy, (DCP_, ZeroPage_X, 2us, 6u, false)
            0xD8uy, (CLD, Implied, 1us, 2u, false)
            0xD9uy, (CMP, Absolute_Y, 3us, 4u, true)
            0xDAuy, (NOP_, Implied, 1us, 2u, false)
            0xDBuy, (DCP_, Absolute_Y, 3us, 7u, false)
            0xDCuy, (TOP_, Absolute_X, 3us, 4u, true)
            0xDDuy, (CMP, Absolute_X, 3us, 4u, true)
            0xDEuy, (DEC, Absolute_X, 3us, 7u, false)
            0xDFuy, (DCP_, Absolute_X, 3us, 7u, false)
            0xE0uy, (CPX, Immediate, 2us, 2u, false)
            0xE1uy, (SBC, Indirect_X, 2us, 6u, false)
            0xE2uy, (DOP_, Immediate, 2us, 2u, false)
            0xE3uy, (ISC_, Indirect_X, 2us, 8u, false)
            0xE4uy, (CPX, ZeroPage, 2us, 3u, false)
            0xE5uy, (SBC, ZeroPage, 2us, 3u, false)
            0xE6uy, (INC, ZeroPage, 2us, 5u, false)
            0xE7uy, (ISC_, ZeroPage, 2us, 5u, false)
            0xE8uy, (INX, Implied, 1us, 2u, false)
            0xE9uy, (SBC, Immediate, 2us, 2u, false)
            0xEAuy, (NOP, Implied, 1us, 2u, false)
            0xEBuy, (SBC_, Immediate, 2us, 2u, false)
            0xECuy, (CPX, Absolute, 3us, 4u, false)
            0xEDuy, (SBC, Absolute, 3us, 4u, false)
            0xEEuy, (INC, Absolute, 3us, 6u, false)
            0xEFuy, (ISC_, Absolute, 3us, 6u, false)
            0xF0uy, (BEQ, Relative, 2us, 2u, true)
            0xF1uy, (SBC, Indirect_Y, 2us, 5u, true)
            0xF2uy, (KIL_, Implied, 1us, 0u, false)
            0xF3uy, (ISC_, Indirect_Y, 2us, 8u, false)
            0xF4uy, (DOP_, ZeroPage_X, 2us, 4u, false)
            0xF5uy, (SBC, ZeroPage_X, 2us, 4u, false)
            0xF6uy, (INC, ZeroPage_X, 2us, 6u, false)
            0xF7uy, (ISC_, ZeroPage_X, 2us, 6u, false)
            0xF8uy, (SED, Implied, 1us, 2u, false)
            0xF9uy, (SBC, Absolute_Y, 3us, 4u, true)
            0xFAuy, (NOP_, Implied, 1us, 2u, false)
            0xFBuy, (ISC_, Absolute_Y, 3us, 7u, false)
            0xFCuy, (TOP_, Absolute_X, 3us, 4u, true)
            0xFDuy, (SBC, Absolute_X, 3us, 4u, true)
            0xFEuy, (INC, Absolute_X, 3us, 7u, false)
            0xFFuy, (ISC_, Absolute_X, 3us, 7u, false)
        ]

    /// オペコードをデコードして命令情報を取得（存在しない場合は例外）
    let decodeOpcode (opcode: byte) =
        opcodeTable[opcode]

