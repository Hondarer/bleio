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
│   ├── BleioClient.cs      BLE GPIO クライアントクラス
│   ├── bleio-client.csproj プロジェクトファイル
│   └── bleio-client.sln    ソリューションファイル
├── docs-src/                ドキュメントソース
│   ├── get_started.md      開発環境セットアップガイド
│   ├── protocol.md         BLE GATT プロトコル仕様
│   └── client-interface.md C# クライアント API リファレンス
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

## ドキュメント

### プロトコル仕様

BLE GATT サービスを使用して ESP32 の GPIO を制御します。プロトコル仕様は [protocol.md](docs-src/protocol.md) を参照してください。

### C# クライアント API

C# クライアントライブラリの API リファレンスは [client-interface.md](docs-src/client-interface.md) を参照してください。主要な機能として以下を提供します。

- GPIO の入出力制御 (デジタル、点滅)
- PWM 出力 (8 段階の周波数プリセット、0-100% のデューティサイクル)
- ADC 入力 (12 ビット分解能、4 段階の減衰率)
- BLE 切断時の動作設定
- バッチコマンド送信
