# WS2811 シリアル LED 機能追加の検討

WS2811 シリアル LED を制御する機能を bleio に追加するための検討内容をまとめます。

## WS2811 シリアル LED の概要

WS2811 は、1 本の信号線でカラー LED (RGB) を制御できるシリアル LED ドライバ IC です。複数の LED を数珠つなぎに接続し、1 本の信号線で各 LED の色を個別に制御できます。

### 基本仕様

**信号方式**

1 線式のシリアル通信で、PWM 信号のパルス幅によってデータを表現します。

**タイミング仕様 (標準)**

| 項目 | 0 ビット | 1 ビット |
|------|---------|---------|
| HIGH 時間 | 0.25[µs] | 0.6[µs] |
| LOW 時間 | 1.0[µs] | 0.65[µs] |
| 合計周期 | 1.25[µs] | 1.25[µs] |

リセット信号は LOW を 50[µs] 以上保持することで発行します。

**データフォーマット**

1 個の LED につき 24 ビット (GRB 順、各色 8 ビット) のデータを送信します。

- G (Green): 8 ビット (MSB ファースト)
- R (Red): 8 ビット (MSB ファースト)
- B (Blue): 8 ビット (MSB ファースト)

数珠つなぎされた LED チェーンの場合、先頭の LED から順番にデータを送信します。

**電源仕様**

- 動作電圧: 5[V] ~ 12[V] (LED による)
- 1 個の LED あたりの最大電流: 約 60[mA] (全点灯、白色、最大輝度の場合)

## 使用可能なピン

WS2811 制御には、ESP32 の RMT (Remote Control) ペリフェラルを使用します。RMT は任意の GPIO ピンに割り当て可能です。

### RMT ペリフェラル仕様

**ESP32 の RMT チャネル数**

ESP32 (ESP32-WROOM-32) は 8 チャネルの RMT を搭載しています。

**使用可能な GPIO ピン**

bleio で使用可能な GPIO のうち、出力が可能なピンであればすべて使用できます。

- GPIO2, GPIO12, GPIO13, GPIO14, GPIO15, GPIO16, GPIO17, GPIO18, GPIO19, GPIO21, GPIO22, GPIO23, GPIO25, GPIO26, GPIO27, GPIO32, GPIO33

ただし、以下のピンは内部用途に予約されているため使用できません。

- GPIO4: ボンディング情報クリア判定用
- GPIO5: 認証機能有効 / 無効判定用

### 推奨ピン

信号品質を考慮すると、以下のピンが推奨されます。

- GPIO18, GPIO19, GPIO21, GPIO22, GPIO23 (比較的ノイズに強い)

### 制限事項

- 同時に使用できる WS2811 出力は最大 8 チャネル (RMT チャネル数の制限)
- 入力専用ピン (GPIO34, GPIO35, GPIO36, GPIO39) は使用できません

## プロトコル仕様の拡張

### 新規コマンドの追加

WS2811 制御のため、以下のコマンドを追加します。

| コマンド値 | 名称 | 説明 |
|----------|------|------|
| 31 | SET_WS2811_ENABLE | WS2811 出力モードを有効化する (Param1: LED 個数、Param2: 予約) |
| 32 | SET_WS2811_DISABLE | WS2811 出力モードを無効化する (Param1, Param2: 未使用) |

### WS2811 データ書き込みキャラクタリスティック

WS2811 の LED データを書き込むための新しいキャラクタリスティックを追加します。

**UUID**

`3e8b9c4d-5f6a-4b7c-8d9e-0f1a2b3c4d5e`

**プロパティ**

WRITE

**データ形式**

可変長配列 (最小 5 バイト)

**全体構造**

| バイト位置 | 名称 | 型 | 説明 |
|----------|------|-----|------|
| 0 | Pin Number | uint8 | GPIO ピン番号 (2-33) |
| 1-3 | LED 1 Color | 3 bytes | 1 つ目の LED の色 (G, R, B の順) |
| 4-6 | LED 2 Color | 3 bytes | 2 つ目の LED の色 (G, R, B の順) |
| ... | ... | ... | 最大 N 個まで |

**パケット長の計算**

- パケット長 = 1 (ピン番号) + 3 × N (各 LED のデータ)
- 最小: 1 + 3 = 4 バイト (LED 1 個の場合)
- 最大: BLE MTU に依存 (MTU 92 バイトの場合、最大 30 個の LED を一度に送信可能)

### 使用例

#### WS2811 出力モードを有効化

GPIO18 に接続された 10 個の LED チェーンを有効化する場合

```text
[0x01, 0x12, 0x1F, 0x0A, 0x00]
```

- 0x01: コマンド個数 (1 個)
- 0x12: ピン番号 (GPIO18 = 0x12 = 18)
- 0x1F: コマンド (SET_WS2811_ENABLE = 31 = 0x1F)
- 0x0A: Param1 (LED 個数 = 10)
- 0x00: Param2 (予約)

#### LED データを書き込み

GPIO18 の LED 3 個に色を設定する場合 (LED1: 赤、LED2: 緑、LED3: 青)

```text
[0x12, 0x00, 0xFF, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF]
```

- 0x12: ピン番号 (GPIO18)
- 0x00, 0xFF, 0x00: LED1 (G=0, R=255, B=0) → 赤
- 0xFF, 0x00, 0x00: LED2 (G=255, R=0, B=0) → 緑
- 0x00, 0x00, 0xFF: LED3 (G=0, R=0, B=255) → 青

## ソースコードの改修検討

### 必要な変更点

#### 1. グローバル変数の追加 (main.c)

```c
// WS2811 設定保持用構造体
typedef struct
{
    int8_t rmt_channel;       // 割り当てられた RMT チャネル (-1: 未割り当て)
    uint16_t led_count;       // LED 個数
    rmt_channel_handle_t channel_handle; // RMT チャネルハンドル
    rmt_encoder_handle_t encoder_handle; // RMT エンコーダハンドル
} ws2811_config_t;

static ws2811_config_t ws2811_configs[40] = {0}; // 全 GPIO の WS2811 設定
```

#### 2. GPIO モード状態の追加

`bleio_mode_state_t` 列挙型に新しいモードを追加します。

```c
typedef enum
{
    // ... 既存のモード ...
    BLEIO_MODE_WS2811          // WS2811 出力モード
} bleio_mode_state_t;
```

#### 3. RMT ペリフェラルの初期化

`app_main()` 関数で RMT ペリフェラルを初期化します。

```c
static void ws2811_module_init(void)
{
    // WS2811 設定の初期化
    for (int i = 0; i < 40; i++)
    {
        ws2811_configs[i].rmt_channel = -1;
        ws2811_configs[i].led_count = 0;
        ws2811_configs[i].channel_handle = NULL;
        ws2811_configs[i].encoder_handle = NULL;
    }

    ESP_LOGI(TAG, "WS2811 module initialized (max 8 channels)");
}
```

#### 4. WS2811 有効化関数

```c
static esp_err_t gpio_enable_ws2811(uint8_t pin, uint16_t led_count)
{
    // パラメータ検証
    if (!is_valid_output_pin(pin))
    {
        ESP_LOGE(TAG, "GPIO%d は WS2811 出力に対応していません", pin);
        return ESP_ERR_INVALID_ARG;
    }

    if (led_count == 0 || led_count > 255)
    {
        ESP_LOGE(TAG, "無効な LED 個数: %d (1-255)", led_count);
        return ESP_ERR_INVALID_ARG;
    }

    // 既存のモードをクリーンアップ
    stop_pwm_if_active(pin);
    stop_adc_if_active(pin);

    // RMT チャネルを割り当て
    int8_t rmt_channel = allocate_rmt_channel(pin);
    if (rmt_channel < 0)
    {
        ESP_LOGE(TAG, "RMT チャネルが不足しています (最大 8 チャネル)");
        return ESP_ERR_NO_MEM;
    }

    // RMT チャネルを設定
    rmt_tx_channel_config_t tx_chan_config = {
        .clk_src = RMT_CLK_SRC_DEFAULT,
        .gpio_num = pin,
        .mem_block_symbols = 64,
        .resolution_hz = 10000000, // 10MHz (0.1µs 単位)
        .trans_queue_depth = 4,
        .flags.invert_out = false,
        .flags.with_dma = false,
    };

    rmt_channel_handle_t channel_handle = NULL;
    esp_err_t ret = rmt_new_tx_channel(&tx_chan_config, &channel_handle);
    if (ret != ESP_OK)
    {
        ESP_LOGE(TAG, "RMT チャネル作成に失敗しました: %s", esp_err_to_name(ret));
        return ret;
    }

    // WS2811 エンコーダを作成
    rmt_encoder_handle_t encoder_handle = NULL;
    // ... (エンコーダ設定の詳細は省略)

    // 設定を保存
    ws2811_configs[pin].rmt_channel = rmt_channel;
    ws2811_configs[pin].led_count = led_count;
    ws2811_configs[pin].channel_handle = channel_handle;
    ws2811_configs[pin].encoder_handle = encoder_handle;

    portENTER_CRITICAL(&gpio_states_mux);
    gpio_states[pin].mode = BLEIO_MODE_WS2811;
    portEXIT_CRITICAL(&gpio_states_mux);

    ESP_LOGI(TAG, "GPIO%d を WS2811 モードに設定しました (LED 個数: %d, チャネル: %d)",
             pin, led_count, rmt_channel);

    return ESP_OK;
}
```

#### 5. WS2811 無効化関数

```c
static esp_err_t gpio_disable_ws2811(uint8_t pin)
{
    portENTER_CRITICAL(&gpio_states_mux);
    bleio_mode_state_t mode = gpio_states[pin].mode;
    portEXIT_CRITICAL(&gpio_states_mux);

    if (mode != BLEIO_MODE_WS2811)
    {
        ESP_LOGW(TAG, "GPIO%d は WS2811 モードではありません", pin);
        return ESP_OK;
    }

    // RMT チャネルとエンコーダを削除
    if (ws2811_configs[pin].channel_handle != NULL)
    {
        rmt_del_channel(ws2811_configs[pin].channel_handle);
        ws2811_configs[pin].channel_handle = NULL;
    }

    if (ws2811_configs[pin].encoder_handle != NULL)
    {
        rmt_del_encoder(ws2811_configs[pin].encoder_handle);
        ws2811_configs[pin].encoder_handle = NULL;
    }

    // 設定をクリア
    ws2811_configs[pin].rmt_channel = -1;
    ws2811_configs[pin].led_count = 0;

    portENTER_CRITICAL(&gpio_states_mux);
    gpio_states[pin].mode = BLEIO_MODE_UNSET;
    portEXIT_CRITICAL(&gpio_states_mux);

    ESP_LOGI(TAG, "GPIO%d の WS2811 モードを無効化しました", pin);
    return ESP_OK;
}
```

#### 6. WS2811 データ書き込みキャラクタリスティックのコールバック

```c
static int gatt_svr_chr_ws2811_write_cb(uint16_t conn_handle, uint16_t attr_handle,
                                        struct ble_gatt_access_ctxt *ctxt, void *arg)
{
    if (ctxt->op != BLE_GATT_ACCESS_OP_WRITE_CHR)
    {
        return BLE_ATT_ERR_UNLIKELY;
    }

    struct os_mbuf *om = ctxt->om;
    uint16_t len = OS_MBUF_PKTLEN(om);

    // 最小長チェック: 1 (ピン番号) + 3 (最低 1 LED)
    if (len < 4)
    {
        ESP_LOGE(TAG, "Invalid write length: %d (minimum 4)", len);
        return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
    }

    uint8_t pin;
    os_mbuf_copydata(om, 0, 1, &pin);

    // WS2811 モードか確認
    portENTER_CRITICAL(&gpio_states_mux);
    bleio_mode_state_t mode = gpio_states[pin].mode;
    portEXIT_CRITICAL(&gpio_states_mux);

    if (mode != BLEIO_MODE_WS2811)
    {
        ESP_LOGE(TAG, "GPIO%d は WS2811 モードではありません", pin);
        return BLE_ATT_ERR_UNLIKELY;
    }

    // LED データを取得
    uint16_t data_len = len - 1;
    uint16_t led_count = data_len / 3;

    if (led_count > ws2811_configs[pin].led_count)
    {
        ESP_LOGE(TAG, "LED 個数が設定を超えています: %d > %d", led_count, ws2811_configs[pin].led_count);
        return BLE_ATT_ERR_INVALID_ATTR_VALUE_LEN;
    }

    uint8_t *led_data = malloc(data_len);
    if (led_data == NULL)
    {
        ESP_LOGE(TAG, "メモリ不足");
        return BLE_ATT_ERR_INSUFFICIENT_RES;
    }

    os_mbuf_copydata(om, 1, data_len, led_data);

    // RMT で LED データを送信
    rmt_transmit_config_t tx_config = {
        .loop_count = 0,
    };

    esp_err_t ret = rmt_transmit(ws2811_configs[pin].channel_handle,
                                 ws2811_configs[pin].encoder_handle,
                                 led_data, data_len, &tx_config);

    free(led_data);

    if (ret != ESP_OK)
    {
        ESP_LOGE(TAG, "WS2811 データ送信に失敗しました: %s", esp_err_to_name(ret));
        return BLE_ATT_ERR_UNLIKELY;
    }

    ESP_LOGI(TAG, "GPIO%d に WS2811 データを送信しました (LED 個数: %d)", pin, led_count);

    return 0;
}
```

#### 7. GATT サービス定義への追加

```c
static const struct ble_gatt_svc_def gatt_svr_svcs[] = {
    {
        .type = BLE_GATT_SVC_TYPE_PRIMARY,
        .uuid = &gatt_svr_svc_uuid.u,
        .characteristics = (struct ble_gatt_chr_def[]){
            // ... 既存のキャラクタリスティック ...
            {
                // WS2811 書き込みキャラクタリスティック
                .uuid = &gatt_svr_chr_ws2811_write_uuid.u,
                .access_cb = gatt_svr_chr_ws2811_write_cb,
                .flags = BLE_GATT_CHR_F_WRITE,
            },
            {
                0, // 終端
            }},
    },
    {
        0, // 終端
    },
};
```

### 必要なライブラリ

ESP-IDF の RMT ドライバを使用します。`platformio.ini` への追加は不要です (ESP-IDF に標準で含まれています)。

## 物理配線例

### 単一 LED チェーンの配線

```text
ESP32 (GPIO18) ──┐
                  │
                  ├─── WS2811 LED 1 ──┬─── WS2811 LED 2 ──┬─── ... ──┬─── WS2811 LED N
                  │                     │                     │          │
5[V] 電源 ────────┴─────────────────────┴─────────────────────┴──────────┴───
GND ──────────────────────────────────────────────────────────────────────────
```

### 配線の詳細

**信号線**

- ESP32 の GPIO (例: GPIO18) から WS2811 LED チェーンの DIN (Data In) に接続
- 最後の LED の DOUT (Data Out) は開放

**電源**

- WS2811 LED の VCC を 5[V] 電源に接続
- WS2811 LED の GND を GND に接続
- ESP32 の GND と WS2811 LED の GND を共通接続 (重要)

**注意事項**

- ESP32 の GPIO は 3.3[V] ロジックですが、WS2811 は 5[V] 電源で動作する場合でも 3.3[V] 信号で制御可能です (ただし、一部の WS2811 では動作が不安定になる場合があります)
- 信号品質を向上させるため、信号線に 330[Ω] ~ 470[Ω] の抵抗を直列に接続することを推奨します
- LED チェーンが長い場合 (10 個以上)、信号線にレベル変換 IC (3.3[V] → 5[V]) を使用することを推奨します

### レベル変換を使用した配線 (推奨)

```text
ESP32 (GPIO18) ──┬─── 74HCT245 (3.3V→5V) ──┬─── WS2811 LED 1 ──┬─── ...
                  │                          │                   │
3.3[V] ───────────┤                          │                   │
5[V] ──────────────┴──────────────────────────┴───────────────────┴───
GND ───────────────────────────────────────────────────────────────────
```

**推奨レベル変換 IC**

- 74HCT245 (8 ビット双方向バスバッファ)
- 74AHCT125 (クワッドバスバッファゲート)

## 回路図例

### WS2811 LED 接続回路

```text
        ESP32
       GPIO18 ────┬── R1 (330Ω) ──┬─── WS2811 LED チェーン (DIN)
                  │                │
                  │                │
       3.3V ──────┤                │
       5V ────────┴────────────────┴─── WS2811 VCC
       GND ──────────────────────────── WS2811 GND
```

**部品表**

| 部品 | 値 / 型番 | 説明 |
|------|----------|------|
| R1 | 330[Ω] ~ 470[Ω] | 信号線保護抵抗 |
| LED | WS2811 LED テープまたは個別 LED | 5[V] 電源、GRB 順 |

### 電源の考慮事項

**電流計算**

1 個の LED あたり最大 60[mA] (全点灯、白色、最大輝度) と仮定します。

- 10 個の LED: 10 × 60[mA] = 600[mA]
- 30 個の LED: 30 × 60[mA] = 1800[mA] = 1.8[A]
- 100 個の LED: 100 × 60[mA] = 6000[mA] = 6[A]

**電源容量**

LED 個数に応じて適切な容量の電源を選定してください。

- 10 個以下: USB 電源 (5[V] / 1[A]) で可能
- 30 個以下: AC アダプタ (5[V] / 2[A] 以上) を推奨
- 100 個以上: 専用電源 (5[V] / 10[A] 以上) が必要

**電源配線**

LED チェーンが長い場合、先頭と末尾の両方から電源を供給することで電圧降下を防ぎます。

## 実装の優先度

### フェーズ 1 (最小実装)

- WS2811 出力モードの有効化 / 無効化 (コマンド 31, 32)
- WS2811 データ書き込みキャラクタリスティックの実装
- 単一チャネル (1 ピン) のサポート

### フェーズ 2 (拡張実装)

- 複数チャネル (最大 8 ピン) の同時サポート
- BLE 切断時の動作設定 (全消灯など)
- LED 個数の動的変更

### フェーズ 3 (高度な機能)

- エフェクト機能 (フェード、レインボー、点滅など) のサーバー側実装
- タイマーによる自動更新 (アニメーション)

## 既存機能との競合

### PWM 機能

WS2811 は RMT ペリフェラルを使用し、PWM は LEDC ペリフェラルを使用するため、ハードウェアリソースの競合はありません。

### ADC 機能

WS2811 は出力のみで、ADC は入力のみのため、競合しません。

### 点滅機能

WS2811 モードに設定されたピンは、点滅機能を無効化します (既存のロジックと同様)。

## テスト計画

### 単体テスト

- WS2811 出力モードの有効化 / 無効化
- 1 個の LED に色を設定
- 複数の LED に色を設定
- 不正なパラメータの処理

### 統合テスト

- BLE 経由での LED 制御
- 複数ピンの同時制御
- 他の機能 (GPIO 出力、PWM など) との併用

### 性能テスト

- 最大 LED 個数の確認 (メモリ使用量)
- 更新速度の測定 (FPS)
- BLE MTU サイズの影響

## 参考資料

- [WS2811 データシート](https://cdn-shop.adafruit.com/datasheets/WS2811.pdf)
- [ESP-IDF RMT ドライバ](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-reference/peripherals/rmt.html)
- [ESP32 RMT による WS2812B 制御例](https://github.com/espressif/esp-idf/tree/master/examples/peripherals/rmt/led_strip)

## まとめ

WS2811 シリアル LED 機能を bleio に追加することで、複数の RGB LED を簡単に制御できるようになります。ESP32 の RMT ペリフェラルを使用することで、正確なタイミングでデータを送信でき、既存の PWM や ADC 機能とも競合しません。

実装の難易度は中程度で、ESP-IDF の RMT ドライバを使用すれば比較的容易に実現できます。フェーズ 1 の最小実装から開始し、段階的に機能を拡張していくことを推奨します。
