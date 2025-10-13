# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト構造

espio は ESP32 ベースのリモート I/O です。

### ディレクトリ構成

```text
espio/
├── server/                ESP32 ファームウェアプロジェクト (サブモジュール)
│   ├── src/               ソースコード (main.c)
│   ├── include/           ヘッダファイル
│   ├── lib/               ライブラリ
│   ├── test/              テストコード
│   ├── platformio.ini     PlatformIO 設定ファイル
│   ├── CMakeLists.txt     ESP-IDF ビルド設定
│   ├── LICENSE            サーバーコードのライセンス
│   └── README.md          サーバーコードの説明
├── client/                C# BLE クライアントプロジェクト
│   ├── Program.cs         メインプログラム
│   ├── BleGpioClient.cs   BLE GPIO クライアントクラス
│   ├── client.csproj      プロジェクトファイル
│   └── espio.sln          ソリューションファイル
├── docs-src/              ドキュメントソース
│   └── get_started.md     開発環境セットアップガイド
├── CLAUDE.md              プロジェクト仕様書
├── README.md              プロジェクト概要
└── LICENSE                プロジェクトライセンス (MIT)
```

### 開発環境

**ビルドシステム**

PlatformIO を使用しています。ESP-IDF フレームワークをベースに開発します。

**ターゲットボード**

DOIT ESP32 DevKit V1 (`esp32doit-devkit-v1`)

**開発ツール**

VS Code + PlatformIO IDE 拡張機能を推奨します。詳細は `docs-src/get_started.md` を参照してください。

### ビルドとデプロイ

**サーバー (ESP32)**

```bash
cd server
pio run
```

**ESP32 への書き込み**

```bash
cd server
pio run --target upload
```

**シリアルモニター**

```bash
cd server
pio device monitor
```

**クライアント (C#)**

```bash
cd client
dotnet build
dotnet run
```

### ライセンス

MIT ライセンスです。詳細は `LICENSE` ファイルを参照してください。

## プロトコル仕様

BLE GATT サービスを使用して ESP32 の GPIO を制御します。詳細なプロトコル仕様は `docs-src/protocol.md` を参照してください。

**概要**

- サービス UUID: `4fafc201-1fb5-459e-8fcc-c5c9c333914b`
- デバイス名: ESP32-GPIO
- 書き込みキャラクタリスティック (UUID: `beb5483e-36e1-4688-b7f5-ea07361b26a8`): GPIO モード設定と出力制御
- 読み取りキャラクタリスティック (UUID: `1c95d5e3-d8f7-413a-bf3d-7a2e5d7be87e`): GPIO 入力状態の読み取り
