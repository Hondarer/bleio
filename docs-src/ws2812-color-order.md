# WS2812 系 LED のカラーオーダー調査結果

WS2812 系のアドレサブル LED には、データの送信順序が GRB (Green-Red-Blue) と RGB (Red-Green-Blue) の 2 種類が存在します。本ドキュメントでは、その調査結果と、BLEIO プロジェクトでの対応をまとめます。

## 背景

BLEIO プロジェクトの開発中、WS2812B LED に赤色 (R=255, G=0, B=0) を設定したところ、実際には緑色で光るという問題が発生しました。調査の結果、使用している LED のカラーオーダーが標準仕様と異なることが判明しました。

## WS2812 系 LED の標準仕様

### WS2812 / WS2812B (SMD 5050 パッケージ)

標準的な WS2812 / WS2812B の仕様は以下の通りです。

**カラーオーダー: GRB (Green-Red-Blue)**

公式データシートには "Follow the order of GRB to sent data" と明記されています。各 LED に 24 [bit] のデータを送信する際、緑 8 [bit]、赤 8 [bit]、青 8 [bit] の順で送信します。

これは、一般的に想定される RGB 順ではなく、赤と緑が入れ替わった順序です。FastLED、WLED、Adafruit NeoPixel などの主要なライブラリは、デフォルトで GRB 順を使用します。

## カラーオーダーのバリエーション

実際の市場には、カラーオーダーが異なる複数のバリアントが存在します。

### WS2812B-2020 (SMD 2020 パッケージ)

WS2812B-2020 は、標準の 5050 パッケージ (5[mm] × 5[mm]) よりも小型の 2020 パッケージ (2[mm] × 2[mm]) を使用しています。

**特徴**

パッケージ内で物理的に赤と緑の LED ダイが入れ替わっているため、標準の GRB 順でデータを送信すると、誤った色が表示されます。

**対処方法**

RGB 順または BRG 順でデータを送信する必要があります。FastLED ライブラリでは以下のように指定します。

```cpp
FastLED.addLeds<WS2812, DATA_PIN, BRG>(leds, NUM_LEDS);
```

### 5mm DIP タイプ (スルーホール型)

5mm 砲弾型のアドレサブル LED には、複数の種類が存在します。

#### PL9823-F5

**カラーオーダー: RGB (Red-Green-Blue)**

PL9823-F5 は、WS2812 と互換性のあるタイミング仕様を持ちますが、赤と緑が入れ替わっています (Surreality Labs の記事より)。スルーホールタイプのため、ブレッドボードに挿して使用でき、初心者にも扱いやすいという利点があります。

#### WS2811 ベースの 5mm LED

**カラーオーダー: RGB (Red-Green-Blue) が多い**

多くの 5mm DIP タイプの互換 LED が WS2811 チップを内蔵しています。WS2811 は一般的に RGB 順を使用します。

#### WS2812D-F5 (Worldsemi 公式)

**カラーオーダー: GRB (Green-Red-Blue)**

公式データシートでは GRB 順と記載されていますが、市場に出回っている互換品では仕様が異なる場合があります。

### クローン品とバリアント

中国製の互換品では、以下のような問題があります。

- WS2812B として販売されていても、実際には SK6812 や他のチップが使用されている場合がある
- 製品説明と実際の仕様が異なる場合がある
- 同じ型番でもロットによって仕様が変わる可能性がある

## 業界標準の対応方法

主要な LED 制御ライブラリでは、カラーオーダーを設定可能にすることで、この問題に対応しています。

### FastLED

```cpp
// 標準 WS2812B (GRB)
FastLED.addLeds<WS2812, DATA_PIN, GRB>(leds, NUM_LEDS);

// RGB 順のバリアント用
FastLED.addLeds<WS2812, DATA_PIN, RGB>(leds, NUM_LEDS);

// WS2812B-2020 用
FastLED.addLeds<WS2812, DATA_PIN, BRG>(leds, NUM_LEDS);
```

### WLED

WLED では、設定画面で LED のカラーオーダーを選択できます。これにより、ユーザーは自分の LED に合わせて適切な設定を選択できます。

## BLEIO プロジェクトでの対応

### 問題の発生

開発中、以下のコマンドで赤色を設定したところ、実際には緑色で光りました。

```csharp
await client.SetWs2812bColorAsync(18, 1, 255, 0, 0);  // 赤を指定
```

ESP32 のログでは正しく `R=255, G=0, B=0` と認識されていましたが、LED は緑色で発光しました。

### 原因

当初のコードでは、WS2812B の標準仕様に従って GRB 順でデータを送信していました。

```c
// 修正前: GRB 形式で保存
config->led_data[offset + 0] = (uint8_t)g_scaled;
config->led_data[offset + 1] = (uint8_t)r_scaled;
config->led_data[offset + 2] = (uint8_t)b_scaled;
```

しかし、使用している LED は RGB 順を必要とするバリアントでした。

### 修正内容

データの送信順序を RGB 順に変更しました。

```c
// 修正後: RGB 形式で保存
config->led_data[offset + 0] = (uint8_t)r_scaled;
config->led_data[offset + 1] = (uint8_t)g_scaled;
config->led_data[offset + 2] = (uint8_t)b_scaled;
```

修正箇所は以下の 2 つです。

- `gpio_set_ws2812b_color` 関数 (main.c:1788-1790)
- `update_ws2812b_patterns` 関数 (main.c:786-788)

### 使用している LED の推定

修正後、RGB 順で正しく動作することから、使用している LED は以下のいずれかと推定されます。

- PL9823-F5 またはそのクローン品
- WS2811 ベースの 5mm DIP LED
- WS2812B-2020 (小型パッケージ版)
- クローン品で仕様が異なるもの

## 実用的な推奨事項

WS2812 系 LED を使用する際の推奨事項をまとめます。

### テストによる確認

カラーオーダーは、実際にテストして確認することを強く推奨します。以下の手順で確認できます。

1. 赤色 (R=255, G=0, B=0) を送信して、赤く光るか確認
2. 緑色 (R=0, G=255, B=0) を送信して、緑く光るか確認
3. 青色 (R=0, G=0, B=255) を送信して、青く光るか確認

### ドキュメント化

確認したカラーオーダーは、コードやドキュメントに明記しておくことを推奨します。

```c
// この LED は RGB 順を使用 (PL9823-F5 または互換品と推定)
config->led_data[offset + 0] = (uint8_t)r_scaled;
config->led_data[offset + 1] = (uint8_t)g_scaled;
config->led_data[offset + 2] = (uint8_t)b_scaled;
```

### 柔軟な実装

可能であれば、カラーオーダーを実行時に設定可能にすることを検討してください。これにより、異なる LED に対応できます。

## 参考文献

- Worldsemi WS2812B Datasheet
- Surreality Labs: "Playing with the PL9823-F5: a breadboardable Neopixel-compatible LED" (2014)
- FastLED GitHub Issue #878: "Examples are inconsistent in terms of color order for WS2812(B) and NeoPixel"
- GitHub Issue: "WS2812 Red and Green Swapped" (Tasmota #3863)

## まとめ

WS2812 系 LED は、標準仕様では GRB 順を使用しますが、5mm DIP タイプやクローン品では RGB 順を使用するものが多く存在します。BLEIO プロジェクトでは、使用している LED が RGB 順であることが確認できたため、コードを RGB 順に修正しました。

今後、異なる LED を使用する場合は、まず実際にテストしてカラーオーダーを確認することを推奨します。
