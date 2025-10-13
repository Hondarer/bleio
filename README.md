# espio

espio は、ESP32 を使った BLE (Bluetooth Low Energy) ベースのリモート GPIO 制御システムです。Windows PC から ESP32 の GPIO を BLE 経由で制御できます。

## 特徴

BLE 経由で ESP32 の GPIO を直接制御できます。ドライバのインストールは不要で、Windows 標準の BLE スタックを使用します。

GPIO のモード設定 (入力、出力、プルアップ付き入力)、デジタル入出力 (HIGH/LOW の書き込みと読み取り)、および自動点滅機能 (250ms/500ms 周期) に対応しています。

C# で書かれた Windows 用のサンプルクライアントを同梱しています。

## システム構成

**サーバー (ESP32)**

DOIT ESP32 DevKit V1 を使用します。ESP-IDF フレームワークと NimBLE スタックで BLE ペリフェラルとして動作し、GATT サービスを公開します。

**クライアント (Windows PC)**

.NET 9.0 (C#) で実装されています。Windows.Devices.Bluetooth API を使用して BLE 接続を行います。

## セットアップ

### 必要なもの

**ハードウェア**

- DOIT ESP32 DevKit V1
- USB ケーブル (Type-A to Micro-B)
- Windows 10/11 PC (BLE 対応)

**ソフトウェア**

- VS Code + PlatformIO IDE 拡張機能
- .NET 9.0 SDK

### サーバーのセットアップ

リポジトリをクローンし、サブモジュールを初期化します。

```bash
git clone https://github.com/Hondarer/espio.git
cd espio
git submodule update --init --recursive
```

サーバーをビルドして ESP32 に書き込みます。

```bash
cd server
pio run --target upload
```

シリアルモニターで動作を確認します。

```bash
pio device monitor
```

起動メッセージに "ESP32 GPIO Control Service" と表示されれば成功です。

### クライアントのセットアップ

クライアントをビルドして実行します。

```bash
cd client
dotnet build
dotnet run
```

"ESP32-GPIO" という名前の BLE デバイスを自動的に検索して接続します。

## 使い方

詳細なプロトコル仕様と使用方法は `CLAUDE.md` を参照してください。

開発環境のセットアップについては `docs-src/get_started.md` を参照してください。

## プロトコル

BLE GATT サービスを使用して GPIO を制御します。

**サービス UUID**

`4fafc201-1fb5-459e-8fcc-c5c9c333914b`

**キャラクタリスティック**

- GPIO 書き込み (UUID: `beb5483e-36e1-4688-b7f5-ea07361b26a8`)
  - プロパティ: WRITE
  - 用途: GPIO モード設定、出力制御、点滅制御
- GPIO 読み取り (UUID: `1c95d5e3-d8f7-413a-bf3d-7a2e5d7be87e`)
  - プロパティ: READ, WRITE
  - 用途: GPIO 入力状態の読み取り

**対応コマンド**

- SET_INPUT (0): 入力モードに設定
- SET_OUTPUT (1): 出力モードに設定
- SET_INPUT_PULLUP (2): プルアップ入力モードに設定
- WRITE_LOW (10): LOW 出力
- WRITE_HIGH (11): HIGH 出力
- BLINK_500MS (12): 500ms 周期の点滅
- BLINK_250MS (13): 250ms 周期の点滅

詳細な仕様は `docs-src/protocol.md` を参照してください。

## 使用例

### 基本的な GPIO 制御

```{.csharp caption="手動で LED を点滅"}
using var client = new BleGpioClient();

// デバイスに接続
await client.ConnectAsync("ESP32-GPIO");

// GPIO2 (LED) を出力モードに設定
await client.SetPinModeAsync(2, BleGpioClient.PinMode.Output);

// LED を手動で点滅
for (int i = 0; i < 5; i++)
{
    await client.DigitalWriteAsync(2, true);  // HIGH
    await Task.Delay(500);
    await client.DigitalWriteAsync(2, false); // LOW
    await Task.Delay(500);
}

// GPIO34 の状態を読み取り
bool state = await client.DigitalReadAsync(34);
Console.WriteLine($"GPIO34: {state}");
```

### 自動点滅機能

```{.csharp caption="自動点滅"}
using var client = new BleGpioClient();
await client.ConnectAsync("ESP32-GPIO");

// GPIO2 を 500ms 周期で点滅開始
await client.StartBlinkAsync(2, BleGpioClient.BlinkMode.Blink500ms);

// 5 秒間点滅させる
await Task.Delay(5000);

// 点滅を停止して LOW にする
await client.DigitalWriteAsync(2, false);
```

## ライセンス

MIT ライセンスです。詳細は `LICENSE` ファイルを参照してください。

## リンク

- [ESP32-WROOM-32 データシート](https://www.espressif.com/sites/default/files/documentation/esp32-wroom-32_datasheet_en.pdf)
- [DOIT ESP32 DevKit V1 ピン配置](https://www.circuitstate.com/tutorials/getting-started-with-espressif-esp32-wifi-bluetooth-soc-using-doit-esp32-devkit-v1-development-board/#DOIT_ESP32_DevKit_V1)
- [PlatformIO ESP32 Platform](https://docs.platformio.org/en/latest/platforms/espressif32.html)
- [ESP-IDF Programming Guide](https://docs.espressif.com/projects/esp-idf/en/latest/)
