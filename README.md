# bleio

bleio は、ESP32 を使った BLE (Bluetooth Low Energy) ベースのリモート GPIO 制御システムです。Windows PC から ESP32 の GPIO を BLE 経由で制御できます。

過去、モルフィー企画からリリースされた USB-IO の思想を受け継ぎ、特別なドライバ不要 (Windows であれば管理者権限不要) で、シンプルなプロトコルを通して ESP32 の GPIO を扱うことができます。[^usb-io]

[^usb-io]: 現在でも、USB-IO は [Km2Net](https://km2net.com/index_e.shtml) さんにて互換品の取り扱いがあります。

## 特徴

BLE 経由で ESP32 の GPIO を直接制御できます。ドライバのインストールは不要で、Windows 標準の BLE スタックを使用します。

GPIO のモード設定 (出力、ハイインピーダンス入力、プルアップ付き入力、プルダウン付き入力)、デジタル入出力 (HIGH / LOW の書き込みと読み取り)、および自動点滅機能 (250ms / 500ms 周期)、PWM 出力、ADC 入力に対応しています。

C# で書かれた Windows 用のサンプルクライアントを同梱しています。

## システム構成

### サーバー (ESP32)

DOIT ESP32 DevKit V1 を使用します。ESP-IDF フレームワークと NimBLE スタックで BLE ペリフェラルとして動作し、GATT サービスを公開します。

### クライアント (Windows PC)

.NET 9.0 (C#) で実装されています。Windows.Devices.Bluetooth API を使用して BLE 接続を行います。

## セットアップ

### 必要なもの

#### ハードウェア

- DOIT ESP32 DevKit V1
- USB ケーブル (Type-A to Micro-B)
- Windows 10/11 PC (BLE 対応)

#### ソフトウェア

- VS Code + PlatformIO IDE 拡張機能
- .NET 9.0 SDK

### サーバーのセットアップ

リポジトリをクローンし、サブモジュールを初期化します。

```bash
git clone https://github.com/Hondarer/bleio.git
cd bleio
git submodule update --init --recursive
```

サーバーをビルドして ESP32 に書き込み、シリアルモニターで動作を確認します。

起動メッセージに "Starting BLEIO-ESP32 Service" と表示されれば成功です。

### クライアントのセットアップ

クライアントをビルドして実行します。

```bash
cd client
dotnet build
dotnet run
```

"BLEIO" という名前の BLE デバイスを自動的に検索して接続します。

## 使い方

詳細なプロトコル仕様と使用方法は `CLAUDE.md` を参照してください。

開発環境のセットアップについては `docs-src/get_started.md` を参照してください。

## プロトコル

BLE GATT サービスを使用して GPIO を制御します。

[protocol.md](docs-src/protocol.md) を参照してください。

## ライセンス

`LICENSE` ファイルを参照してください。

## リンク

- [ESP32-WROOM-32 データシート](https://www.espressif.com/sites/default/files/documentation/esp32-wroom-32_datasheet_en.pdf)
- [DOIT ESP32 DevKit V1 ピン配置](https://www.circuitstate.com/tutorials/getting-started-with-espressif-esp32-wifi-bluetooth-soc-using-doit-esp32-devkit-v1-development-board/#DOIT_ESP32_DevKit_V1)
- [PlatformIO ESP32 Platform](https://docs.platformio.org/en/latest/platforms/espressif32.html)
- [ESP-IDF Programming Guide](https://docs.espressif.com/projects/esp-idf/en/latest/)
