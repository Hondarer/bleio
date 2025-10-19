# BLEIO クライアント実装仕様

BLEIO クライアントは、Windows から ESP32 の GPIO を BLE (Bluetooth Low Energy) 経由で制御するための C# クライアントライブラリです。

## 概要

本クライアントは、Windows 標準の BLE スタックを使用して ESP32 と通信します。ドライバのインストールは不要です。

## 対応環境

**プラットフォーム**

Windows 10 バージョン 1803 (ビルド 17763) 以降

**フレームワーク**

.NET 9.0 (`net9.0-windows10.0.19041.0`)

**必要な機能**

- Bluetooth 4.0 以降をサポートする Bluetooth アダプタ
- Windows の Bluetooth 機能が有効になっていること

## プロジェクト構成

```text
bleio-client/
├── BleioClient.cs         BLE GPIO クライアントクラス
├── Program.cs             サンプルプログラム
├── bleio-client.csproj    プロジェクトファイル
├── bleio-client.sln       ソリューションファイル
├── .gitignore             Git 除外設定
├── bin/                   ビルド出力ディレクトリ
└── obj/                   中間ファイルディレクトリ
```

## ビルドと実行

**ビルド**

```bash
cd bleio-client
dotnet build
```

**実行**

```bash
dotnet run
```

## BleioClient クラス

### 名前空間

```csharp
namespace Hondarersoft.Bleio
```

### 主要なメソッド

#### 接続

**デバイス名で接続**

```csharp
public async Task<bool> ConnectAsync(string deviceName = "BLEIO")
```

BLE デバイスの名前を指定して接続します。デフォルトは `"BLEIO"` です。

**MAC アドレスで接続 (文字列形式)**

```csharp
public async Task<bool> ConnectByMacAddressAsync(string macAddress)
```

MAC アドレスを `"aa:bb:cc:dd:ee:ff"` 形式の文字列で指定して接続します。

**MAC アドレスで接続 (数値形式)**

```csharp
public async Task<bool> ConnectByMacAddressAsync(ulong bluetoothAddress)
```

MAC アドレスを ulong 型の数値で指定して接続します。

#### GPIO 制御

**ピンモード設定**

```csharp
public async Task SetPinModeAsync(byte pin, PinMode mode, LatchMode latchMode = LatchMode.None)
```

GPIO ピンのモードを設定します。

- `pin`: GPIO ピン番号 (2-39)
- `mode`: ピンモード (Output, InputFloating, InputPullup, InputPulldown)
- `latchMode`: ラッチモード (None, Low, High)

**デジタル出力**

```csharp
public async Task DigitalWriteAsync(byte pin, bool value)
```

GPIO ピンに HIGH (true) または LOW (false) を出力します。

**デジタル入力 (単一ピン)**

```csharp
public async Task<bool?> DigitalReadAsync(byte pin)
```

指定した GPIO ピンの状態を読み取ります。ピンが入力モードに設定されていない場合は null を返します。

**デジタル入力 (全ピン)**

```csharp
public async Task<(byte Pin, bool State)[]> ReadAllInputsAsync()
```

入力モードに設定されているすべての GPIO ピンの状態を一括で読み取ります。

**コマンド一括送信**

```csharp
public async Task SendCommandsAsync(GpioCommand[] commands)
```

複数の GPIO コマンドを一度に送信します。最大 24 個のコマンドを同時に送信できます。

**自動点滅**

```csharp
public async Task StartBlinkAsync(byte pin, BlinkMode mode)
```

GPIO ピンを自動的に点滅させます。

- `mode`: 点滅周期 (Blink500ms, Blink250ms)

**PWM 出力**

```csharp
public async Task SetPwmAsync(byte pin, double dutyCycle, PwmFrequency frequency = PwmFrequency.Freq1kHz)
```

GPIO ピンを PWM 出力モードに設定します。

- `pin`: GPIO ピン番号 (2-39、入力専用ピンを除く)
- `dutyCycle`: デューティサイクル (0.0-1.0、0.0 = 0%、1.0 = 100%)
- `frequency`: PWM 周波数プリセット (デフォルト: Freq1kHz)

#### リソース管理

```csharp
public void Dispose()
```

BLE 接続を切断し、リソースを解放します。

### 列挙型

#### PinMode

GPIO ピンのモードを表します。

```csharp
public enum PinMode : byte
{
    Output = 0,           // 出力モード
    InputFloating = 1,    // ハイインピーダンス入力モード
    InputPullup = 2,      // 内部プルアップ付き入力モード
    InputPulldown = 3     // 内部プルダウン付き入力モード
}
```

#### LatchMode

入力ラッチモードを表します。

```csharp
public enum LatchMode : byte
{
    None = 0,    // ラッチなし
    Low = 1,     // LOW エッジラッチ
    High = 2     // HIGH エッジラッチ
}
```

#### BlinkMode

自動点滅モードを表します。

```csharp
public enum BlinkMode : byte
{
    Blink500ms = 12,    // 500ms 周期で点滅
    Blink250ms = 13     // 250ms 周期で点滅
}
```

#### PwmFrequency

PWM 周波数プリセットを表します。

```csharp
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
```

### GpioCommand レコード

GPIO コマンドを表す record 型です。

```csharp
public record GpioCommand(byte Pin, byte Command, byte Param1, byte Param2);
```

- `Pin`: GPIO ピン番号
- `Command`: コマンド番号 (プロトコル仕様参照)
- `Param1`: パラメータ 1 (入力コマンドではラッチモード)
- `Param2`: パラメータ 2 (将来用、現在は 0x00)

## 使用例

### 基本的な使い方

```csharp
using var client = new BleioClient();

// デバイスに接続
if (!await client.ConnectAsync())
{
    Console.WriteLine("接続に失敗しました");
    return;
}

// GPIO2 (LED) を出力モードに設定
await client.SetPinModeAsync(2, BleioClient.PinMode.Output);

// LED を点灯
await client.DigitalWriteAsync(2, true);

// LED を消灯
await client.DigitalWriteAsync(2, false);
```

### MAC アドレスで接続

```csharp
using var client = new BleioClient();

// MAC アドレスで接続
if (!await client.ConnectByMacAddressAsync("aa:bb:cc:dd:ee:ff"))
{
    Console.WriteLine("接続に失敗しました");
    return;
}
```

### 入力ピンの読み取り

```csharp
// GPIO34 を入力モードに設定 (内部プルアップ)
await client.SetPinModeAsync(34, BleioClient.PinMode.InputPullup);

// GPIO34 の状態を読み取り
bool? state = await client.DigitalReadAsync(34);
if (state == null)
{
    Console.WriteLine("GPIO34 は入力モードに設定されていません");
}
else
{
    Console.WriteLine($"GPIO34: {(state.Value ? "HIGH" : "LOW")}");
}
```

### 複数ピンの一括読み取り

```csharp
// 複数のピンを入力モードに設定
await client.SetPinModeAsync(34, BleioClient.PinMode.InputPullup);
await client.SetPinModeAsync(35, BleioClient.PinMode.InputFloating);

// すべての入力ピンの状態を一括取得
var inputs = await client.ReadAllInputsAsync();
foreach (var (pin, state) in inputs)
{
    Console.WriteLine($"GPIO{pin}: {(state ? "HIGH" : "LOW")}");
}
```

### 自動点滅

```csharp
// GPIO2 を 250ms 周期で点滅
await client.StartBlinkAsync(2, BleioClient.BlinkMode.Blink250ms);

// 点滅を停止する場合は LOW または HIGH を書き込む
await client.DigitalWriteAsync(2, false);
```

### PWM 出力

```csharp
// GPIO2 を 50% デューティサイクル、10 kHz で PWM 出力
await client.SetPwmAsync(2, 0.5, BleioClient.PwmFrequency.Freq10kHz);

// GPIO4 を 75% デューティサイクル、デフォルト周波数 (1 kHz) で PWM 出力
await client.SetPwmAsync(4, 0.75);

// LED の明るさを段階的に変化させる
for (double brightness = 0.0; brightness <= 1.0; brightness += 0.1)
{
    await client.SetPwmAsync(2, brightness, BleioClient.PwmFrequency.Freq10kHz);
    await Task.Delay(500);
}

// PWM を停止する場合は LOW または HIGH を書き込む
await client.DigitalWriteAsync(2, false);
```

### 複数コマンドの一括送信

```csharp
var commands = new[]
{
    new BleioClient.GpioCommand(2, 0, 0, 0),   // GPIO2 を出力モードに設定
    new BleioClient.GpioCommand(4, 11, 0, 0),  // GPIO4 を HIGH に設定
    new BleioClient.GpioCommand(5, 10, 0, 0)   // GPIO5 を LOW に設定
};

await client.SendCommandsAsync(commands);
```

### 入力ラッチ機能

```csharp
// GPIO34 を内部プルアップ付き入力モードに設定し、LOW エッジをラッチ
await client.SetPinModeAsync(34, BleioClient.PinMode.InputPullup, BleioClient.LatchMode.Low);

// ラッチ前は HIGH を返す
var inputs = await client.ReadAllInputsAsync();
// inputs[0] = (34, true)

// ボタンが一度でも押されると (LOW になると) ラッチされる
await Task.Delay(5000); // ユーザーがボタンを押すまで待つ

// ラッチ後は LOW を返す (ボタンを離しても LOW のまま)
inputs = await client.ReadAllInputsAsync();
// inputs[0] = (34, false)

// ラッチをリセットするには、同じピンに SetPinModeAsync を再度実行
await client.SetPinModeAsync(34, BleioClient.PinMode.InputPullup, BleioClient.LatchMode.Low);
```

## 実装の特徴

### 接続管理

- BLE デバイスの検索と接続を自動化
- GATT サービスとキャラクタリスティックの自動取得
- 接続状態の監視 (ConnectionStatusChanged イベント)
- 切断時の自動検出とフラグ更新

### リソース管理

- IDisposable パターンによる適切なリソース解放
- 使用しない GATT サービスの即座の破棄
- 接続エラー時の自動クリーンアップ

### エラーハンドリング

- 接続前の操作を防ぐための EnsureConnected チェック
- GATT 通信エラーの詳細なメッセージ
- 無効なパラメータの検証

### デバッグ出力

- すべての操作にコンソール出力を追加
- MAC アドレスの表示 (コロン区切り形式)
- GATT サービスとキャラクタリスティックの検出状況を詳細に表示

## プロトコル対応

本クライアントは、[BLEIO プロトコル仕様](../docs-src/protocol.md) に準拠しています。

**サービス UUID**

`4fafc201-1fb5-459e-8fcc-c5c9c333914b`

**書き込みキャラクタリスティック UUID**

`beb5483e-36e1-4688-b7f5-ea07361b26a8`

**読み取りキャラクタリスティック UUID**

`1c95d5e3-d8f7-413a-bf3d-7a2e5d7be87e`

## 制限事項

### 一度に送信できるコマンド数

最大 24 個のコマンドを同時に送信できます。これは ESP32 の使用可能な GPIO 数 (24 個) と一致しています。

### MTU サイズ

BLE MTU は最小 100 バイトを想定しています。クライアントが MTU 100 バイトをサポートしていない場合、デフォルトの MTU (23 バイト) が使用され、最大 4 コマンドまで送信できます。

### Windows 専用

本クライアントは Windows.Devices.Bluetooth 名前空間を使用しているため、Windows 専用です。他のプラットフォームでは動作しません。

### 非同期処理のみ

すべての GPIO 操作は非同期メソッドとして実装されています。同期的な呼び出しはサポートされていません。

## トラブルシューティング

### デバイスが見つからない

- Windows の Bluetooth 設定で ESP32 がペアリング済みか確認してください
- ESP32 の電源が入っているか確認してください
- デバイス名が "BLEIO" であることを確認してください (異なる場合は ConnectAsync に引数を渡してください)

### 接続に失敗する

- 他のアプリケーションが ESP32 に接続していないか確認してください
- Windows の Bluetooth 機能が有効になっているか確認してください
- MAC アドレスで接続する場合は、フォーマットが `"aa:bb:cc:dd:ee:ff"` であることを確認してください

### 入力ピンの読み取りで null が返される

- ピンが入力モード (InputFloating, InputPullup, InputPulldown) に設定されているか確認してください
- SetPinModeAsync を呼び出してから ReadAllInputsAsync または DigitalReadAsync を呼び出してください

### コマンドの送信に失敗する

- デバイスとの接続が確立されているか確認してください
- 送信するコマンド数が 1-24 の範囲内であることを確認してください
- ピン番号が有効な範囲 (2-39) であることを確認してください

## ライセンス

MIT ライセンスです。詳細はプロジェクトルートの [LICENSE](../LICENSE) ファイルを参照してください。

## 関連ドキュメント

- [BLEIO プロトコル仕様](../docs-src/protocol.md)
- [プロジェクト概要](../README.md)
- [開発環境セットアップガイド](../docs-src/get_started.md)
