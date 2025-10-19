# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト構造

bleio は ESP32 ベースのリモート I/O です。

### ディレクトリ構成

```text
bleio/
├── bleio-server/            ESP32 ファームウェアプロジェクト (サブモジュール)
│   ├── src/                ソースコード (main.c)
│   ├── include/            ヘッダファイル
│   ├── lib/                ライブラリ
│   ├── test/               テストコード
│   ├── platformio.ini      PlatformIO 設定ファイル
│   ├── CMakeLists.txt      ESP-IDF ビルド設定
│   ├── LICENSE             サーバーコードのライセンス
│   └── README.md           サーバーコードの説明
├── bleio-client/            C# BLE クライアントプロジェクト
│   ├── Program.cs          メインプログラム
│   ├── BleGpioClient.cs    BLE GPIO クライアントクラス
│   ├── bleio-client.csproj プロジェクトファイル
│   └── bleio-client.sln    ソリューションファイル
├── docs-src/                ドキュメントソース
│   └── get_started.md      開発環境セットアップガイド
├── CLAUDE.md                プロジェクト仕様書
├── README.md                プロジェクト概要
└── LICENSE                  プロジェクトライセンス (MIT)
```

### 開発環境

**ビルドシステム**

PlatformIO を使用して、ESP-IDF フレームワークをベースに開発します。

**ターゲットボード**

DOIT ESP32 DevKit V1 (`esp32doit-devkit-v1`)

**開発ツール**

VS Code + PlatformIO IDE 拡張機能を推奨します。詳細は `docs-src/get_started.md` を参照してください。

## プロトコル仕様

BLE GATT サービスを使用して ESP32 の GPIO を制御します。プロトコル仕様は [protocol.md](docs-src/protocol.md) を参照してください。
