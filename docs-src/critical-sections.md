# 排他処理とクリティカルセクション

## 概要

複数のタスクや割り込みハンドラが同じデータにアクセスする場合、データの整合性を保つために排他処理が必要です。bleio-server では FreeRTOS のクリティカルセクション機能を使って、共有データへのアクセスを保護しています。

## クリティカルセクションとは

あるコード区間の実行中に、他のタスクや割り込みによって中断されると困る場合、その区間をクリティカルセクション (Critical section) と呼びます。クリティカルセクションに入ると、他のタスクや割り込みがその区間を同時に実行できなくなります。

FreeRTOS では、スピンロック (Spinlock) という仕組みでクリティカルセクションを実現しています。スピンロックは、ロックが解放されるまで待機する排他制御の方法です。

## コンテキストの種類と使い分け

FreeRTOS では、コードが実行される状況 (コンテキスト) によって、使用すべき関数が異なります。

### 通常コンテキスト (タスクコンテキスト)

通常のタスクから実行されるコードです。BLE のコールバック関数や、アプリケーションのメイン処理などが該当します。

**使用する関数**

```c
portMUX_TYPE *mutex = main_get_gpio_states_mutex();
portENTER_CRITICAL(mutex);
// クリティカルセクション内の処理
portEXIT_CRITICAL(mutex);
```

### ISR コンテキスト (割り込みコンテキスト)

タイマー割り込みや GPIO 割り込みなど、割り込みハンドラから実行されるコードです。bleio-server では、周期タイマ (`periodic_timer_callback`) から呼び出される関数が該当します。

**使用する関数**

```c
portMUX_TYPE *mutex = main_get_gpio_states_mutex();
portENTER_CRITICAL_ISR(mutex);
// クリティカルセクション内の処理
portEXIT_CRITICAL_ISR(mutex);
```

### 使い分けのルール

- **通常のタスクから呼ばれる関数**: `portENTER_CRITICAL` / `portEXIT_CRITICAL`
- **割り込みハンドラから呼ばれる関数**: `portENTER_CRITICAL_ISR` / `portEXIT_CRITICAL_ISR`

間違った関数を使うと、スピンロックの二重取得エラーが発生し、システムがクラッシュします。

## bleio における実装

### 共有データ

bleio-server では、GPIO の状態を管理する `bleio_gpio_state_t` 構造体の配列を複数のコンテキストから参照します。

```c
typedef struct
{
    bleio_mode_state_t mode;       // 現在のモード
    uint8_t current_level;         // 現在の出力レベル
    uint8_t latch_mode;            // 入力ラッチモード
    bool is_latched;               // ラッチ済みフラグ
    uint8_t stable_counter;        // 安定カウンタ
    uint8_t last_level;            // 前回の読み取り値
    uint8_t disconnect_behavior;   // BLE 切断時の振る舞い
} bleio_gpio_state_t;
```

この構造体へのアクセスは、必ずクリティカルセクション内で行います。

### コンテキストごとの使用例

#### 通常コンテキスト (BLE コールバック)

BLE の書き込みコールバック関数は通常コンテキストで実行されます。

```c
// ble_service.c
static int gatt_svr_chr_write_cb(...)
{
    // コマンド処理
    ret = gpio_basic_write_level(pin, command);
    // ...
}

// gpio_basic.c
esp_err_t gpio_basic_write_level(uint8_t pin, uint8_t command)
{
    portMUX_TYPE *mutex = main_get_gpio_states_mutex();
    bleio_gpio_state_t *state = main_get_gpio_state(pin);

    portENTER_CRITICAL(mutex);  // 通常コンテキスト用
    state->mode = new_mode;
    state->current_level = level;
    portEXIT_CRITICAL(mutex);

    gpio_set_level(pin, level);
    return ESP_OK;
}
```

#### ISR コンテキスト (タイマー割り込み)

周期タイマのコールバックは ISR コンテキストで実行されます。

```c
// main.c
static void periodic_timer_callback(void *arg)
{
    // この関数は ISR コンテキストで実行される
    gpio_basic_update_blink_outputs(global_blink_counter);
    gpio_basic_update_input_latches();
    ws2812b_update_patterns(global_blink_counter);
}

// gpio_basic.c
void gpio_basic_update_blink_outputs(uint8_t global_blink_counter)
{
    portMUX_TYPE *mutex = main_get_gpio_states_mutex();

    for (int pin = 0; pin < 40; pin++)
    {
        bleio_gpio_state_t *state = main_get_gpio_state(pin);

        portENTER_CRITICAL_ISR(mutex);  // ISR コンテキスト用
        bleio_mode_state_t mode = state->mode;
        portEXIT_CRITICAL_ISR(mutex);

        // モードに応じた処理
    }
}

// ws28xx.c
void ws2812b_update_patterns(uint8_t blink_counter)
{
    for (int pin = 0; pin < 40; pin++)
    {
        portMUX_TYPE *mutex = main_get_gpio_states_mutex();
        bleio_gpio_state_t *state = main_get_gpio_state(pin);

        portENTER_CRITICAL_ISR(mutex);  // ISR コンテキスト用
        bleio_mode_state_t mode = state->mode;
        portEXIT_CRITICAL_ISR(mutex);

        // パターン更新処理
    }
}
```

## よくある間違いと対策

### 間違い: ISR コンテキストで通常の関数を使う

```c
// ❌ 間違った実装
static void periodic_timer_callback(void *arg)
{
    portMUX_TYPE *mutex = main_get_gpio_states_mutex();
    portENTER_CRITICAL(mutex);  // ISR では使えない！
    // 処理
    portEXIT_CRITICAL(mutex);
}
```

**エラーメッセージ**

```text
assert failed: spinlock_acquire spinlock.h:142 (lock->count == 0)
```

**対策**

ISR コンテキストでは、必ず `_ISR` 付きの関数を使います。

```c
// ✅ 正しい実装
static void periodic_timer_callback(void *arg)
{
    portMUX_TYPE *mutex = main_get_gpio_states_mutex();
    portENTER_CRITICAL_ISR(mutex);  // ISR 用の関数を使う
    // 処理
    portEXIT_CRITICAL_ISR(mutex);
}
```

### 間違い: クリティカルセクションの範囲が広すぎる

クリティカルセクション内では他のタスクや割り込みが停止するため、範囲を最小限にする必要があります。

```c
// ❌ 範囲が広すぎる
portENTER_CRITICAL(mutex);
uint8_t mode = state->mode;
uint8_t level = state->current_level;
// 時間のかかる処理
vTaskDelay(100);
gpio_set_level(pin, level);
portEXIT_CRITICAL(mutex);
```

**対策**

共有データへのアクセスだけをクリティカルセクションで保護します。

```c
// ✅ 最小限の範囲
portENTER_CRITICAL(mutex);
uint8_t mode = state->mode;
uint8_t level = state->current_level;
portEXIT_CRITICAL(mutex);

// クリティカルセクション外で時間のかかる処理
vTaskDelay(100);
gpio_set_level(pin, level);
```

### 間違い: クリティカルセクションのネスト

同じミューテックスを二重に取得しようとすると、デッドロックやアサーションエラーが発生します。

```c
// ❌ ネストした呼び出し
void function_a(void)
{
    portENTER_CRITICAL(mutex);
    function_b();  // function_b も同じミューテックスを取得
    portEXIT_CRITICAL(mutex);
}

void function_b(void)
{
    portENTER_CRITICAL(mutex);  // 二重取得！
    // 処理
    portEXIT_CRITICAL(mutex);
}
```

**対策**

ロックが必要な最小単位の関数を作り、呼び出し側ではロックを取らないようにします。または、ロックを取得する関数とロックなしで呼び出せる内部関数を分離します。

```c
// ✅ ロックなしの内部関数を分離
static void internal_function_b(bleio_gpio_state_t *state)
{
    // ロックは呼び出し側で取得済み前提
    state->mode = new_mode;
}

void function_a(void)
{
    portENTER_CRITICAL(mutex);
    internal_function_b(state);
    portEXIT_CRITICAL(mutex);
}

void function_b(void)
{
    portENTER_CRITICAL(mutex);
    internal_function_b(state);
    portEXIT_CRITICAL(mutex);
}
```

## デバッグ方法

### アサーションエラーの確認

スピンロックの問題が発生すると、以下のようなアサーションエラーが表示されます。

```text
assert failed: spinlock_acquire spinlock.h:142 (lock->count == 0)

Backtrace: 0x40082fcd:0x3ffba990 0x4008f9b5:0x3ffba9b0 ...
```

このエラーが発生した場合、以下を確認します。

1. ISR コンテキストで `portENTER_CRITICAL` を使っていないか
2. 通常コンテキストで `portENTER_CRITICAL_ISR` を使っていないか
3. クリティカルセクションがネストしていないか

### 呼び出し元の確認

エラーが発生した場合、バックトレースから呼び出し元を確認します。

- タイマーコールバック (`periodic_timer_callback`) から呼ばれている → ISR コンテキスト
- BLE コールバック (`gatt_svr_chr_write_cb`) から呼ばれている → 通常コンテキスト

## まとめ

bleio-server における排他処理のポイント：

1. 共有データへのアクセスは必ずクリティカルセクションで保護する
2. 通常コンテキストでは `portENTER_CRITICAL` / `portEXIT_CRITICAL` を使う
3. ISR コンテキストでは `portENTER_CRITICAL_ISR` / `portEXIT_CRITICAL_ISR` を使う
4. クリティカルセクションの範囲は最小限にする
5. クリティカルセクションのネストを避ける

これらのルールを守ることで、マルチタスク環境でも安全にデータを共有できます。

## 関連ドキュメント

- [FreeRTOS Documentation - Critical Sections](https://www.freertos.org/Documentation/02-Kernel/02-Kernel-features/03-Direct-to-task-notifications/01-Task-notifications-as-binary-semaphores)
- [ESP-IDF Programming Guide - FreeRTOS](https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-reference/system/freertos.html)
