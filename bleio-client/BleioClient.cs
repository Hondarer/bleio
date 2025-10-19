using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace Hondarersoft.Bleio
{
    public class BleioClient : IDisposable
    {
        private const string ServiceUuid = "4fafc201-1fb5-459e-8fcc-c5c9c333914b";
        private const string CharGpioWriteUuid = "beb5483e-36e1-4688-b7f5-ea07361b26a8";
        private const string CharGpioReadUuid = "1c95d5e3-d8f7-413a-bf3d-7a2e5d7be87e";
        private const string CharAdcReadUuid = "2d8a7b3c-4e9f-4a1b-8c5d-6e7f8a9b0c1d";

        private BluetoothLEDevice? _device;
        private GattDeviceService? _service;
        private GattCharacteristic? _writeCharacteristic;
        private GattCharacteristic? _readCharacteristic;
        private GattCharacteristic? _adcReadCharacteristic;
        private bool _isConnected = false;

        public async Task<bool> ConnectAsync(string deviceName = "BLEIO")
        {
            try
            {
                // デバイスを検索
                var selector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(deviceName);
                var devices = await DeviceInformation.FindAllAsync(selector);

                if (devices.Count == 0)
                {
                    Console.WriteLine($"デバイス '{deviceName}' が見つかりません");
                    return false;
                }

                // デバイスに接続
                _device = await BluetoothLEDevice.FromIdAsync(devices[0].Id);

                if (_device == null)
                {
                    Console.WriteLine("デバイスへの接続に失敗しました");
                    return false;
                }

                return await InitializeDeviceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接続エラー({ex.ToString()}): {ex.Message}");
                Console.WriteLine($"スタックトレース:\n{ex.StackTrace}");
                CleanupResources();
                return false;
            }
        }

        public async Task<bool> ConnectByMacAddressAsync(string macAddress)
        {
            // "aa:bb:cc:dd:ee:ff" 形式の文字列を ulong に変換
            try
            {
                var bytes = macAddress.Split(':')
                    .Select(hex => Convert.ToByte(hex, 16))
                    .ToArray();

                if (bytes.Length != 6)
                {
                    Console.WriteLine($"無効な MAC アドレス形式です: {macAddress} (期待: aa:bb:cc:dd:ee:ff)");
                    return false;
                }

                ulong bluetoothAddress = 0;
                for (int i = 0; i < 6; i++)
                {
                    bluetoothAddress |= ((ulong)bytes[i]) << (8 * (5 - i));
                }

                return await ConnectByMacAddressAsync(bluetoothAddress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MAC アドレスの解析に失敗しました: {macAddress}");
                Console.WriteLine($"エラー: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConnectByMacAddressAsync(ulong bluetoothAddress)
        {
            try
            {
                // MAC アドレスの表示
                var macString = string.Join(":",
                    Enumerable.Range(0, 6)
                        .Select(i => ((bluetoothAddress >> (8 * (5 - i))) & 0xFF).ToString("x2")));
                Console.WriteLine($"MAC アドレス {macString} のデバイスに接続を試みています...");

                // MAC アドレスから直接接続
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);

                if (_device == null)
                {
                    Console.WriteLine($"MAC アドレス {macString} のデバイスへの接続に失敗しました");
                    return false;
                }

                return await InitializeDeviceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接続エラー({ex.ToString()}): {ex.Message}");
                Console.WriteLine($"スタックトレース:\n{ex.StackTrace}");
                CleanupResources();
                return false;
            }
        }

        private async Task<bool> InitializeDeviceAsync()
        {
            try
            {
                if (_device == null)
                {
                    Console.WriteLine("デバイスが初期化されていません");
                    return false;
                }

                Console.WriteLine($"デバイスに接続しました: {_device.Name}");

                // MAC アドレスをコロン区切りで表示
                var macAddress = _device.BluetoothAddress;
                var macString = string.Join(":",
                    Enumerable.Range(0, 6)
                        .Select(i => ((macAddress >> (8 * (5 - i))) & 0xFF).ToString("x2")));
                Console.WriteLine($"  Bluetooth アドレス: {macString}");
                Console.WriteLine($"  デバイス ID: {_device.DeviceId}");

                // GATT サービスを取得
                Console.WriteLine("すべての GATT サービスを確認しています...");

                var allServicesResult = await _device.GetGattServicesAsync();
                if (allServicesResult.Status == GattCommunicationStatus.Success)
                {
                    Console.WriteLine($"見つかったサービス数: {allServicesResult.Services.Count}");
                    var targetUuid = Guid.Parse(ServiceUuid);

                    foreach (var svc in allServicesResult.Services)
                    {
                        Console.WriteLine($"  - UUID: {svc.Uuid}");
                        if (svc.Uuid == targetUuid)
                        {
                            _service = svc;
                            Console.WriteLine($"    - 目的のサービスを発見しました");
                        }
                        else
                        {
                            // 使用しないサービスは即座に破棄
                            svc.Dispose();
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"サービスの列挙に失敗しました: {allServicesResult.Status}");
                }

                if (_service == null)
                {
                    Console.WriteLine($"サービス UUID {ServiceUuid} が見つかりませんでした");
                    CleanupResources();
                    return false;
                }

                // 書き込み用キャラクタリスティックを取得
                var writeCharResult = await _service.GetCharacteristicsForUuidAsync(
                    Guid.Parse(CharGpioWriteUuid));

                if (writeCharResult.Status == GattCommunicationStatus.Success && writeCharResult.Characteristics.Count > 0)
                {
                    _writeCharacteristic = writeCharResult.Characteristics[0];
                    Console.WriteLine("書き込み用キャラクタリスティックを取得しました");
                }
                else
                {
                    Console.WriteLine("書き込み用キャラクタリスティックの取得に失敗しました");
                    CleanupResources();
                    return false;
                }

                // 読み取り用キャラクタリスティックを取得
                var readCharResult = await _service.GetCharacteristicsForUuidAsync(
                    Guid.Parse(CharGpioReadUuid));

                if (readCharResult.Status == GattCommunicationStatus.Success && readCharResult.Characteristics.Count > 0)
                {
                    _readCharacteristic = readCharResult.Characteristics[0];
                    Console.WriteLine("読み取り用キャラクタリスティックを取得しました");
                }
                else
                {
                    Console.WriteLine("読み取り用キャラクタリスティックの取得に失敗しました");
                    CleanupResources();
                    return false;
                }

                // ADC 読み取り用キャラクタリスティックを取得
                var adcReadCharResult = await _service.GetCharacteristicsForUuidAsync(
                    Guid.Parse(CharAdcReadUuid));

                if (adcReadCharResult.Status == GattCommunicationStatus.Success && adcReadCharResult.Characteristics.Count > 0)
                {
                    _adcReadCharacteristic = adcReadCharResult.Characteristics[0];
                    Console.WriteLine("ADC 読み取り用キャラクタリスティックを取得しました");
                }

                Console.WriteLine("GATT サービスの初期化が完了しました");

                // 接続状態変化のイベントハンドラを登録
                _device.ConnectionStatusChanged += OnConnectionStatusChanged;
                _isConnected = true;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"デバイス初期化エラー: {ex.Message}");
                Console.WriteLine($"スタックトレース:\n{ex.StackTrace}");
                CleanupResources();
                return false;
            }
        }

        private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                Console.WriteLine($"デバイスとの接続が切断されました: {sender.Name}");
                _isConnected = false;
            }
            else if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                Console.WriteLine($"デバイスに再接続しました: {sender.Name}");
                _isConnected = true;
            }
        }

        private void CleanupResources()
        {
            _writeCharacteristic = null;
            _readCharacteristic = null;

            _service?.Dispose();
            _service = null;

            if (_device != null)
            {
                _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _device.Dispose();
                _device = null;
            }

            _isConnected = false;

            Console.WriteLine("リソースをクリーンアップしました");
        }

        private void EnsureConnected()
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("デバイスとの接続が切断されています。再接続してください。");
            }
        }

        public async Task SetPinModeAsync(byte pin, PinMode mode, LatchMode latchMode = LatchMode.None)
        {
            EnsureConnected();
            await SendCommandsAsync(new[] { new GpioCommand(pin, (byte)mode, (byte)latchMode, 0) });
        }

        public async Task SendCommandsAsync(GpioCommand[] commands)
        {
            EnsureConnected();

            if (_writeCharacteristic == null)
            {
                throw new InvalidOperationException("デバイスに接続されていません");
            }

            if (commands.Length == 0 || commands.Length > 24)
            {
                throw new ArgumentException("コマンド数は 1-24 の範囲で指定してください");
            }

            var writer = new DataWriter();

            // コマンド個数を書き込む
            writer.WriteByte((byte)commands.Length);

            // 各コマンドを書き込む (ピン番号、コマンド、パラメータ1、パラメータ2)
            foreach (var cmd in commands)
            {
                writer.WriteByte(cmd.Pin);
                writer.WriteByte(cmd.Command);
                writer.WriteByte(cmd.Param1);
                writer.WriteByte(cmd.Param2);
            }

            var result = await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer());

            if (result == GattCommunicationStatus.Success)
            {
                Console.WriteLine($"{commands.Length} 個のコマンドを送信しました");
                foreach (var cmd in commands)
                {
                    Console.WriteLine($"    {cmd.Pin}, {cmd.Command}, {cmd.Param1}, {cmd.Param2}");
                }
            }
            else
            {
                string errorMessage = result switch
                {
                    GattCommunicationStatus.Unreachable => "デバイスに到達できません (接続が切断された可能性があります)",
                    GattCommunicationStatus.ProtocolError => "プロトコルエラーが発生しました",
                    GattCommunicationStatus.AccessDenied => "アクセスが拒否されました",
                    _ => $"コマンドの送信に失敗しました (ステータス: {result})"
                };
                throw new InvalidOperationException(errorMessage);
            }
        }

        public async Task DigitalWriteAsync(byte pin, bool value)
        {
            EnsureConnected();
            byte command = value ? (byte)11 : (byte)10;
            await SendCommandsAsync(new[] { new GpioCommand(pin, command, 0, 0) });
        }

        public async Task<bool?> DigitalReadAsync(byte pin)
        {
            EnsureConnected();
            var inputs = await ReadAllInputsAsync();
            var pinData = inputs.FirstOrDefault(i => i.Pin == pin);

            if (pinData.Pin == 0 && pin != 0)
            {
                Console.WriteLine($"GPIO{pin} は入力モードに設定されていません");
                return null;
            }

            return pinData.State;
        }

        public async Task<(byte Pin, bool State)[]> ReadAllInputsAsync()
        {
            EnsureConnected();

            if (_readCharacteristic == null)
            {
                throw new InvalidOperationException("デバイスに接続されていません");
            }

            try
            {
                Console.WriteLine("すべての入力ピンの状態を読み取ります...");

                var readResult = await _readCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

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
                int expectedLen = 1 + count * 2;

                if (response.Length != expectedLen)
                {
                    throw new InvalidOperationException(
                        $"受信データ長が不正です (期待: {expectedLen} バイト, 実際: {response.Length} バイト)");
                }

                var inputs = new (byte Pin, bool State)[count];
                for (int i = 0; i < count; i++)
                {
                    byte pin = response[1 + i * 2];
                    bool state = response[1 + i * 2 + 1] != 0;
                    inputs[i] = (pin, state);
                    Console.WriteLine($"    GPIO{pin}: {(state ? "HIGH" : "LOW")}");
                }

                Console.WriteLine($"{count} 個の入力ピンの状態を取得しました");
                return inputs;
            }
            catch (InvalidOperationException)
            {
                // 既に適切なエラーメッセージを持つ例外なので、そのまま再スロー
                throw;
            }
            catch (Exception ex)
            {
                // その他の予期しない例外
                throw new InvalidOperationException($"読み取り中に予期しないエラーが発生しました: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            CleanupResources();
        }

        public enum PinMode : byte
        {
            Output = 0,
            InputFloating = 1,
            InputPullup = 2,
            InputPulldown = 3
        }

        public enum LatchMode : byte
        {
            None = 0,
            Low = 1,
            High = 2
        }

        public enum BlinkMode : byte
        {
            Blink500ms = 12,
            Blink250ms = 13
        }

        public enum PwmFrequency : byte
        {
            Freq1kHz = 0,      // 1 kHz (デフォルト)
            Freq5kHz = 1,      // 5 kHz (LED 調光)
            Freq10kHz = 2,     // 10 kHz (LED 調光、標準)
            Freq25kHz = 3,     // 25 kHz (モーター制御)
            Freq50Hz = 4,      // 50 Hz (サーボモーター)
            Freq100Hz = 5,     // 100 Hz (低速制御)
            Freq500Hz = 6,     // 500 Hz (中速制御)
            Freq20kHz = 7      // 20 kHz (高周波、可聴域外)
        }

        public enum AdcAttenuation : byte
        {
            Atten0dB = 0,      // 0 dB (0-1.1V)
            Atten2_5dB = 1,    // 2.5 dB (0-1.5V)
            Atten6dB = 2,      // 6 dB (0-2.2V)
            Atten11dB = 3      // 11 dB (0-3.3V、デフォルト)
        }

        public async Task StartBlinkAsync(byte pin, BlinkMode mode)
        {
            EnsureConnected();
            await SendCommandsAsync(new[] { new GpioCommand(pin, (byte)mode, 0, 0) });
        }

        public async Task SetPwmAsync(byte pin, double dutyCycle, PwmFrequency frequency = PwmFrequency.Freq1kHz)
        {
            EnsureConnected();

            // デューティサイクルの範囲チェック
            if (dutyCycle < 0.0 || dutyCycle > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(dutyCycle),
                    "デューティサイクルは 0.0 から 1.0 の範囲で指定してください");
            }

            // 0.0-1.0 を 0-255 に変換
            byte dutyCycleByte = (byte)Math.Round(dutyCycle * 255);

            // コマンドを送信 (コマンド 20: SET_PWM)
            await SendCommandsAsync(new[] {
                new GpioCommand(pin, 20, dutyCycleByte, (byte)frequency)
            });
        }

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

        public async Task DisableAdcAsync(byte pin)
        {
            EnsureConnected();

            // コマンドを送信 (コマンド 31: SET_ADC_DISABLE)
            await SendCommandsAsync(new[] {
                new GpioCommand(pin, 31, 0, 0)
            });
        }

        public async Task<(byte Pin, uint RawValue, double Voltage)?> ReadAdcAsync(byte pin)
        {
            EnsureConnected();
            var adcValues = await ReadAllAdcAsync();
            var pinData = adcValues.FirstOrDefault(a => a.Pin == pin);

            if (pinData.Pin == 0 && pin != 0)
            {
                Console.WriteLine($"GPIO{pin} は ADC モードに設定されていません");
                return null;
            }

            return pinData;
        }

        public async Task<(byte Pin, uint RawValue, double Voltage)[]> ReadAllAdcAsync()
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

                var adcValues = new (byte Pin, uint RawValue, double Voltage)[count];
                for (int i = 0; i < count; i++)
                {
                    byte pin = response[1 + i * 3];
                    ushort rawValue = (ushort)(response[1 + i * 3 + 1] | (response[1 + i * 3 + 2] << 8));
                    // デフォルトで 11 dB (12 dB) 減衰と仮定して電圧を計算
                    double voltage = AdcToVoltage(rawValue, AdcAttenuation.Atten11dB);
                    adcValues[i] = (pin, rawValue, voltage);

                    Console.WriteLine($"    GPIO{pin}: Raw={rawValue}, Voltage={voltage:F3}V");
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

        public record GpioCommand(byte Pin, byte Command, byte Param1, byte Param2);
    }
}
