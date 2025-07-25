namespace HamicomEmu.Input

module Joypad =

    open HamicomEmu.Input.Types

    module Button =
        let right = 0b1000_0000uy
        let left = 0b0100_0000uy
        let down = 0b0010_0000uy
        let up = 0b0001_0000uy
        let start = 0b0000_1000uy
        let select = 0b0000_0100uy
        let b = 0b0000_0010uy
        let a = 0b0000_0001uy

    let init = {
        strobe = false
        buttonIdx = 0
        buttonStatus = 0uy
    }

    /// strobe の書き込み
    /// TODO: 拡張ポート対応
    let write value joy =
        let strobe = if value &&& 1uy <> 0uy then true else false
        let idx = if strobe then 0 else joy.buttonIdx // strobe がセットされていれば読み取りは A ボタン固定

        {
            joy with
                strobe = strobe
                buttonIdx = idx
        }

    /// 1 ビットずつ入力状態を返す
    let read joy =
        let idx = joy.buttonIdx

        if idx > 7 then
            1uy, joy // 8 回読んだら以降は常に 1 を返す仕様
        else
            let bit = (joy.buttonStatus >>> idx) &&& 1uy
            let idx' = if not joy.strobe && idx <= 7 then idx + 1 else idx
            bit, { joy with buttonIdx = idx' }

    /// 入力状態の更新
    /// 本来は strobe をセットした瞬間のジョイパッドの入力データが書き込まれ
    /// クリアしたらそのデータが読み込み可能になる
    let setButtonPressed button isPressed joy =
        let newStatus =
            if isPressed then
                joy.buttonStatus ||| button
            else
                joy.buttonStatus &&& (~~~button)

        { joy with buttonStatus = newStatus }

    let mergeStates a b =
        { a with buttonStatus = a.buttonStatus ||| b.buttonStatus }
