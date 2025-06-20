﻿open HamicomEmu.Cpu
open HamicomEmu.Bus
open HamicomEmu.Cartridge
open Tests
open HamicomEmu.Ppu.Screen
open HamicomEmu.Ppu.Renderer
open HamicomEmu.Apu
open HamicomEmu.Trace
open HamicomEmu.Platform.AudioEngine
open Joypad

open System
open System.IO
open FsToolkit.ErrorHandling
open Expecto

// MonoGame メインゲームクラス
open Microsoft.Xna.Framework
open Microsoft.Xna.Framework.Graphics
open Microsoft.Xna.Framework.Input

let handleJoypadInput (ks: KeyboardState) joy =
  let keyMap = [
    Keys.Right, JoyButton.Right
    Keys.Left,  JoyButton.Left
    Keys.Down,  JoyButton.Down
    Keys.Up,    JoyButton.Up
    Keys.Enter, JoyButton.Start
    Keys.Space, JoyButton.Select
    Keys.S,     JoyButton.B
    Keys.A,     JoyButton.A
  ]
  
  keyMap
  |> List.fold (fun accJoy (key, button) ->
    let isDown = ks.IsKeyDown key
    accJoy |> setButtonPressed button isDown
    ) joy

let loadRom path =
  try
    Ok (File.ReadAllBytes(path))
  with
  | e -> Error $"Failed to load ROM: {e.Message}"

let frameToTexture (graphics: GraphicsDevice) (frame: Frame) : Texture2D =
  let tex = new Texture2D(graphics, Frame.Width, Frame.Height)
  let colorData =
    frame.data
    |> Array.map (fun (r, g, b) -> Color(int r, int g, int b))
  tex.SetData(colorData)
  tex

type basicNesGame(loadedRom) as this =

  inherit Game()
  let scale = 4
  let sampleRate = 44100
  let mutable prevT = 0.0 // 音声の時間差分計算用
  let graphics = new GraphicsDeviceManager(this)
  let mutable spriteBatch = Unchecked.defaultof<SpriteBatch>
  let mutable texture = Unchecked.defaultof<Texture2D>
  let mutable audioEngine = AudioEngine(sampleRate)

  let raw = loadedRom
  let parsed = raw |> Result.bind parseRom
  let mutable cpu = Cpu.initial
  let mutable bus = Unchecked.defaultof<Bus.BusState>
  
  let mutable frame = initialFrame

  do
    match parsed with
    | Ok rom ->
      bus <- Bus.initial rom
      let cpu', bus' = Cpu.reset cpu bus
      cpu <- cpu'
      bus <- bus'
    | Error e -> failwith $"Failed to parse ROM: {e}"

    graphics.PreferredBackBufferWidth <- Frame.Width * scale
    graphics.PreferredBackBufferHeight <- Frame.Height * scale
    graphics.ApplyChanges()

  override _.Initialize() =
    this.IsFixedTimeStep <- true
    this.TargetElapsedTime <- TimeSpan.FromSeconds(1.0 / 60.0) // フレームの更新間隔
    base.Initialize()

  override _.LoadContent() =
    spriteBatch <- new SpriteBatch(this.GraphicsDevice)
    texture <- new Texture2D(this.GraphicsDevice, Frame.Width, Frame.Height)
    base.LoadContent()

  override _.Draw(gameTime) =
    this.GraphicsDevice.Clear(Color.Black)
    spriteBatch.Begin(samplerState = SamplerState.PointClamp)
    frame <- renderScanlineBased bus.ppu frame
    let colorData =
      frame.data |> Array.map (fun (r, g, b) -> Color(int r, int g, int b))
    texture.SetData(colorData)
    let destRect = Rectangle(0, 0, Frame.Width * scale, Frame.Height * scale)
    spriteBatch.Draw(texture, destRect, Color.White)
    spriteBatch.End()
    base.Draw(gameTime)

  override _.Update(gameTime) =
    if Keyboard.GetState().IsKeyDown(Keys.Escape) then this.Exit()
    let joy = bus.joy1 |> handleJoypadInput (Keyboard.GetState())
    bus <- {bus with joy1 = joy }

    let sampleRate      = 44100
    let cpuClock        = Constants.cpuClockNTSC
    let cyclesPerSample = cpuClock / float sampleRate   // ≒ 40.584
    let samplesPerFrame = sampleRate / 60               // = 735

    // 1フレーム分のサンプルを作るバッファ
    let samples = ResizeArray<float32>()

    for _ in 1 .. samplesPerFrame do
      // 1フレームに達するまで CPU を進める
      let mutable cyclesRemain = cyclesPerSample
      while cyclesRemain > 0.0 do
        let cpu', bus', used = Cpu.step cpu bus   // ← step が消費サイクル数を返すように
        cpu  <- cpu'
        bus  <- bus'
        cyclesRemain <- cyclesRemain - float used

      // APU から 1 サンプル取り出す
      let sample, apu' = Apu.mix (1.0 / float sampleRate) bus.apu
      bus <- { bus with apu = apu' }

      samples.Add(sample)

    // AudioEngine へ一括送信
    audioEngine.Submit(samples |> Seq.toList)


    base.Update(gameTime)

/// https://www.nesworld.com/article.php?system=nes&data=neshomebrew
let defaultRom = "roms/Alter_Ego.nes"

[<EntryPoint>]
let main argv =
  if argv |> Array.exists ((=) "--test") then
    // --test を除いた引数だけ Expecto に渡す
    let filteredArgs = argv |> Array.filter (fun a -> a <> "--test")
    runTestsWithArgs defaultConfig filteredArgs Tests.tests
  else
    match argv |> Array.tryHead with
    | Some romPath ->
      match loadRom romPath with
      | Ok rom ->
        use game = new basicNesGame(loadRom romPath)
        game.Run()
        0
      | Error msg ->
        use game = new basicNesGame (loadRom defaultRom)
        game.Run()
        0
    | None ->
    use game = new basicNesGame (loadRom defaultRom)
    game.Run()
    0
