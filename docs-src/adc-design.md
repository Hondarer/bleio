# ADC (アナログ入力) 機能 詳細設計

## 概要

BLEIO に ADC (Analog-to-Digital Converter) 機能を追加します。ADC は、アナログ電圧を デジタル値に変換する機能であり、センサーからの電圧読み取りや可変抵抗の値取得に使用します。

## 設計方針

プロトコル仕様の推奨に従い、新しい読み取りキャラクタリスティックを追加してアナログ値を取得します。既存の GPIO 読み取りキャラクタリスティックとは分離します。

## プロトコル仕様

### 新しいキャラクタリスティック

**ADC 読み取りキャラクタリスティック**

アナログ電圧を読み取ります。

**UUID**

`2d8a7b3c-4e9f-4a1b-8c5d-6e7f8a9b0c1d`

**プロパティ**

READ

**読み取りデータ形式**

可変長配列 (最小 1 バイト、最大 73 バイト)

**全体構造**

| バイト位置 | 名称 | 型 | 説明 |
|----------|------|-----|------|
| 0 | ADC Count | uint8 | ADC 有効化されているピン数 (0-24) |
| 1 | Pin 1 Number | uint8 | 1 つ目のピン番号 |
| 2 | Pin 1 Value Low | uint8 | 1 つ目のピンの ADC 値下位バイト |
| 3 | Pin 1 Value High | uint8 | 1 つ目のピンの ADC 値上位バイト |
| 4 | Pin 2 Number | uint8 | 2 つ目のピン番号 |
| 5 | Pin 2 Value Low | uint8 | 2 つ目のピンの ADC 値下位バイト |
| 6 | Pin 2 Value High | uint8 | 2 つ目のピンの ADC 値上位バイト |
| ... | ... | ... | 最大 24 ピンまで |

**パケット長の計算**

- パケット長 = 1 (ピン数) + 3 × N (各ピンのデータ)
- 最小: 1 バイト (ADC ピンが 0 個の場合)
- 最大: 1 + 3 × 24 = 73 バイト (ADC ピンが 24 個の場合)

**ADC 値のエンコード**

ADC 値は 12 ビット (0-4095) のため、2 バイトで表現します。

- Value Low: ADC 値の下位 8 ビット (値 & 0xFF)
- Value High: ADC 値の上位 4 ビット (値 >> 8)

実際の電圧 = `(ADC値 / 4095.0) * 減衰率に応じた電圧`

### 新しいコマンド

**SET_ADC_ENABLE (コマンド値 30)**

GPIO を ADC 入力モードに設定し、減衰率を指定します。

**コマンド構造**

| フィールド | 型 | 説明 |
|----------|-----|------|
| Pin Number | uint8 | GPIO ピン番号 (ADC 対応ピンのみ) |
| Command | uint8 | 30 (SET_ADC_ENABLE) |
| Param1 | uint8 | 減衰率 (0-3) |
| Param2 | uint8 | 予約 (将来用、現在は 0x00) |

**Param1: 減衰率 (Attenuation)**

ADC の入力電圧範囲を設定します。

| Param1 値 | 減衰率 | 測定可能電圧範囲 | 用途 |
|----------|--------|----------------|------|
| 0 | 0 dB | 0 - 1.1 V | 低電圧センサー |
| 1 | 2.5 dB | 0 - 1.5 V | 低電圧センサー (拡張) |
| 2 | 6 dB | 0 - 2.2 V | 中電圧センサー |
| 3 | 11 dB | 0 - 3.3 V | 高電圧センサー、バッテリー電圧 (デフォルト) |

**使用例**

GPIO32 を ADC モードに設定し、0-3.3V の範囲で読み取る場合

```text
[0x01, 0x20, 0x1E, 0x03, 0x00]
```

- 0x01: コマンド個数 (1 個)
- 0x20: ピン番号 (GPIO32 = 0x20 = 32)
- 0x1E: コマンド (SET_ADC_ENABLE = 30 = 0x1E)
- 0x03: Param1 (減衰率 = 3 = 11 dB = 0-3.3V)
- 0x00: Param2 (予約)

**SET_ADC_DISABLE (コマンド値 31)**

GPIO の ADC モードを無効化します。

**コマンド構造**

| フィールド | 型 | 説明 |
|----------|-----|------|
| Pin Number | uint8 | GPIO ピン番号 |
| Command | uint8 | 31 (SET_ADC_DISABLE) |
| Param1 | uint8 | 予約 (0x00) |
| Param2 | uint8 | 予約 (0x00) |

## ESP32 ハードウェア仕様

### ADC ユニット

ESP32 には 2 つの ADC ユニットがあります。

**ADC1 (8 チャネル)**

- Wi-Fi や Bluetooth を使用している場合でも動作可能
- GPIO32-39 に接続 (ただし、GPIO37, 38 は使用不可)

**ADC2 (10 チャネル)**

- GPIO0, 2, 4, 12-15, 25-27 に接続
- Wi-Fi または Bluetooth (Classic および BLE) とハードウェアリソースを共有
- Wi-Fi / Bluetooth 使用中は完全に使用不可

**ADC2 が使用できない理由**

ADC2 は Wi-Fi および Bluetooth サブシステムと同じハードウェアリソース (内部マルチプレクサ) を共有しています。このため、Wi-Fi や Bluetooth が有効な場合、ADC2 のピンは ADC として機能せず、読み取りを試みるとエラーが返されるか不正な値が返されます。

これは ESP32 のハードウェア制限であり、ソフトウェアでは回避できません。

**BLEIO での ADC 対応方針**

BLEIO は BLE (Bluetooth Low Energy) を常時使用しているため、ADC2 は完全に使用不可です。したがって、**ADC1 のみをサポート**します。

ADC1 の使用可能なピンは 6 個 (GPIO32, 33, 34, 35, 36, 39) であり、一般的なアナログセンサーの読み取り用途には十分です。

### 分解能

- 12 ビット (0-4095)
- ESP32 の ADC は非線形性があるため、esp_adc_cal ライブラリを使用して補正します

### ADC 対応ピン

**ADC1 チャネル (推奨)**

| GPIO | ADC1 チャネル | 備考 |
|------|--------------|------|
| GPIO32 | ADC1_CH4 | デジタル入出力も可能 |
| GPIO33 | ADC1_CH5 | デジタル入出力も可能 |
| GPIO34 | ADC1_CH6 | 入力専用、プルアップ/プルダウンなし |
| GPIO35 | ADC1_CH7 | 入力専用、プルアップ/プルダウンなし |
| GPIO36 | ADC1_CH0 | 入力専用、プルアップ/プルダウンなし |
| GPIO39 | ADC1_CH3 | 入力専用、プルアップ/プルダウンなし |

**使用を避けるべきピン (ADC2)**

GPIO0, 2, 4, 12-15, 25-27 は ADC2 に接続されています。

ADC2 は BLE (Bluetooth Low Energy) とハードウェアリソースを共有しているため、BLEIO では完全に使用不可です。これらのピンに対して SET_ADC_ENABLE コマンドを送信すると、サーバー側でエラーが返されます。

ADC 機能が必要な場合は、必ず ADC1 対応ピン (GPIO32, 33, 34, 35, 36, 39) を使用してください。

## ソフトウェア設計

### サーバー側 (ESP32)

#### データ構造

**GPIO 状態管理への追加**

```c
typedef enum {
    BLEIO_MODE_UNSET = 0,
    BLEIO_MODE_INPUT_FLOATING,
    BLEIO_MODE_INPUT_PULLUP,
    BLEIO_MODE_INPUT_PULLDOWN,
    BLEIO_MODE_OUTPUT_LOW,
    BLEIO_MODE_OUTPUT_HIGH,
    BLEIO_MODE_BLINK_250MS,
    BLEIO_MODE_BLINK_500MS,
    BLEIO_MODE_PWM,
    BLEIO_MODE_ADC  // 新規追加
} bleio_mode_state_t;
```

**ADC 設定の保持**

各 GPIO の ADC 設定を保持する構造体を追加します。

```c
typedef struct {
    adc1_channel_t channel;      // ADC1 チャネル (-1: 未設定)
    adc_atten_t attenuation;     // 減衰率
    bool calibrated;             // キャリブレーション済みか
    esp_adc_cal_characteristics_t cal_chars;  // キャリブレーション特性
} adc_config_t;

static adc_config_t adc_configs[40] = {0};  // GPIO ごとの ADC 設定
```

**GPIO から ADC1 チャネルへのマッピングテーブル**

```c
static const struct {
    uint8_t gpio_num;
    adc1_channel_t channel;
} adc1_gpio_map[] = {
    {32, ADC1_CHANNEL_4},
    {33, ADC1_CHANNEL_5},
    {34, ADC1_CHANNEL_6},
    {35, ADC1_CHANNEL_7},
    {36, ADC1_CHANNEL_0},
    {39, ADC1_CHANNEL_3}
};
#define ADC1_GPIO_MAP_SIZE (sizeof(adc1_gpio_map) / sizeof(adc1_gpio_map[0]))
```

**減衰率マッピングテーブル**

```c
static const adc_atten_t adc_atten_map[] = {
    ADC_ATTEN_DB_0,    // 0: 0 dB (0-1.1V)
    ADC_ATTEN_DB_2_5,  // 1: 2.5 dB (0-1.5V)
    ADC_ATTEN_DB_6,    // 2: 6 dB (0-2.2V)
    ADC_ATTEN_DB_11    // 3: 11 dB (0-3.3V)
};
#define ADC_ATTEN_MAP_SIZE (sizeof(adc_atten_map) / sizeof(adc_atten_map[0]))
```

#### 関数

**ADC 初期化**

```c
void adc_module_init(void)
{
    // ADC1 の初期化
    adc1_config_width(ADC_WIDTH_BIT_12);  // 12 ビット分解能

    // ADC 設定の初期化
    for (int i = 0; i < 40; i++) {
        adc_configs[i].channel = -1;
        adc_configs[i].attenuation = ADC_ATTEN_DB_11;
        adc_configs[i].calibrated = false;
    }

    ESP_LOGI(TAG, "ADC module initialized (12-bit resolution, ADC1 only)");
}
```

**GPIO から ADC1 チャネルへの変換**

```c
adc1_channel_t gpio_to_adc1_channel(uint8_t gpio_num)
{
    for (int i = 0; i < ADC1_GPIO_MAP_SIZE; i++) {
        if (adc1_gpio_map[i].gpio_num == gpio_num) {
            return adc1_gpio_map[i].channel;
        }
    }
    return -1;  // ADC1 に対応していない
}
```

**ADC 有効化**

```c
esp_err_t gpio_enable_adc(uint8_t pin, uint8_t atten_param)
{
    // パラメータ検証
    adc1_channel_t channel = gpio_to_adc1_channel(pin);
    if (channel < 0) {
        ESP_LOGE(TAG, "GPIO%d は ADC1 に対応していません", pin);
        return ESP_ERR_INVALID_ARG;
    }

    if (atten_param >= ADC_ATTEN_MAP_SIZE) {
        ESP_LOGE(TAG, "無効な減衰率パラメータ: %d (最大: %d)", atten_param, ADC_ATTEN_MAP_SIZE - 1);
        return ESP_ERR_INVALID_ARG;
    }

    // 既存のモードをクリーンアップ
    portENTER_CRITICAL(&gpio_states_mux);
    bleio_mode_state_t prev_mode = gpio_states[pin].mode;
    portEXIT_CRITICAL(&gpio_states_mux);

    if (prev_mode == BLEIO_MODE_PWM) {
        free_ledc_channel(pin);
    }

    // ADC チャネルの設定
    adc_atten_t attenuation = adc_atten_map[atten_param];
    esp_err_t ret = adc1_config_channel_atten(channel, attenuation);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "ADC チャネル設定に失敗しました: %s", esp_err_to_name(ret));
        return ret;
    }

    // キャリブレーション特性の取得
    esp_adc_cal_value_t cal_type = esp_adc_cal_characterize(
        ADC_UNIT_1,
        attenuation,
        ADC_WIDTH_BIT_12,
        1100,  // デフォルト Vref (mV)
        &adc_configs[pin].cal_chars
    );

    // 設定を保存
    adc_configs[pin].channel = channel;
    adc_configs[pin].attenuation = attenuation;
    adc_configs[pin].calibrated = (cal_type != ESP_ADC_CAL_VAL_NOT_SUPPORTED);

    portENTER_CRITICAL(&gpio_states_mux);
    gpio_states[pin].mode = BLEIO_MODE_ADC;
    portEXIT_CRITICAL(&gpio_states_mux);

    const char *cal_type_str =
        (cal_type == ESP_ADC_CAL_VAL_EFUSE_VREF) ? "eFuse Vref" :
        (cal_type == ESP_ADC_CAL_VAL_EFUSE_TP) ? "eFuse Two Point" :
        (cal_type == ESP_ADC_CAL_VAL_DEFAULT_VREF) ? "Default Vref" : "Not Supported";

    const char *range_str =
        (atten_param == 0) ? "0-1.1V" :
        (atten_param == 1) ? "0-1.5V" :
        (atten_param == 2) ? "0-2.2V" : "0-3.3V";

    ESP_LOGI(TAG, "GPIO%d を ADC モードに設定しました (チャネル: %d, 減衰: %d dB, 範囲: %s, キャリブレーション: %s)",
             pin, channel, atten_param, range_str, cal_type_str);

    return ESP_OK;
}
```

**ADC 無効化**

```c
esp_err_t gpio_disable_adc(uint8_t pin)
{
    portENTER_CRITICAL(&gpio_states_mux);
    bleio_mode_state_t mode = gpio_states[pin].mode;
    portEXIT_CRITICAL(&gpio_states_mux);

    if (mode != BLEIO_MODE_ADC) {
        ESP_LOGW(TAG, "GPIO%d は ADC モードではありません", pin);
        return ESP_OK;
    }

    // 設定をクリア
    adc_configs[pin].channel = -1;
    adc_configs[pin].calibrated = false;

    portENTER_CRITICAL(&gpio_states_mux);
    gpio_states[pin].mode = BLEIO_MODE_UNSET;
    portEXIT_CRITICAL(&gpio_states_mux);

    ESP_LOGI(TAG, "GPIO%d の ADC モードを無効化しました", pin);
    return ESP_OK;
}
```

**ADC 値の読み取り**

```c
uint16_t read_adc_value(uint8_t pin)
{
    adc_config_t *config = &adc_configs[pin];

    if (config->channel < 0) {
        ESP_LOGW(TAG, "GPIO%d は ADC モードではありません", pin);
        return 0;
    }

    // 生の ADC 値を読み取り
    int raw = adc1_get_raw(config->channel);

    if (raw < 0) {
        ESP_LOGE(TAG, "GPIO%d の ADC 読み取りに失敗しました", pin);
        return 0;
    }

    // キャリブレーションが有効な場合は補正された値を使用
    if (config->calibrated) {
        uint32_t voltage = esp_adc_cal_raw_to_voltage(raw, &config->cal_chars);
        // 電圧 (mV) を ADC 値に逆変換 (0-4095)
        // 減衰率に応じた最大電圧で正規化
        uint32_t max_voltage =
            (config->attenuation == ADC_ATTEN_DB_0) ? 1100 :
            (config->attenuation == ADC_ATTEN_DB_2_5) ? 1500 :
            (config->attenuation == ADC_ATTEN_DB_6) ? 2200 : 3300;

        uint16_t normalized = (voltage * 4095) / max_voltage;
        return (normalized > 4095) ? 4095 : normalized;
    }

    return (uint16_t)raw;
}
```

**コマンド処理への追加**

```c
// handle_gpio_command 関数に追加
else if (command == CMD_SET_ADC_ENABLE) {
    ret = gpio_enable_adc(pin, param1);  // param1 = attenuation
}
else if (command == CMD_SET_ADC_DISABLE) {
    ret = gpio_disable_adc(pin);
}
```

**ADC 読み取りキャラクタリスティック コールバック**

```c
static int gatt_svr_chr_adc_read_cb(uint16_t conn_handle, uint16_t attr_handle,
                                     struct ble_gatt_access_ctxt *ctxt, void *arg) {
    if (ctxt->op == BLE_GATT_ACCESS_OP_READ_CHR) {
        // READ 操作: すべての ADC モード設定済みピンの値を返す
        uint8_t buffer[1 + MAX_USABLE_GPIO * 3];  // 1 (カウント) + 24 * 3 (ピン番号と ADC 値) = 73 バイト
        uint8_t count = 0;

        // すべての GPIO をスキャンして、ADC モードのピンを収集
        for (int pin = 0; pin < 40; pin++) {
            if (!is_valid_gpio(pin)) {
                continue;
            }

            portENTER_CRITICAL(&gpio_states_mux);
            bleio_mode_state_t mode = gpio_states[pin].mode;
            portEXIT_CRITICAL(&gpio_states_mux);

            // ADC モードかチェック
            if (mode == BLEIO_MODE_ADC) {
                uint16_t adc_value = read_adc_value(pin);

                buffer[1 + count * 3] = pin;
                buffer[1 + count * 3 + 1] = adc_value & 0xFF;        // 下位バイト
                buffer[1 + count * 3 + 2] = (adc_value >> 8) & 0xFF; // 上位バイト
                count++;

                ESP_LOGI(TAG, "ADC GPIO%d: %d (0x%03X)", pin, adc_value, adc_value);
            }
        }

        buffer[0] = count;
        uint16_t data_len = 1 + count * 3;

        ESP_LOGI(TAG, "Sending %d ADC values (%d bytes)", count, data_len);
        int rc = os_mbuf_append(ctxt->om, buffer, data_len);
        return (rc == 0) ? 0 : BLE_ATT_ERR_INSUFFICIENT_RES;
    }

    // WRITE 操作は無効
    return BLE_ATT_ERR_UNLIKELY;
}
```

**GATT サービス定義への追加**

```c
static const struct ble_gatt_svc_def gatt_svr_svcs[] = {
    {
        .type = BLE_GATT_SVC_TYPE_PRIMARY,
        .uuid = &gatt_svr_svc_uuid.u,
        .characteristics = (struct ble_gatt_chr_def[]) {
            {
                // GPIO 書き込みキャラクタリスティック
                .uuid = &gatt_svr_chr_write_uuid.u,
                .access_cb = gatt_svr_chr_write_cb,
                .flags = BLE_GATT_CHR_F_WRITE,
            },
            {
                // GPIO 読み取りキャラクタリスティック
                .uuid = &gatt_svr_chr_read_uuid.u,
                .access_cb = gatt_svr_chr_read_cb,
                .flags = BLE_GATT_CHR_F_READ,
            },
            {
                // ADC 読み取りキャラクタリスティック (新規追加)
                .uuid = &gatt_svr_chr_adc_read_uuid.u,
                .access_cb = gatt_svr_chr_adc_read_cb,
                .flags = BLE_GATT_CHR_F_READ,
            },
            {
                0, // 終端
            }
        },
    },
    {
        0, // 終端
    },
};
```

### クライアント側 (C#)

#### AdcAttenuation 列挙型

```csharp
public enum AdcAttenuation : byte
{
    Atten0dB = 0,      // 0 dB (0-1.1V)
    Atten2_5dB = 1,    // 2.5 dB (0-1.5V)
    Atten6dB = 2,      // 6 dB (0-2.2V)
    Atten11dB = 3      // 11 dB (0-3.3V、デフォルト)
}
```

#### フィールド追加

```csharp
private const string CharAdcReadUuid = "2d8a7b3c-4e9f-4a1b-8c5d-6e7f8a9b0c1d";

private GattCharacteristic? _adcReadCharacteristic;
```

#### 初期化処理への追加

```csharp
// InitializeDeviceAsync メソッド内で ADC 読み取りキャラクタリスティックを取得
var adcReadCharResult = await _service.GetCharacteristicsForUuidAsync(
    Guid.Parse(CharAdcReadUuid));

if (adcReadCharResult.Status == GattCommunicationStatus.Success && adcReadCharResult.Characteristics.Count > 0)
{
    _adcReadCharacteristic = adcReadCharResult.Characteristics[0];
    Console.WriteLine("ADC 読み取りキャラクタリスティックを取得しました");
}
else
{
    // ADC 読み取りキャラクタリスティックはオプション (古いファームウェアでは存在しない可能性)
    Console.WriteLine("ADC 読み取りキャラクタリスティックが見つかりません (古いファームウェアの可能性)");
}
```

#### EnableAdcAsync メソッド

```csharp
/// <summary>
/// GPIO ピンを ADC (アナログ入力) モードに設定します
/// </summary>
/// <param name="pin">GPIO ピン番号 (ADC1 対応ピン: 32, 33, 34, 35, 36, 39)</param>
/// <param name="attenuation">減衰率 (測定可能電圧範囲を決定)</param>
public async Task EnableAdcAsync(byte pin, AdcAttenuation attenuation = AdcAttenuation.Atten11dB)
{
    EnsureConnected();

    // ADC 対応ピンのチェック
    if (pin != 32 && pin != 33 && pin != 34 && pin != 35 && pin != 36 && pin != 39)
    {
        throw new ArgumentException($"GPIO{pin} は ADC1 に対応していません。対応ピン: 32, 33, 34, 35, 36, 39");
    }

    // コマンドを送信 (コマンド 30: SET_ADC_ENABLE)
    await SendCommandsAsync(new[] {
        new GpioCommand(pin, 30, (byte)attenuation, 0)
    });
}
```

#### DisableAdcAsync メソッド

```csharp
/// <summary>
/// GPIO ピンの ADC モードを無効化します
/// </summary>
/// <param name="pin">GPIO ピン番号</param>
public async Task DisableAdcAsync(byte pin)
{
    EnsureConnected();

    // コマンドを送信 (コマンド 31: SET_ADC_DISABLE)
    await SendCommandsAsync(new[] {
        new GpioCommand(pin, 31, 0, 0)
    });
}
```

#### ReadAdcAsync メソッド

```csharp
/// <summary>
/// 指定した GPIO ピンの ADC 値を読み取ります
/// </summary>
/// <param name="pin">GPIO ピン番号</param>
/// <returns>ADC 値 (0-4095)、ピンが ADC モードでない場合は null</returns>
public async Task<ushort?> ReadAdcAsync(byte pin)
{
    EnsureConnected();
    var adcValues = await ReadAllAdcAsync();
    var pinData = adcValues.FirstOrDefault(a => a.Pin == pin);

    if (pinData.Pin == 0 && pin != 0)
    {
        Console.WriteLine($"GPIO{pin} は ADC モードに設定されていません");
        return null;
    }

    return pinData.Value;
}
```

#### ReadAllAdcAsync メソッド

```csharp
/// <summary>
/// ADC モードに設定されているすべての GPIO ピンの値を一括で読み取ります
/// </summary>
/// <returns>ピン番号と ADC 値 (0-4095) のペアの配列</returns>
public async Task<(byte Pin, ushort Value)[]> ReadAllAdcAsync()
{
    EnsureConnected();

    if (_adcReadCharacteristic == null)
    {
        throw new InvalidOperationException("ADC 読み取りキャラクタリスティックが利用できません");
    }

    try
    {
        Console.WriteLine("すべての ADC ピンの値を読み取ります...");

        var readResult = await _adcReadCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

        if (readResult.Status != GattCommunicationStatus.Success)
        {
            string errorMessage = readResult.Status switch
            {
                GattCommunicationStatus.Unreachable => "デバイスに到達できません (接続が切断された可能性があります)",
                GattCommunicationStatus.ProtocolError => "プロトコルエラーが発生しました",
                GattCommunicationStatus.AccessDenied => "アクセスが拒否されました",
                _ => $"読み取りに失敗しました (ステータス: {readResult.Status})"
            };
            throw new InvalidOperationException(errorMessage);
        }

        var reader = DataReader.FromBuffer(readResult.Value);
        byte[] response = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(response);

        if (response.Length < 1)
        {
            throw new InvalidOperationException("受信データが空です");
        }

        byte count = response[0];
        int expectedLen = 1 + count * 3;

        if (response.Length != expectedLen)
        {
            throw new InvalidOperationException(
                $"受信データ長が不正です (期待: {expectedLen} バイト, 実際: {response.Length} バイト)");
        }

        var adcValues = new (byte Pin, ushort Value)[count];
        for (int i = 0; i < count; i++)
        {
            byte pin = response[1 + i * 3];
            ushort value = (ushort)(response[1 + i * 3 + 1] | (response[1 + i * 3 + 2] << 8));
            adcValues[i] = (pin, value);

            // ADC 値を電圧に変換 (11 dB 減衰の場合)
            double voltage = (value / 4095.0) * 3.3;
            Console.WriteLine($"    GPIO{pin}: {value} (約 {voltage:F3} V)");
        }

        Console.WriteLine($"{count} 個の ADC ピンの値を取得しました");
        return adcValues;
    }
    catch (InvalidOperationException)
    {
        throw;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"読み取り中に予期しないエラーが発生しました: {ex.Message}", ex);
    }
}
```

#### 電圧変換ヘルパーメソッド

```csharp
/// <summary>
/// ADC 値を電圧 (V) に変換します
/// </summary>
/// <param name="adcValue">ADC 値 (0-4095)</param>
/// <param name="attenuation">減衰率</param>
/// <returns>電圧 (V)</returns>
public static double AdcToVoltage(ushort adcValue, AdcAttenuation attenuation)
{
    double maxVoltage = attenuation switch
    {
        AdcAttenuation.Atten0dB => 1.1,
        AdcAttenuation.Atten2_5dB => 1.5,
        AdcAttenuation.Atten6dB => 2.2,
        AdcAttenuation.Atten11dB => 3.3,
        _ => 3.3
    };

    return (adcValue / 4095.0) * maxVoltage;
}
```

## 使用例

### サーバー側の使用例

```c
// GPIO32 を ADC モードに設定 (0-3.3V)
gpio_enable_adc(32, 3);

// GPIO34 を ADC モードに設定 (0-1.1V、高精度)
gpio_enable_adc(34, 0);

// ADC 値を読み取り
uint16_t value = read_adc_value(32);
ESP_LOGI(TAG, "GPIO32 ADC value: %d", value);

// ADC を無効化
gpio_disable_adc(32);
```

### クライアント側の使用例

```csharp
// GPIO32 を ADC モードに設定 (0-3.3V)
await client.EnableAdcAsync(32, BleioClient.AdcAttenuation.Atten11dB);

// GPIO34 を ADC モードに設定 (0-1.1V、高精度)
await client.EnableAdcAsync(34, BleioClient.AdcAttenuation.Atten0dB);

// 単一ピンの ADC 値を読み取り
ushort? value = await client.ReadAdcAsync(32);
if (value != null)
{
    double voltage = BleioClient.AdcToVoltage(value.Value, BleioClient.AdcAttenuation.Atten11dB);
    Console.WriteLine($"GPIO32: {value} (約 {voltage:F3} V)");
}

// すべての ADC ピンの値を一括取得
var adcValues = await client.ReadAllAdcAsync();
foreach (var (pin, adcValue) in adcValues)
{
    double voltage = BleioClient.AdcToVoltage(adcValue, BleioClient.AdcAttenuation.Atten11dB);
    Console.WriteLine($"GPIO{pin}: {adcValue} (約 {voltage:F3} V)");
}

// ADC を無効化
await client.DisableAdcAsync(32);
```

## 制限事項

### ADC1 のみサポート

BLEIO は BLE を常時使用しているため、BLE とハードウェアリソースを共有する ADC2 は使用できません。

**使用不可ピン (ADC2)**

GPIO0, 2, 4, 12-15, 25-27

これらのピンに対して SET_ADC_ENABLE コマンドを送信すると、サーバー側でエラーメッセージが出力され、コマンドは無視されます。

**ハードウェア制限の理由**

ADC2 は ESP32 の内部マルチプレクサを Wi-Fi / Bluetooth サブシステムと共有しています。BLE が有効な場合、このマルチプレクサは Bluetooth 通信に占有されるため、ADC2 のピンは ADC として機能しません。これはソフトウェアでは回避できないハードウェア制限です。

### 使用可能なピン数

ADC1 で使用できるピンは 6 個のみです (GPIO32, 33, 34, 35, 36, 39)。

### ADC の精度

ESP32 の ADC は非線形性があり、完全に正確ではありません。esp_adc_cal ライブラリによるキャリブレーションを使用しますが、誤差が残ります。

### サンプリングレート

現在の実装では、クライアントからの READ 要求ごとに ADC 値を読み取ります。高速なサンプリングには対応していません。

## テストケース

### 基本機能テスト

1. GPIO32 を ADC モードに設定 (11 dB 減衰)
2. 3.3V を GPIO32 に印加し、ADC 値が約 4095 であることを確認
3. 0V を GPIO32 に印加し、ADC 値が約 0 であることを確認
4. ADC を無効化し、ADC 値が読み取れないことを確認

### 減衰率テスト

1. GPIO32 を ADC モードに設定 (0 dB 減衰、0-1.1V)
2. 1.0V を印加し、ADC 値が約 3723 (1.0 / 1.1 * 4095) であることを確認
3. GPIO32 を ADC モードに設定 (11 dB 減衰、0-3.3V)
4. 1.0V を印加し、ADC 値が約 1241 (1.0 / 3.3 * 4095) であることを確認

### 複数ピンテスト

1. GPIO32, GPIO33, GPIO34 を ADC モードに設定
2. それぞれ異なる電圧を印加
3. ReadAllAdcAsync() で一括読み取り
4. 各ピンの値が正しいことを確認

## 実装の優先順位

### Phase 1: サーバー側 ADC 基本機能

- ADC モジュール初期化
- GPIO から ADC1 チャネルへのマッピング
- SET_ADC_ENABLE / SET_ADC_DISABLE コマンドの実装
- ADC 読み取りキャラクタリスティックの追加

### Phase 2: クライアント側対応

- AdcAttenuation 列挙型の追加
- EnableAdcAsync / DisableAdcAsync メソッドの実装
- ReadAdcAsync / ReadAllAdcAsync メソッドの実装
- AdcToVoltage ヘルパーメソッドの実装

### Phase 3: ドキュメント更新

- protocol.md の更新 (SET_ADC_ENABLE / SET_ADC_DISABLE コマンド仕様)
- INDEX.md の更新 (クライアント API の追加)
- 使用例の追加

## 参考資料

**ESP-IDF ADC API リファレンス**

https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-reference/peripherals/adc.html

**ESP-IDF ADC キャリブレーション API**

https://docs.espressif.com/projects/esp-idf/en/latest/esp32/api-reference/peripherals/adc_calibration.html

**ADC の基礎**

ADC (Analog-to-Digital Converter) は、アナログ電圧をデジタル値に変換する回路です。

- 分解能 (Resolution): 何段階に分割できるか (ESP32 は 12 ビット = 4096 段階)
- 減衰率 (Attenuation): 入力電圧範囲を調整する (0 dB, 2.5 dB, 6 dB, 11 dB)
- Vref: 基準電圧 (ESP32 は約 1.1V、eFuse に記録されている場合がある)

**使用例**

- センサー読み取り: 温度センサー、光センサー、湿度センサーなど
- バッテリー電圧監視: バッテリー残量の推定
- 可変抵抗: ボリュームやジョイスティックの位置検出
