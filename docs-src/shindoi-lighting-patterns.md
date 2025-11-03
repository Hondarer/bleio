# しんどいサイネージ 点灯パターン仕様書

## 概要

本仕様書は、しんどいサイネージの点灯パターンを他のプラットフォームや言語で再現するための技術仕様です。  
アルゴリズムを数式と疑似コードで記述しています。

## システム構成

### ハードウェア仕様

- **LED**: NeoPixel (WS2812系)

### ソフトウェア仕様

- **色空間**: HSV (色相・彩度・明度) から RGB への変換
- **色相範囲**: 0～65535 (16bit 符号なし整数)
- **彩度**: 常に 255 (最大彩度)
- **明度**: 0～255 (8bit)
- **更新周期**: 10ms
- **明るさ段階**: 0～255 (8bit)

## データ型定義

```cpp
// 符号なし整数型
typedef unsigned char      uint8_t;   // 0～255
typedef unsigned short     uint16_t;  // 0～65535
typedef unsigned int       uint32_t;  // 0～4294967295

// 色データ構造 (32bit パック形式)
// ビット配置: [31:24] 未使用, [23:16] R, [15:8] G, [7:0] B
uint32_t color = (R << 16) | (G << 8) | B;
```

## 色相環の定義

色相は 0～65535 の範囲で表現され、色相環を一周します。

| 色相値 | 角度 | 色 |
|--------|------|------|
| 0 | 0° | 赤 (Red) |
| 10923 | 60° | 黄 (Yellow) |
| 21845 | 120° | 緑 (Green) |
| 32768 | 180° | シアン (Cyan) |
| 43691 | 240° | 青 (Blue) |
| 54613 | 300° | マゼンタ (Magenta) |
| 65535 | 360° | 赤 (Red, 一周) |

### 色相の計算式

```text
角度 (度) = (hue / 65536) × 360
hue = (角度 / 360) × 65536
```

## メインループ仕様

### タイミング制御

すべての処理は 10ms 周期で実行されます。

```cpp
static uint32_t tick = 0;
uint32_t currentTime = millis();  // システム起動からの経過時間 (ms)

if ((currentTime - tick) >= 10) {
    tick = currentTime;
    
    // ここで点灯パターンの更新処理を実行
    updateLEDPattern();
    show();  // LED に反映
}
```

## HSV から RGB への変換アルゴリズム (ColorHSV)

### 概要

ColorHSV 関数は、HSV 色空間の値を RGB 色空間に変換します。

### 入力パラメータ

- `hue`: 色相 (0～65535)
- `sat`: 彩度 (0～255)
- `val`: 明度 (0～255)

### 出力

- 32bit パック形式の RGB 値: `(R << 16) | (G << 8) | B`

### アルゴリズム

#### ステップ1: 色相を6つのセクタントに分割

色相環を 6 つの領域に分割します。各セクタントは約 10922.67 (65536/6) の幅を持ちます。

```cpp
// hue を 0～5 の整数値 (セクタント) に変換
uint8_t sextant = hue >> 8;  // 上位8ビットを取得 (hue / 256)

// 範囲外の値を制限
if (sextant > 5) sextant = 5;

// セクタント内の位置 (0～255)
uint8_t h_fraction = hue & 0xFF;  // 下位8ビットを取得 (hue % 256)
```

| セクタント | 色相範囲 | 主要な色の遷移 |
|-----------|---------|---------------|
| 0 | 0～10922 | 赤 → 黄 |
| 1 | 10923～21845 | 黄 → 緑 |
| 2 | 21846～32767 | 緑 → シアン |
| 3 | 32768～43690 | シアン → 青 |
| 4 | 43691～54613 | 青 → マゼンタ |
| 5 | 54614～65535 | マゼンタ → 赤 |

#### ステップ2: 彩度が 0 の場合 (無彩色)

```cpp
if (sat == 0) {
    // グレースケール
    R = G = B = val;
    return (R << 16) | (G << 8) | B;
}
```

#### ステップ3: 中間値の計算

3 つの中間値を計算します。

##### bottom (最小輝度レベル)

```cpp
// エラー補正付き除算: (val * (255 - sat) + 128) / 256
uint16_t invsat = 255 - sat;      // 彩度の逆数
uint16_t ww = val * invsat;       // 中間結果
ww += 1;                          // 誤差補正
ww += ww >> 8;                    // さらに誤差補正 (ww / 256 を加算)
uint8_t bottom = ww >> 8;         // 256で除算
```

数式で表すと:

```text
bottom = floor((val × (255 - sat) + 1 + floor((val × (255 - sat)) / 256)) / 256)
```

##### top (最大輝度レベル)

```cpp
uint8_t top = val;  // 最大値
```

##### rising (上昇する中間値)

```cpp
// bottom から top への補間
uint16_t ww = val * sat;          // val × sat
ww += 1;                          // 誤差補正
ww += ww >> 8;                    // 誤差補正
uint8_t scale_val = ww >> 8;      // スケール値

ww = scale_val * h_fraction;      // セクタント内の位置で補間
ww += 1;                          // 誤差補正
ww += ww >> 8;                    // 誤差補正
uint8_t rising = ww >> 8;         // 上昇値

rising += bottom;                 // ベースレベルに加算
```

数式で表すと:

```text
scale_val = floor((val × sat + 1 + floor((val × sat) / 256)) / 256)
rising = bottom + floor((scale_val × h_fraction + 1 + floor((scale_val × h_fraction) / 256)) / 256)
```

##### falling (下降する中間値)

```cpp
// top から bottom への補間
uint16_t inv_h_fraction = 255 - h_fraction;  // 反転した位置
uint16_t ww = scale_val * inv_h_fraction;    // 下降の計算
ww += 1;                                     // 誤差補正
ww += ww >> 8;                               // 誤差補正
uint8_t falling = ww >> 8;                   // 下降値

falling += bottom;                           // ベースレベルに加算
```

数式で表すと:

```text
falling = bottom + floor((scale_val × (255 - h_fraction) + 1 + floor((scale_val × (255 - h_fraction)) / 256)) / 256)
```

#### ステップ4: セクタントに応じて RGB を割り当て

```cpp
uint8_t R, G, B;

switch (sextant) {
    case 0:  // 赤 → 黄
        R = top;
        G = rising;
        B = bottom;
        break;
    
    case 1:  // 黄 → 緑
        R = falling;
        G = top;
        B = bottom;
        break;
    
    case 2:  // 緑 → シアン
        R = bottom;
        G = top;
        B = rising;
        break;
    
    case 3:  // シアン → 青
        R = bottom;
        G = falling;
        B = top;
        break;
    
    case 4:  // 青 → マゼンタ
        R = rising;
        G = bottom;
        B = top;
        break;
    
    case 5:  // マゼンタ → 赤
    default:
        R = top;
        G = bottom;
        B = falling;
        break;
}
```

#### ステップ5: 32bit 値にパック

```cpp
uint32_t rgb = (R << 16) | (G << 8) | B;
return rgb;
```

### 完全な疑似コード

```python
def ColorHSV(hue, sat, val):
    # セクタント計算
    sextant = (hue >> 8) & 0xFF
    if sextant > 5:
        sextant = 5
    
    # 彩度0の場合
    if sat == 0:
        return (val << 16) | (val << 8) | val
    
    # セクタント内位置
    h_fraction = hue & 0xFF
    
    # 中間値計算
    invsat = 255 - sat
    ww = val * invsat
    ww += 1
    ww += ww >> 8
    bottom = ww >> 8
    
    top = val
    
    # スケール値
    ww = val * sat
    ww += 1
    ww += ww >> 8
    scale_val = ww >> 8
    
    # rising
    ww = scale_val * h_fraction
    ww += 1
    ww += ww >> 8
    rising = (ww >> 8) + bottom
    
    # falling
    inv_h_fraction = 255 - h_fraction
    ww = scale_val * inv_h_fraction
    ww += 1
    ww += ww >> 8
    falling = (ww >> 8) + bottom
    
    # RGB 割り当て
    if sextant == 0:
        R, G, B = top, rising, bottom
    elif sextant == 1:
        R, G, B = falling, top, bottom
    elif sextant == 2:
        R, G, B = bottom, top, rising
    elif sextant == 3:
        R, G, B = bottom, falling, top
    elif sextant == 4:
        R, G, B = rising, bottom, top
    else:  # sextant == 5
        R, G, B = top, bottom, falling
    
    return (R << 16) | (G << 8) | B
```

## ガンマ補正アルゴリズム (gamma32)

### 概要

人間の目は明るさの変化を対数的に感じるため、線形な明度値では暗い部分が黒くつぶれ、明るい部分が白飛びして見えます。ガンマ補正は、この特性を補正して自然な色の変化を実現します。  
ガンマ値 2.6 を使用した補正を行います。

### ガンマ補正の数学的定義

```text
output = (input / 255)^2.6 × 255
```

ただし、実装では計算速度を確保する必要があるため、256要素のルックアップテーブル (LUT) を使用します。

### ガンマ補正テーブル (gamma8)

```cpp
const uint8_t gamma8[256] = {
    0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,
    0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   1,   1,   1,   1,
    1,   1,   1,   1,   1,   1,   1,   1,   1,   2,   2,   2,   2,   2,   2,   2,
    2,   3,   3,   3,   3,   3,   3,   3,   4,   4,   4,   4,   4,   5,   5,   5,
    5,   6,   6,   6,   6,   7,   7,   7,   7,   8,   8,   8,   9,   9,   9,  10,
   10,  10,  11,  11,  11,  12,  12,  13,  13,  13,  14,  14,  15,  15,  16,  16,
   17,  17,  18,  18,  19,  19,  20,  20,  21,  21,  22,  22,  23,  24,  24,  25,
   25,  26,  27,  27,  28,  29,  29,  30,  31,  32,  32,  33,  34,  35,  35,  36,
   37,  38,  39,  39,  40,  41,  42,  43,  44,  45,  46,  47,  48,  49,  50,  50,
   51,  52,  54,  55,  56,  57,  58,  59,  60,  61,  62,  63,  64,  66,  67,  68,
   69,  70,  72,  73,  74,  75,  77,  78,  79,  81,  82,  83,  85,  86,  87,  89,
   90,  92,  93,  95,  96,  98,  99, 101, 102, 104, 105, 107, 109, 110, 112, 114,
  115, 117, 119, 120, 122, 124, 126, 127, 129, 131, 133, 135, 137, 138, 140, 142,
  144, 146, 148, 150, 152, 154, 156, 158, 160, 162, 164, 167, 169, 171, 173, 175,
  177, 180, 182, 184, 186, 189, 191, 193, 196, 198, 200, 203, 205, 208, 210, 213,
  215, 218, 220, 223, 225, 228, 231, 233, 236, 239, 241, 244, 247, 249, 252, 255
};
```

### gamma32 アルゴリズム

gamma32 は、32bit パック形式の RGB 値の各チャンネル (R, G, B) に対して、gamma8 テーブルを適用します。

#### 疑似コード

```python
def gamma32(rgb):
    # RGB を分解
    R = (rgb >> 16) & 0xFF
    G = (rgb >> 8) & 0xFF
    B = rgb & 0xFF
    
    # 各チャンネルにガンマ補正を適用
    R_corrected = gamma8[R]
    G_corrected = gamma8[G]
    B_corrected = gamma8[B]
    
    # 再パック
    return (R_corrected << 16) | (G_corrected << 8) | B_corrected
```

#### C言語実装例

```cpp
uint32_t gamma32(uint32_t x) {
    uint8_t r = (x >> 16) & 0xFF;
    uint8_t g = (x >> 8) & 0xFF;
    uint8_t b = x & 0xFF;
    
    r = gamma8[r];
    g = gamma8[g];
    b = gamma8[b];
    
    return ((uint32_t)r << 16) | ((uint32_t)g << 8) | b;
}
```

### ガンマ補正の効果

| 入力値 | 出力値 | 変化率 |
|--------|--------|--------|
| 0 | 0 | - |
| 32 | 1 | 3.1% |
| 64 | 5 | 7.8% |
| 128 | 50 | 39% |
| 192 | 144 | 75% |
| 255 | 255 | 100% |

暗い領域 (0～64) では変化が非常に緩やか、中間 (64～192) では適度な変化、明るい領域 (192～255) では急激に変化します。これにより、暗い部分のディテールが保たれ、全体として自然な明度勾配が得られます。

### gamma32 の適用タイミング

ColorHSV で RGB に変換した後、必ず gamma32 を適用してから LED に出力します。

```cpp
uint32_t color = ColorHSV(hue, 255, brightness);
color = gamma32(color);
setPixelColor(ledIndex, color);
```

この順序により、HSV 空間での計算結果を人間の目に最適化された形で表示できます。

## 点灯パターンアルゴリズム

### 共通変数

```cpp
static uint16_t hue = 0;                  // 現在の色相 (0～65535)
static uint16_t currentLedBrightness = 0; // 現在の LED 輝度 (0～255)
static uint8_t NUM_PIXELS;                // LED の個数
```

### パターン1: HUE_WAVE (虹色の波)

虹色のグラデーションが横に流れるパターンです。NUM_PIXELS 個の LED が色相環を等分し、全体で虹色を形成します。

#### アルゴリズム

```python
def modeHueWave(hue, brightness):
    ledIndex = 0
    
    # 逆順ループで右から左への流れを作る
    for i in range(NUM_PIXELS - 1, -1, -1):
        # 各 LED の色相を計算
        # i=11 のとき: hue + 59990
        # i=10 のとき: hue + 54613
        # ...
        # i=0 のとき: hue + 0
        pixelHue = hue + (i * 65536 // NUM_PIXELS)
        
        # HSV から RGB に変換
        color = ColorHSV(pixelHue, 255, brightness)
        
        # ガンマ補正を適用
        color = gamma32(color)
        
        # LED に設定
        setPixelColor(ledIndex, color)
        ledIndex += 1
```

#### 数学的表現

```text
LED[j] の色相 = (hue + ((NUM_PIXELS - 1 - j) × 65536 / NUM_PIXELS)) mod 65536
```

ここで `j` は物理的な LED のインデックス (0～(NUM_PIXELS-1)) です。

#### 具体例 (NUM_PIXELS = 12, hue = 0 の場合)

| 物理 LED | ループ変数 i | 色相オフセット | 色相値 | 色 |
|---------|------------|--------------|--------|-----|
| 0 | 11 | 59990 | 59990 | マゼンタ寄りの赤 |
| 1 | 10 | 54613 | 54613 | マゼンタ |
| 2 | 9 | 49152 | 49152 | 青紫 |
| 3 | 8 | 43691 | 43691 | 青 |
| 4 | 7 | 38229 | 38229 | シアン寄りの青 |
| 5 | 6 | 32768 | 32768 | シアン |
| 6 | 5 | 27306 | 27306 | 緑寄りのシアン |
| 7 | 4 | 21845 | 21845 | 緑 |
| 8 | 3 | 16384 | 16384 | 黄緑 |
| 9 | 2 | 10922 | 10922 | 黄 |
| 10 | 1 | 5461 | 5461 | オレンジ |
| 11 | 0 | 0 | 0 | 赤 |

時間経過とともに `hue` が増加するため、すべての LED の色が同じ速度で変化し、虹色の波が右から左へ流れて見えます。

### 速度設定 (色相変化量)

10ms ごとに `hue` に加算される値により、色変化の速度が決まります。

| モード | 加算値 | 1秒あたりの変化 | 一周時間 | 視覚効果 |
|--------|--------|----------------|----------|----------|
| H (High) | 2048 | 204,800 | 約0.32秒 | 激しい点滅・高速な流れ |
| M (Medium) | 512 | 51,200 | 約1.28秒 | 適度な色変化 |
| L (Low) | 64 | 6,400 | 約10.24秒 | ゆったりとした色変化 |

#### 計算式

```text
1秒あたりの色相変化量 = (加算値) × (1000ms / 10ms) = 加算値 × 100

一周時間 (秒) = 65536 / (1秒あたりの色相変化量)
              = 65536 / (加算値 × 100)
              = 655.36 / 加算値
```

### メインループの疑似コード

```python
def loop():
    global hue, currentLedBrightness
    
    # 10ms 待機
    wait(10)
    
    # パターンに応じて描画
    if displayMode == MODE_HUE_WAVE_H or MODE_HUE_WAVE_M or MODE_HUE_WAVE_L:
        modeHueWave(hue, currentLedBrightness)
    
    # モードに応じて色相を更新
    if displayMode == MODE_HUE_WAVE_H:
        hue += 2048
    elif displayMode == MODE_HUE_WAVE_M:
        hue += 512
    elif displayMode == MODE_HUE_WAVE_L:
        hue += 64
    
    # hue は uint16_t なので自動的にオーバーフローして 0 に戻る
    hue = hue & 0xFFFF  # 16bit マスク
    
    # 明るさのフェード処理
    if targetBrightness != currentLedBrightness:
        diff = (targetBrightness - currentLedBrightness) // 32
        
        if diff == 0:
            # 差が小さい場合は ±1 ずつ変化
            diff = 1 if targetBrightness > currentLedBrightness else -1
        
        currentLedBrightness += diff
    
    # LED に出力
    show()
```

## 明るさ制御

### 明るさの段階

0～255 段階の明るさがあります。

### フェード処理アルゴリズム

明るさが変更されると、段階的に遷移します。

```python
def updateBrightness():
    global currentLedBrightness
    
    diff = (targetBrightness - currentLedBrightness) // 32
    
    if diff == 0:
        # 小さな差の場合は符号のみ取得
        if targetBrightness > currentLedBrightness:
            diff = 1
        elif targetBrightness < currentLedBrightness:
            diff = -1
        else:
            return  # すでに目標値
    
    currentLedBrightness += diff
    
    # 範囲制限
    if currentLedBrightness < 0:
        currentLedBrightness = 0
    elif currentLedBrightness > 255:
        currentLedBrightness = 255
```

#### フェード時間の計算

明るさの差を 32 で割った値ずつ、10ms ごとに変化します。

```text
変化回数 = 差 / (差 / 32) = 32回 (最大の場合)
所要時間 = 32回 × 10ms = 320ms
```

具体例:

| 開始 | 終了 | 差 | 1回の変化量 | 変化回数 | 所要時間 |
|------|------|----|-----------|---------|---------|
| 64 | 128 | 64 | 2 | 32 | 320ms |
| 128 | 192 | 64 | 2 | 32 | 320ms |
| 192 | 255 | 63 | 1～2 | 約32 | 約320ms |
| 255 | 64 | -191 | -5～-6 | 約32 | 約320ms |

差が小さい場合 (32未満) は、±1 ずつ変化します。

```text
所要時間 = 差 × 10ms
```

例:

| 開始 | 終了 | 差 | 1回の変化量 | 変化回数 | 所要時間 |
|------|------|----|-----------|---------|---------|
| 128 | 140 | 12 | 1 | 12 | 120ms |
| 200 | 190 | -10 | -1 | 10 | 100ms |

## モード一覧

### 表示モード定義

```cpp
enum DISPLAY_MODE {
    MODE_HUE_WAVE_H,    // 1: 虹色の波 - 高速
    MODE_HUE_WAVE_M,    // 2: 虹色の波 - 中速
    MODE_HUE_WAVE_L,    // 3: 虹色の波 - 低速
};
```

### モード別仕様表

| モード番号 | モード名 | パターン | 色相加算値 | 一周時間 | 視覚効果 |
|-----------|---------|---------|-----------|---------|----------|
| 1 | MODE_HUE_WAVE_H | 虹色の波 | 2048 | 0.32秒 | 激しく流れる虹色 |
| 2 | MODE_HUE_WAVE_M | 虹色の波 | 512 | 1.28秒 | 適度に流れる虹色 |
| 3 | MODE_HUE_WAVE_L | 虹色の波 | 64 | 10.24秒 | ゆったり流れる虹色 |

## 実装上の注意事項

### オーバーフロー処理

色相 (`hue`) は uint16_t (0～65535) として扱い、オーバーフローによる自動的な循環を利用します。

```cpp
// C言語の場合
uint16_t hue = 65530;
hue += 10;  // 65540 になるが、uint16_t の範囲を超えるため 4 になる (65540 % 65536 = 4)

// Python の場合 (明示的にマスク)
hue = (hue + 10) & 0xFFFF
```

### 整数演算の精度

除算による誤差を最小化するため、適切な誤差補正を行います。

```cpp
// 悪い例: 精度が低い
result = (a * b) / 256;

// 良い例: 誤差補正付き
uint16_t ww = a * b;
ww += 1;          // 四捨五入
ww += ww >> 8;    // さらに誤差補正
result = ww >> 8;  // 256で除算
```

### LED の更新

すべての LED の色を設定した後、必ず `show()` を呼び出して実際に出力します。

```python
for i in range(NUM_PIXELS):
    setPixelColor(i, color)

show()  # ここで初めて LED が点灯する
```
