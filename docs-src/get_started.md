# VS Code で ESP32 を開発する方法

VS Code で ESP32 の開発環境を整えるには、主に 2 つの方法があります。まず簡単に使える PlatformIO を紹介してから、公式ツールの ESP-IDF についても触れます。

## PlatformIO を使う方法 (推奨)

最も手軽に始められる方法です。拡張機能を 1 つ入れるだけで、コンパイル、転送、デバッグがすべて揃います。

### セットアップ手順

VS Code の拡張機能から「PlatformIO IDE」を検索してインストールします。インストール後、サイドバーに PlatformIO のアイコンが現れます。

新しいプロジェクトを作る際は、PlatformIO Home から「New Project」を選び、ボード (例: ESP32 Dev Module) とフレームワーク (Arduino または ESP-IDF) を指定します。

### コンパイルと転送

画面下部のツールバーに、以下のボタンが並びます。

- チェックマーク: ビルド (コンパイル) のみ
- 右矢印: ビルドして ESP32 に転送
- コンセント型アイコン: シリアルモニター起動

USB ケーブルで ESP32 を接続した状態で右矢印を押せば、自動的にコンパイルして書き込まれます。

### デバッグ実行

ESP32 でデバッグするには、JTAG デバッガ (ESP-Prog など) が必要です。`platformio.ini` にデバッグ設定を追加します。

```ini
[env:esp32dev]
platform = espressif32
board = esp32dev
framework = arduino
debug_tool = esp-prog
debug_init_break = tbreak setup
```

設定後、F5 キーでデバッグ実行が始まります。ブレークポイントを置いて変数を確認できます。

## ESP-IDF 拡張機能を使う方法

ESP32 の開発元である Espressif が提供する公式ツールです。より細かい制御ができますが、セットアップは少し複雑です。

拡張機能「Espressif IDF」をインストールし、表示される設定ウィザードに従って ESP-IDF をダウンロードします。その後、コマンドパレット (Ctrl+Shift+P) から各種操作を実行できます。

- ESP-IDF: Build your project
- ESP-IDF: Flash your project
- ESP-IDF: Monitor your device

デバッグは PlatformIO と同様に JTAG デバッガが必要で、設定後に F5 でデバッグできます。

## まとめ

まずは PlatformIO から始めるのをお勧めします。シンプルな環境で動作確認してから、必要に応じて ESP-IDF に移行するとスムーズです。
