# BLEIO サーバーコードリファクタリング設計

BLEIO サーバーの main.c が肥大化しているため、責務別にファイルを分割して保守性を高める設計です。

## 現状の問題点

**main.c の規模**

- 行数: 2680 行
- 関数数: 約 50 個
- 責務: BLE サービス、GPIO 制御、PWM、ADC、WS2812B LED 制御がすべて 1 ファイルに混在

**問題**

- 単一ファイルが大きすぎて見通しが悪い
- 責務が明確に分離されていない
- 変更時の影響範囲が把握しにくい
- テストが困難
- 複数人での並行開発が難しい

## 分割方針

責務ごとにファイルを分割し、以下の原則に従います。

**単一責任の原則 (SRP: Single Responsibility Principle)**

各モジュールは単一の責務のみを持ちます。

**依存関係の明確化**

モジュール間の依存関係をヘッダーファイルで明示します。

**状態管理の集約**

グローバル状態は main.c に集約し、各モジュールはアクセサ関数経由でアクセスします。

**公開インターフェースの最小化**

各モジュールは必要最小限の関数のみを公開します。

## モジュール構成

分割後のファイル構成は以下の通りです。

```text
bleio-server/src/
|-- main.c                  # アプリケーションエントリポイント、グローバル状態管理
|-- main.h                  # グローバル定義、共通型定義
|-- ble_service.c           # BLE GATT サービス
|-- ble_service.h
|-- gpio_basic.c            # GPIO 基本操作 (入出力、点滅)
|-- gpio_basic.h
|-- gpio_pwm.c              # PWM 制御
|-- gpio_pwm.h
|-- gpio_adc.c              # ADC 読み取り
|-- gpio_adc.h
|-- ws2812b.c               # WS2812B LED 制御
|-- ws2812b.h
```

## 各モジュールの詳細

### main.c / main.h

**責務**

- アプリケーションのエントリポイント (`app_main`)
- グローバル状態管理 (GPIO 状態配列、設定配列)
- タイマーの初期化と管理
- 各モジュールの初期化呼び出し

**公開インターフェース (main.h)**

```c
// GPIO モード状態の定義
typedef enum {
    BLEIO_MODE_UNSET = 0,
    BLEIO_MODE_INPUT_FLOATING,
    BLEIO_MODE_INPUT_PULLUP,
    BLEIO_MODE_INPUT_PULLDOWN,
    BLEIO_MODE_OUTPUT,
    BLEIO_MODE_OUTPUT_PWM,
    BLEIO_MODE_ADC,
    BLEIO_MODE_WS2812B
} bleio_mode_state_t;

// GPIO 状態構造体
typedef struct {
    bleio_mode_state_t mode;
    uint8_t current_level;
    uint8_t disconnect_behavior;
    uint8_t latch_mode;
} bleio_gpio_state_t;

// グローバル状態へのアクセサ
bleio_gpio_state_t* get_gpio_state(uint8_t pin);
portMUX_TYPE* get_gpio_states_mutex(void);
uint16_t get_conn_handle(void);
void set_conn_handle(uint16_t handle);

// 点滅カウンタ
uint8_t get_global_blink_counter(void);
uint8_t get_prev_blink_counter(void);
void set_prev_blink_counter(uint8_t value);

// GPIO 検証
bool is_valid_gpio(uint8_t pin);
bool is_valid_output_pin(uint8_t pin);
```

**内部データ**

```c
static bleio_gpio_state_t gpio_states[40];
static portMUX_TYPE gpio_states_mux;
static uint16_t conn_handle;
static uint8_t global_blink_counter;
static uint8_t prev_blink_counter;
static esp_timer_handle_t blink_timer;
static esp_timer_handle_t input_poll_timer;
```

### ble_service.c / ble_service.h

**責務**

- BLE GATT サービスの実装
- ペアリングと認証の管理
- 広告 (Advertise) の制御
- BLE イベントハンドリング
- GPIO コマンドの解釈と各モジュールへの委譲

**公開インターフェース**

```c
// BLE サービス初期化
void ble_service_init(void);

// 認証モード管理
bool is_auth_enabled(void);
bool is_pairing_mode_requested(void);
void clear_bonding_info(void);

// コマンド処理 (内部で各モジュールを呼び出す)
int handle_gpio_write_command(const uint8_t *data, uint16_t len);
int handle_gpio_read_command(uint8_t pin, uint8_t *out_value);
int handle_adc_read_command(uint8_t pin, uint16_t *out_value);
```

**依存関係**

- main.h (グローバル状態アクセス)
- gpio_basic.h
- gpio_pwm.h
- gpio_adc.h
- ws2812b.h

### gpio_basic.c / gpio_basic.h

**責務**

- GPIO の入出力モード設定
- GPIO のデジタル出力 (HIGH/LOW)
- 点滅パターンの実装
- 入力ポーリング
- 切断時の動作設定

**公開インターフェース**

```c
// GPIO モード設定
esp_err_t gpio_basic_set_mode(uint8_t pin, uint8_t command, uint8_t latch_mode);

// デジタル出力
esp_err_t gpio_basic_write_level(uint8_t pin, uint8_t command);

// 点滅
esp_err_t gpio_basic_start_blink(uint8_t pin, uint8_t command);

// 切断時の動作
esp_err_t gpio_basic_set_disconnect_behavior(uint8_t pin, uint8_t behavior);

// 初期化
void gpio_basic_init(void);

// タイマーコールバック
void gpio_basic_blink_timer_callback(void);
void gpio_basic_input_poll_timer_callback(void);
```

**依存関係**

- main.h (グローバル状態アクセス)

### gpio_pwm.c / gpio_pwm.h

**責務**

- PWM 出力の制御
- LEDC チャネルの管理 (割り当て、解放)
- PWM パラメータの設定 (デューティサイクル、周波数)

**公開インターフェース**

```c
// PWM 設定構造体
typedef struct {
    uint8_t duty_cycle;
    uint8_t freq_preset;
} pwm_config_t;

// LEDC チャネル管理構造体
typedef struct {
    bool in_use;
    uint8_t gpio_num;
} ledc_channel_info_t;

// PWM 制御
esp_err_t gpio_pwm_set(uint8_t pin, uint8_t duty_cycle, uint8_t freq_preset);
void gpio_pwm_stop(uint8_t pin);

// 初期化
void gpio_pwm_init(void);

// 状態アクセサ
pwm_config_t* gpio_pwm_get_config(uint8_t pin);
ledc_channel_info_t* gpio_pwm_get_ledc_channels(void);
```

**内部データ**

```c
static pwm_config_t pwm_configs[40];
static ledc_channel_info_t ledc_channels[LEDC_CHANNEL_MAX];
```

**依存関係**

- main.h (グローバル状態アクセス)

### gpio_adc.c / gpio_adc.h

**責務**

- ADC 入力の初期化と制御
- ADC チャネルの管理
- ADC 値の読み取り
- GPIO から ADC チャネルへのマッピング

**公開インターフェース**

```c
// ADC 設定構造体
typedef struct {
    int8_t channel;
    adc_atten_t attenuation;
    adc_bitwidth_t resolution;
    uint16_t last_value;
} adc_config_t;

// ADC 制御
esp_err_t gpio_adc_enable(uint8_t pin, uint8_t atten_param);
esp_err_t gpio_adc_disable(uint8_t pin);
uint16_t gpio_adc_read_value(uint8_t pin);
void gpio_adc_stop(uint8_t pin);

// 初期化
void gpio_adc_init(void);

// 状態アクセサ
adc_config_t* gpio_adc_get_config(uint8_t pin);
```

**内部データ**

```c
static adc_config_t adc_configs[40];
static adc_oneshot_unit_handle_t adc1_handle;
```

**依存関係**

- main.h (グローバル状態アクセス)

### ws2812b.c / ws2812b.h

**責務**

- WS2812B LED の制御
- RMT エンコーダの実装
- LED パターンの実装 (ON, BLINK, RAINBOW, FLICKER)
- 色変換 (RGB ↔ HSV)
- ガンマ補正

**公開インターフェース**

```c
// パターン定義
#define WS2812B_PATTERN_ON 0
#define WS2812B_PATTERN_BLINK_250MS 1
#define WS2812B_PATTERN_BLINK_500MS 2
#define WS2812B_PATTERN_RAINBOW 3
#define WS2812B_PATTERN_FLICKER 4
#define WS2812B_PATTERN_UNSET 0xFF

// LED パターン構造体
typedef struct {
    uint8_t pattern_type;
    uint8_t pattern_param1;
    uint8_t pattern_param2;
    uint16_t hue;
} ws2812b_led_pattern_t;

// FLICKER データ構造
typedef struct {
    uint16_t base_hue;
    uint8_t base_sat;
    uint8_t base_val;
    uint32_t seed;
    uint8_t val_ema;
    uint16_t hue_ema;
    uint8_t val_target;
    uint16_t hue_target;
    uint8_t tick;
} led_flicker_data_t;

// WS2812B 設定構造体
typedef struct {
    uint16_t num_leds;
    uint8_t brightness;
    rmt_channel_handle_t rmt_channel;
    rmt_encoder_handle_t rmt_encoder;
    uint8_t *led_data;
    ws2812b_led_pattern_t gpio_pattern;
    ws2812b_led_pattern_t *led_patterns;
    uint8_t *base_colors;
    led_flicker_data_t *flicker_data;
} ws2812b_config_t;

// WS2812B 制御
esp_err_t ws2812b_enable(uint8_t pin, uint16_t num_leds, uint8_t brightness);
esp_err_t ws2812b_set_color(uint8_t pin, uint16_t led_index, uint8_t r, uint8_t g, uint8_t b);
esp_err_t ws2812b_set_pattern(uint8_t pin, uint8_t led_index, uint8_t pattern_type,
                               uint8_t param1, uint8_t param2);
void ws2812b_stop(uint8_t pin);

// 初期化
void ws2812b_init(void);

// パターン更新 (タイマーから呼び出される)
void ws2812b_update_patterns(void);

// 状態アクセサ
ws2812b_config_t* ws2812b_get_config(uint8_t pin);
```

**内部データ**

```c
static ws2812b_config_t ws2812b_configs[40];
```

**内部関数**

```c
// 色変換
static uint32_t gamma32(uint32_t rgb);
static void rgb_to_hsv(uint8_t r, uint8_t g, uint8_t b, uint16_t *h, uint8_t *s, uint8_t *v);
static uint32_t ws2812b_color_hsv(uint16_t hue, uint8_t sat, uint8_t val);

// 疑似乱数 (FLICKER 用)
static uint8_t flicker_rand(uint32_t *state);

// RMT エンコーダ
static esp_err_t ws2812b_encoder_new(rmt_encoder_handle_t *ret_encoder);
static size_t ws2812b_encoder_encode(...);
static esp_err_t ws2812b_encoder_reset(...);
static esp_err_t ws2812b_encoder_del(...);

// ヘルパー関数
static esp_err_t ws2812b_turn_off_all_leds(uint8_t pin);
```

**依存関係**

- main.h (グローバル状態アクセス)

## データ構造の配置

**main.h (グローバル定義)**

- `bleio_mode_state_t` (列挙型)
- `bleio_gpio_state_t` (GPIO 状態)

**gpio_pwm.h**

- `pwm_config_t`
- `ledc_channel_info_t`

**gpio_adc.h**

- `adc_config_t`

**ws2812b.h**

- `ws2812b_led_pattern_t`
- `led_flicker_data_t`
- `ws2812b_config_t`

## 依存関係図

```text
main.c
  |
  +-- ble_service.c
  |     |
  |     +-- gpio_basic.c --> main.h
  |     +-- gpio_pwm.c   --> main.h
  |     +-- gpio_adc.c   --> main.h
  |     +-- ws2812b.c    --> main.h
  |
  +-- gpio_basic.c --> main.h
  +-- gpio_pwm.c   --> main.h
  +-- gpio_adc.c   --> main.h
  +-- ws2812b.c    --> main.h
```

すべてのモジュールは main.h に依存しますが、モジュール間の直接依存はありません。ble_service.c のみが他のモジュールを呼び出します。

## 移行手順

分割作業は以下の手順で段階的に実施します。

### フェーズ 1: ヘッダーファイルの作成

1. main.h を作成し、共通型定義を移動
2. 各モジュールのヘッダーファイルを作成
3. 公開インターフェースを宣言

### フェーズ 2: モジュールの実装 (依存関係の少ない順)

1. gpio_adc.c の作成 (他モジュールへの依存なし)
2. gpio_pwm.c の作成 (他モジュールへの依存なし)
3. ws2812b.c の作成 (他モジュールへの依存なし)
4. gpio_basic.c の作成 (他モジュールへの依存なし)
5. ble_service.c の作成 (すべてのモジュールに依存)

### フェーズ 3: main.c の整理

1. 各モジュールに移動した関数を main.c から削除
2. グローバル状態管理とアクセサのみを残す
3. app_main を簡潔化

### フェーズ 4: ビルドとテスト

1. ビルドエラーの解消
2. 動作確認
3. メモリ使用量の確認

### フェーズ 5: ドキュメント更新

1. コード構造の説明を README に追加
2. 各モジュールの役割をコメントに記載

## 期待される効果

**保守性の向上**

- 各モジュールの責務が明確になる
- 変更時の影響範囲が限定される

**可読性の向上**

- ファイルサイズが適切になる (各ファイル 300〜600 行程度)
- 関連する機能がまとまる

**テスト容易性の向上**

- 各モジュールを独立してテストできる
- モックの作成が容易になる

**並行開発の促進**

- 異なるモジュールを並行して開発できる
- コンフリクトが減少する

## 注意事項

**静的変数のスコープ**

各モジュールの内部データは static で宣言し、アクセサ関数経由でのみアクセス可能にします。

**ヘッダーのインクルード順序**

main.h を最初にインクルードし、その後に各モジュールのヘッダーをインクルードします。

**コンパイル単位の増加**

ファイル数が増えるため、ビルド時間が若干増加する可能性があります。

**メモリ使用量**

分割によるメモリ使用量の増加はほとんどありません (関数のインライン化が減る程度)。

## まとめ

main.c を責務別に 6 つのファイルに分割することで、コードの保守性、可読性、テスト容易性を大幅に向上させます。分割は段階的に実施し、各フェーズでビルドとテストを行うことで、安全に移行できます。
