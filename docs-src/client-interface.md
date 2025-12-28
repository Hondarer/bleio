# BleioClient インターフェース仕様

BleioClient は、ESP32 ベースの BLEIO デバイスを制御する C# クライアントライブラリです。BLE (Bluetooth Low Energy) を使用して GPIO の制御、PWM 出力、ADC 入力などを行います。

## 概要

BleioClient は、フラットで直感的な API を提供し、以下の特徴を持ちます。

- **一貫性のある命名規則**: すべての設定メソッドが `SetXxxAsync` の形式
- **型安全性**: enum や record を使用してパラメータを明確化
- **シンプルさ**: 通常操作はフラットでシンプル、バッチ操作は型安全なコマンドビルダーを提供

## クイックスタート

```csharp
var client = new BleioClient();
await client.ConnectAsync(deviceName: "BLEIO");

// 通常操作: シンプルなフラット API
await client.SetOutputAsync(pin: 2, OutputKind.High);
await client.SetInputAsync(pin: 34, InputConfig.PullUp, latchMode: LatchMode.Low);
await client.SetPwmAsync(pin: 2, dutyCycle: 0.5, PwmFrequency.Freq10kHz);

// 一括操作: 型安全なコマンドビルダー
await client.SendBulkAsync(
    Command.SetOutput(pin: 2, OutputKind.High),
    Command.SetOutput(pin: 12, OutputKind.Low),
    Command.SetInput(pin: 34, InputConfig.PullUp, latchMode: LatchMode.Low),
    Command.SetPwm(pin: 25, dutyCycle: 0.5, PwmFrequency.Freq10kHz),
    Command.SetDisconnectBehavior(pin: 2, DisconnectBehavior.SetLow)
);

// 読み取り
bool? state = await client.ReadInputAsync(pin: 34);
AdcReading? reading = await client.ReadAdcAsync(pin: 32);
```

### コマンドビルダー

```csharp
public static class Command
{
    public static GpioCommand SetOutput(byte pin, OutputKind outputKind) => ...; // ブリンクは OutputKind の 1 形態とする
    public static GpioCommand SetPwm(byte pin, double dutyCycle, PwmFrequency frequency) => ...;
    public static GpioCommand SetInput(byte pin, InputConfig config, LatchMode latchMode = LatchMode.None) => ...;
    public static GpioCommand EnableAdc(byte pin, AdcAttenuation attenuation) => ...;
    public static GpioCommand SetDisconnectBehavior(byte pin, DisconnectBehavior behavior) => ...;
}
```

## API リファレンス

### BleioClient クラス

```csharp
namespace Hondarersoft.Bleio
{
    public class BleioClient : IDisposable
    {
        // 接続管理
        public Task<bool> ConnectAsync(string? deviceName = null, string serviceUuid = ServiceUuid, int timeoutMs = 8000);
        public Task<bool> ConnectByMacAddressAsync(string macAddress);
        public Task<bool> ConnectByMacAddressAsync(ulong bluetoothAddress);

        // 出力設定 (点滅設定を含む)
        public Task SetOutputAsync(byte pin, OutputKind outputKind);
        public Task SetPwmAsync(byte pin, double dutyCycle, PwmFrequency frequency = PwmFrequency.Freq1kHz);

        // 入力設定
        public Task SetInputAsync(byte pin, InputConfig config, LatchMode latchMode = LatchMode.None);

        // ADC 設定
        public Task EnableAdcAsync(byte pin, AdcAttenuation attenuation = AdcAttenuation.Atten11dB);
        public Task DisableAdcAsync(byte pin);

        // 切断時動作設定
        public Task SetDisconnectBehaviorAsync(byte pin, DisconnectBehavior behavior);

        // 読み取り
        public Task<bool?> ReadInputAsync(byte pin);
        public Task<(byte Pin, bool State)[]> ReadAllInputAsync(); // すべての入力ピンが入力以外の場合、空配列
        public Task<AdcReading?> ReadAdcAsync(byte pin);
        public Task<AdcReading[]> ReadAllAdcAsync(); // すべての入力ピンが ADC 未設定の場合、空配列

        // 一括操作
        public Task SendBulkAsync(params GpioCommand[] commands);

        public void Dispose();
    }
}
```

### Command ビルダー

```csharp
namespace Hondarersoft.Bleio
{
    public static class Command
    {
        // 出力コマンド (点滅コマンドを含む)
        public static GpioCommand SetOutput(byte pin, OutputKind outputKind);

        // PWM コマンド
        public static GpioCommand SetPwm(byte pin, double dutyCycle, PwmFrequency frequency = PwmFrequency.Freq1kHz);

        // 入力コマンド
        public static GpioCommand SetInput(byte pin, InputConfig config, LatchMode latchMode = LatchMode.None);

        // ADC コマンド
        public static GpioCommand EnableAdc(byte pin, AdcAttenuation attenuation = AdcAttenuation.Atten11dB);
        public static GpioCommand DisableAdc(byte pin);

        // 切断時動作コマンド
        public static GpioCommand SetDisconnectBehavior(byte pin, DisconnectBehavior behavior);
    }
}
```

### 内部実装

コマンドビルダーは、内部的に以下の具象クラスを生成します。

```csharp
// 内部実装 (ユーザーには公開しない)
internal record SetOutputCommand(byte Pin, OutputKind outputKind) : GpioCommand(Pin);
internal record SetPwmCommand(byte Pin, byte DutyCycle, PwmFrequency Frequency) : GpioCommand(Pin);
internal record SetInputCommand(byte Pin, InputConfig Config, LatchMode LatchMode) : GpioCommand(Pin);
internal record EnableAdcCommand(byte Pin, AdcAttenuation Attenuation) : GpioCommand(Pin);
internal record DisableAdcCommand(byte Pin) : GpioCommand(Pin);
internal record SetDisconnectBehaviorCommand(byte Pin, DisconnectBehavior Behavior) : GpioCommand(Pin);

// 各コマンドは ToRawCommand() メソッドでプロトコルレベルの表現に変換
internal abstract record GpioCommand(byte Pin)
{
    internal abstract RawGpioCommand ToRawCommand();
}

// プロトコルレベルの表現
internal record RawGpioCommand(byte Pin, byte Command, byte Param1, byte Param2);
```

## 使用例

### 基本的な使用例

```csharp
using Hondarersoft.Bleio;

var client = new BleioClient();

// デバイスに接続
await client.ConnectAsync(deviceName: "BLEIO");

// GPIO2 を HIGH に設定
await client.SetOutputAsync(pin: 2, OutputKind.High);

// GPIO12 を LOW に設定
await client.SetOutputAsync(pin: 12, OutputKind.Low);

// GPIO2 を 250ms 点滅に設定
await client.SetOutputAsync(pin: 2, OutputKind.Blink250ms);

// GPIO2 を 500ms 点滅に設定
await client.SetOutputAsync(pin: 2, OutputKind.Blink500ms);

// GPIO34 を入力モード (プルアップ、LOW ラッチ) に設定
await client.SetInputAsync(pin: 34, InputConfig.PullUp, latchMode: LatchMode.Low);

// GPIO25 を PWM 出力 (50%, 10kHz) に設定
await client.SetPwmAsync(pin: 25, dutyCycle: 0.5, PwmFrequency.Freq10kHz);

// GPIO32 を ADC 入力 (11dB 減衰) に設定
await client.EnableAdcAsync(pin: 32, AdcAttenuation.Atten11dB);

// GPIO2 を切断時に LOW に設定
await client.SetDisconnectBehaviorAsync(pin: 2, DisconnectBehavior.SetLow);

// GPIO34 の状態を読み取り
bool? state = await client.ReadInputAsync(pin: 34);
Console.WriteLine($"GPIO34: {(state == true ? "HIGH" : state == false ? "LOW" : "未設定")}");

// GPIO32 の ADC 値を読み取り
var reading = await client.ReadAdcAsync(pin: 32);
if (reading != null)
{
    Console.WriteLine($"GPIO32: Raw={reading.Value.RawValue}, Voltage={reading.Value.Voltage:F3}V");
}
```

### バッチ操作の例

```csharp
using Hondarersoft.Bleio;

var client = new BleioClient();
await client.ConnectAsync(deviceName: "BLEIO");

// 複数のコマンドを一括送信
await client.SendBulkAsync(
    Command.SetOutput(pin: 2, OutputKind.High),
    Command.SetOutput(pin: 12, OutputKind.Low),
    Command.SetInput(pin: 34, InputConfig.PullUp, latchMode: LatchMode.Low),
    Command.SetPwm(pin: 25, dutyCycle: 0.5, PwmFrequency.Freq10kHz),
    Command.EnableAdc(pin: 32, AdcAttenuation.Atten11dB),
    Command.SetDisconnectBehavior(pin: 2, DisconnectBehavior.SetLow)
);
```

### 既存コードとの互換性

サンプルプログラムを更新し、破壊的変更とします。

## 方向性

### 1. DutyCycle の型

PWM のデューティサイクルは `double dutyCycle` (0.0-1.0) でインターフェースする。

- メリット: 直感的 (50% = 0.5)
- デメリット: 内部で `(byte)(dutyCycle * 255)` に変換する必要がある

### 2. ADC 電圧変換

`ReadAdcAsync` が返す `AdcReading` には、減衰率の情報を含めない。  
減衰率が必要な場合は、ユーザーが管理します。

### 3. 名前空間の分割

すべて Hondarersoft.Bleio 名前空間とする。

## 型定義の詳細

### OutputKind enum

```csharp
namespace Hondarersoft.Bleio
{
    public enum OutputKind : byte
    {
        Low = 0x01,        // SET_OUTPUT_LOW (コマンド 0x01)
        High = 0x02,       // SET_OUTPUT_HIGH (コマンド 0x02)
        Blink250ms = 0x03, // SET_OUTPUT_BLINK_250MS (コマンド 0x03)
        Blink500ms = 0x04  // SET_OUTPUT_BLINK_500MS (コマンド 0x04)
    }
}
```

### InputConfig enum

```csharp
namespace Hondarersoft.Bleio
{
    public enum InputConfig : byte
    {
        Floating = 0x81,   // SET_INPUT_FLOATING (コマンド 0x81)
        PullUp = 0x82,     // SET_INPUT_PULLUP (コマンド 0x82)
        PullDown = 0x83    // SET_INPUT_PULLDOWN (コマンド 0x83)
    }
}
```

### LatchMode enum

```csharp
namespace Hondarersoft.Bleio
{
    public enum LatchMode : byte
    {
        None = 0,          // ラッチなし (デフォルト、現在の GPIO レベルをそのまま返す)
        Low = 1,           // LOW エッジラッチ (LOW への変化を検出してラッチする)
        High = 2           // HIGH エッジラッチ (HIGH への変化を検出してラッチする)
    }
}
```

### PwmFrequency enum

```csharp
namespace Hondarersoft.Bleio
{
    public enum PwmFrequency : byte
    {
        Freq1kHz = 0,      // 1 kHz (デフォルト)
        Freq5kHz = 1,      // 5 kHz (LED 調光)
        Freq10kHz = 2,     // 10 kHz (LED 調光、標準)
        Freq25kHz = 3,     // 25 kHz (モーター制御)
        Freq50Hz = 4,      // 50 Hz (サーボモーター)
        Freq100Hz = 5,     // 100 Hz (低速制御)
        Freq500Hz = 6,     // 500 Hz (中速制御)
        Freq20kHz = 7      // 20 kHz (高周波、可聴域外)
    }
}
```

### AdcAttenuation enum

```csharp
namespace Hondarersoft.Bleio
{
    public enum AdcAttenuation : byte
    {
        Atten0dB = 0,      // 0 dB (0-1.1V)
        Atten2_5dB = 1,    // 2.5 dB (0-1.5V)
        Atten6dB = 2,      // 6 dB (0-2.2V)
        Atten11dB = 3      // 11 dB (0-3.3V、デフォルト)
    }
}
```

### DisconnectBehavior enum

```csharp
namespace Hondarersoft.Bleio
{
    public enum DisconnectBehavior : byte
    {
        Maintain = 0,      // 状態を維持
        SetLow = 1,        // 切断時に LOW
        SetHigh = 2        // 切断時に HIGH
    }
}
```

### AdcReading record

```csharp
namespace Hondarersoft.Bleio
{
    public record AdcReading(byte Pin, uint RawValue, double Voltage);
}
```

ADC 読み取り結果を表現します。

- `Pin`: GPIO ピン番号
- `RawValue`: 生の ADC 値 (0-4095、12 ビット)
- `Voltage`: 計算された電圧 (V)

**注意**: `Voltage` は、サーバー側が返す `RawValue` をクライアント側で変換した推定値です。減衰率の情報は含まれていないため、正確な電圧を得るには、ユーザーが `EnableAdcAsync` で設定した減衰率を記憶しておく必要があります。

### GpioCommand record

```csharp
namespace Hondarersoft.Bleio
{
    public record GpioCommand(byte Pin, byte Command, byte Param1, byte Param2);
}
```

プロトコルレベルの GPIO コマンドを表現します。`Command` ビルダーが内部的に生成します。ユーザーが直接構築することは推奨されません。

## プロトコル詳細

### コマンド番号の対応表

| コマンド定数 | 値 | メソッド | 説明 |
|------------|-----|----------|------|
| SET_OUTPUT_LOW | 0x01 | `SetOutputAsync(pin, OutputKind.Low)` | ピンを LOW に設定 |
| SET_OUTPUT_HIGH | 0x02 | `SetOutputAsync(pin, OutputKind.High)` | ピンを HIGH に設定 |
| SET_OUTPUT_BLINK_250MS | 0x03 | `SetOutputAsync(pin, OutputKind.Blink250ms)` | 250ms 点滅 |
| SET_OUTPUT_BLINK_500MS | 0x04 | `SetOutputAsync(pin, OutputKind.Blink500ms)` | 500ms 点滅 |
| SET_OUTPUT_PWM | 0x05 | `SetPwmAsync(pin, dutyCycle, frequency)` | PWM 出力 |
| SET_OUTPUT_ON_DISCONNECT | 0x09 | `SetDisconnectBehaviorAsync(pin, behavior)` | 切断時動作 |
| SET_INPUT_FLOATING | 0x81 | `SetInputAsync(pin, InputConfig.Floating, latchMode)` | 入力 (フローティング) |
| SET_INPUT_PULLUP | 0x82 | `SetInputAsync(pin, InputConfig.PullUp, latchMode)` | 入力 (プルアップ) |
| SET_INPUT_PULLDOWN | 0x83 | `SetInputAsync(pin, InputConfig.PullDown, latchMode)` | 入力 (プルダウン) |
| SET_ADC_ENABLE | 0x91 | `EnableAdcAsync(pin, attenuation)` | ADC 有効化 |
| SET_ADC_DISABLE | 0x92 | `DisableAdcAsync(pin)` | ADC 無効化 |

### キャラクタリスティック UUID

| 名称 | UUID | 用途 |
|------|------|------|
| BLEIO Service | `4fafc201-1fb5-459e-8fcc-c5c9c333914b` | メインサービス |
| GPIO Write | `beb5483e-36e1-4688-b7f5-ea07361b26a8` | GPIO コマンド送信 (WRITE) |
| GPIO Read | `1c95d5e3-d8f7-413a-bf3d-7a2e5d7be87e` | 入力ピン読み取り (READ) |
| ADC Read | `2d8a7b3c-4e9f-4a1b-8c5d-6e7f8a9b0c1d` | ADC 値読み取り (READ) |

## 実装ノート

### エラーハンドリング

接続が切断されている状態でメソッドを呼び出すと、`InvalidOperationException` がスローされます。

```csharp
try
{
    await client.SetOutputAsync(2, BleioClient.OutputKind.High);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"エラー: {ex.Message}");
    // 再接続を試みる
    await client.ConnectAsync();
}
```

### ADC 電圧計算

`ReadAdcAsync` および `ReadAllAdcAsync` は、デフォルトで `AdcAttenuation.Atten11dB` (0-3.3V) を仮定して電圧を計算します。これは、サーバー側が減衰率の情報を返さないためです。

正確な電圧値を得るには、`EnableAdcAsync` で設定した減衰率を記憶しておき、必要に応じて再計算してください。

```csharp
// 11dB 減衰で ADC を有効化
await client.EnableAdcAsync(32, BleioClient.AdcAttenuation.Atten11dB);

// 読み取り (Voltage は 11dB 減衰を仮定して計算される)
var reading = await client.ReadAdcAsync(32);
Console.WriteLine($"電圧: {reading.Voltage:F3}V");
```

### コマンドビルダーの実装

```csharp
public static class Command
{
    public static GpioCommand SetOutput(byte pin, OutputKind outputKind) =>
        new GpioCommand(pin, (byte)outputKind, 0, 0);

    public static GpioCommand SetPwm(byte pin, double dutyCycle, PwmFrequency frequency = PwmFrequency.Freq1kHz)
    {
        if (dutyCycle < 0.0 || dutyCycle > 1.0)
            throw new ArgumentOutOfRangeException(nameof(dutyCycle), "デューティサイクルは 0.0 から 1.0 の範囲で指定してください");

        byte dutyCycleByte = (byte)Math.Round(dutyCycle * 255);
        return new GpioCommand(pin, 0x05, dutyCycleByte, (byte)frequency);
    }

    public static GpioCommand SetInput(byte pin, InputConfig config, LatchMode latchMode = LatchMode.None) =>
        new GpioCommand(pin, (byte)config, (byte)latchMode, 0);

    public static GpioCommand EnableAdc(byte pin, AdcAttenuation attenuation = AdcAttenuation.Atten11dB) =>
        new GpioCommand(pin, 0x91, (byte)attenuation, 0);

    public static GpioCommand DisableAdc(byte pin) =>
        new GpioCommand(pin, 0x92, 0, 0);

    public static GpioCommand SetDisconnectBehavior(byte pin, DisconnectBehavior behavior) =>
        new GpioCommand(pin, 0x09, (byte)behavior, 0);
}
```

### GPIO ピン番号のバリデーション

ADC 対応ピンのバリデーションは `EnableAdcAsync` 内で行われます。

```csharp
// ADC 対応ピンのチェック
if (pin != 32 && pin != 33 && pin != 34 && pin != 35 && pin != 36 && pin != 39)
{
    throw new ArgumentException($"GPIO{pin} は ADC1 に対応していません。対応ピン: 32, 33, 34, 35, 36, 39");
}
```

出力専用ピン (GPIO34-39) や内部予約ピン (GPIO4, GPIO5) に対する不適切な設定は、サーバー側で無視されます。クライアント側では検証を行いません。

## WS2812B シリアル LED 制御

WS2812B は、1 本の信号線でカラー LED (RGB) を制御できるシリアル LED ドライバ IC です。

### EnableWs2812bAsync

GPIO を WS2812B 出力モードに設定し、LED 個数と基準輝度を指定します。

```csharp
public async Task EnableWs2812bAsync(byte pin, byte numLeds, byte brightness = 0)
```

**パラメータ**

- `pin`: GPIO ピン番号 (出力可能なピン)
- `numLeds`: LED 個数 (1-256)
- `brightness`: 基準輝度 (0-255、0 は 100% を意味する)

**使用例**

```csharp
// GPIO18 に 10 個の LED を接続、基準輝度 100% (0)
await client.EnableWs2812bAsync(pin: 18, numLeds: 10, brightness: 0);

// GPIO18 に 10 個の LED を接続、基準輝度 50% (128)
await client.EnableWs2812bAsync(pin: 18, numLeds: 10, brightness: 128);
```

### SetWs2812bColorAsync

WS2812B LED チェーンの特定の LED に色を設定します。

```csharp
public async Task SetWs2812bColorAsync(byte pin, byte ledIndex, byte r, byte g, byte b)
```

**パラメータ**

- `pin`: GPIO ピン番号
- `ledIndex`: LED 番号 (1 から始まる)
- `r`: 赤 (0-255)
- `g`: 緑 (0-255)
- `b`: 青 (0-255)

**使用例**

```csharp
// GPIO18 の LED1 を赤に設定
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 1, r: 255, g: 0, b: 0);

// GPIO18 の LED2 を緑に設定
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 2, r: 0, g: 255, b: 0);

// GPIO18 の LED3 を青に設定
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 3, r: 0, g: 0, b: 255);
```

### SetWs2812bPatternAsync

WS2812B LED チェーンの点灯パターンを設定します。個別の LED ごと、または GPIO 全体に対してパターンを適用できます。

```csharp
public async Task SetWs2812bPatternAsync(byte pin, byte ledIndex, Ws2812bPattern pattern, byte param1 = 0, byte param2 = 0)
```

**パラメータ**

- `pin`: GPIO ピン番号
- `ledIndex`: LED 番号 (0: GPIO 全体、1-255: 個別 LED)
- `pattern`: パターンタイプ (Ws2812bPattern enum)
- `param1`: パターンパラメータ1 (パターンにより異なる)
- `param2`: パターンパラメータ2 (パターンにより異なる)

**Ws2812bPattern enum**

```csharp
public enum Ws2812bPattern : byte
{
    On = 0,            // 常時点灯 (デフォルト)
    Blink250ms = 1,    // 250ms 点灯 / 250ms 消灯
    Blink500ms = 2,    // 500ms 点灯 / 500ms 消灯
    Rainbow = 3,       // 虹色パターン
    Flicker = 4,       // 炎のゆらめきパターン
    Unset = 0xFF       // 個別 LED のパターン設定をクリア (GPIO パターンに戻す)
}
```

**パターンパラメータ**

- **On, Blink250ms, Blink500ms, Unset**: param1, param2 は未使用 (0)
- **Rainbow**:
  - param1: 色相が一周する LED 個数 (1-16、デフォルト: 12)
    - 例: 10 個の LED で色相を 2 周させたい場合は 5 を指定
  - param2: 変化スピード (0-255、デフォルト: 128)
    - 0: 約 0.64秒で一周 (デフォルト、中速)
    - 1: 約 82秒 (1.4分) で一周 (非常にゆっくり)
    - 64: 約 1.28秒で一周 (やや遅い)
    - 128: 約 0.64秒で一周 (中速)
    - 255: 約 0.32秒で一周 (高速)
- **Flicker**:
  - param1: ゆらめきの速度 (0-255、デフォルト: 128)
    - 0: 約 0.5秒周期で変化 (デフォルト、中速)
    - 64: 約 1秒周期で変化 (ゆっくり)
    - 128: 約 0.5秒周期で変化 (中速)
    - 255: 約 0.25秒周期で変化 (速い)
  - param2: ゆらめきの変化幅 (0-255、デフォルト: 128)
    - 0: 変動なし (基準色のまま)
    - 64: 小さい変動 (控えめなゆらめき)
    - 128: 中程度の変動 (自然なゆらめき)
    - 192: 大きい変動 (激しいゆらめき)
    - 255: 最大変動 (非常に激しいゆらめき)

**使用例**

```csharp
// GPIO18 のすべての LED を虹色パターンに設定 (色相が 12 LED で一周、スピード 128)
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 0, pattern: Ws2812bPattern.Rainbow, param1: 12, param2: 128);

// GPIO18 の LED1 を 250ms 点滅パターンに設定
// (事前に SetWs2812bColorAsync でベースカラーを設定しておく)
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 1, r: 255, g: 0, b: 0); // 赤に設定
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 1, pattern: Ws2812bPattern.Blink250ms);

// GPIO18 の LED2-10 を虹色パターン、LED1 を赤の点滅に設定
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 1, r: 255, g: 0, b: 0);
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 1, pattern: Ws2812bPattern.Blink250ms);
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 0, pattern: Ws2812bPattern.Rainbow, param1: 10, param2: 128);

// GPIO18 の LED1 の個別パターン設定をクリアして GPIO パターンに戻す
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 1, pattern: Ws2812bPattern.Unset);

// GPIO18 のすべての LED を赤色の炎のゆらめきに設定
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 0, r: 255, g: 64, b: 0); // 赤色に設定
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 0, pattern: Ws2812bPattern.Flicker, param1: 128, param2: 128);

// ゆっくりとしたゆらめき
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 0, r: 255, g: 64, b: 0);
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 0, pattern: Ws2812bPattern.Flicker, param1: 64, param2: 96);

// 激しいゆらめき
await client.SetWs2812bColorAsync(pin: 18, ledIndex: 0, r: 255, g: 64, b: 0);
await client.SetWs2812bPatternAsync(pin: 18, ledIndex: 0, pattern: Ws2812bPattern.Flicker, param1: 255, param2: 192);
```

**動作仕様**

- LED 番号 0 でパターンを設定すると、その GPIO のすべての LED に適用されます
- 個別の LED にパターンを設定した場合、その LED は個別パターンが優先されます
- 個別パターンが設定されていない LED は、GPIO 全体のパターン (LED 番号 0) を継承します
- BLINK 系パターンは、デジタル出力の SET_OUTPUT_BLINK_250MS / SET_OUTPUT_BLINK_500MS と同じタイミングで点滅します
- RAINBOW パターンは、SetWs2812bColorAsync で設定した色を無視します
- FLICKER1 パターンは、同じ GPIO の FLICKER1 LED すべてで明度が同期します。色相ゆらぎはありません
- FLICKER2 パターンは、LED ごとに独立した明度のゆらめきを持ちます。色相ゆらぎはありません
- FLICKER3 パターンは、LED ごとに独立した明度と色相のゆらめきを持ちます
- PATTERN_UNSET は個別 LED (LED 番号 1-255) にのみ設定可能で、個別設定をクリアして GPIO 全体のパターンに戻します

**RAINBOW パターンの動作詳細**

RAINBOW パターンは、HSV 色空間を使用して色相を連続的に変化させ、虹色のアニメーションを生成します。

- **色の流れる方向**: LED 番号 1 (ESP32 に最も近い) から LED 番号 N (最も遠い) 方向に色が流れます
- **色相オフセット**: 各 LED は、LED 番号に応じた色相オフセットを持ちます
- **同期動作**: 同じ GPIO の複数の LED は、共通の基準クロックを共有し、同期してアニメーションします

**FLICKER パターンの動作詳細**

FLICKER パターンは、炎のゆらめきを表現するパターンです。SetWs2812bColorAsync で設定した基準色を中心に、明度をゆらめかせます。3 つの種別があり、用途に応じて使い分けます。

**FLICKER1: GPIO 共有明度、色相ゆらぎなし**

- **同期動作**: 同じ GPIO の FLICKER1 LED すべてで明度が同期します
- **用途**: 複数の LED を炎の一部として表現し、全体で一体感のあるゆらめきを演出
- **明度の変動**: 間欠カオス法による 1/f ゆらぎで明度を生成し、基準明度の 62.5% - 100% の範囲でゆらめきます
- **色相の変動**: なし (基準色の色相をそのまま使用)
- **注意事項**: 基準色が設定されていない場合 (RGB = 0, 0, 0) は消灯します

**FLICKER2: LED 個別明度、色相ゆらぎなし**

- **独立した挙動**: 各 LED は独立した疑似乱数生成器を持ち、互いに同期せずに独立してゆらめきます
- **用途**: 複数の炎を個別に表現する場合や、キャンドルアレイのような演出
- **明度の変動**: 間欠カオス法による 1/f ゆらぎで明度を生成し、基準明度の 62.5% - 100% の範囲でゆらめきます
- **色相の変動**: なし (基準色の色相をそのまま使用)
- **注意事項**: 基準色が設定されていない場合 (RGB = 0, 0, 0) は消灯します

**FLICKER3: LED 個別明度、色相ゆらぎあり**

- **独立した挙動**: 各 LED は独立した疑似乱数生成器を持ち、互いに同期せずに独立してゆらめきます
- **用途**: 炎のリアルな表現。明度と色相の両方がゆらぐことで、より自然な炎を演出
- **明度の変動**: 間欠カオス法による 1/f ゆらぎで明度を生成し、基準明度の 62.5% - 100% の範囲でゆらめきます
- **色相の変動**: ランダムウォークにより色相が小さく変動します (明度とは独立)
- **注意事項**: 基準色が設定されていない場合 (RGB = 0, 0, 0) は消灯します

## 関連ドキュメント

- [プロトコル仕様](bleio-protocol.md): BLE GATT プロトコルの詳細
- [開発環境セットアップ](get-started.md): ESP32 ファームウェアの開発環境
