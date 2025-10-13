using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

public class BleGpioClient : IDisposable
{
    private const string ServiceUuid = "4fafc201-1fb5-459e-8fcc-c5c9c333914b";
    private const string CharGpioWriteUuid = "beb5483e-36e1-4688-b7f5-ea07361b26a8";
    private const string CharGpioReadUuid = "1c95d5e3-d8f7-413a-bf3d-7a2e5d7be87e";

    private BluetoothLEDevice? _device;
    private GattDeviceService? _service;
    private GattCharacteristic? _writeCharacteristic;
    private GattCharacteristic? _readCharacteristic;

    public async Task<bool> ConnectAsync(string deviceName = "ESP32-GPIO")
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

            Console.WriteLine("GATT サービスの初期化が完了しました");
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

    private void CleanupResources()
    {
        _writeCharacteristic = null;
        _readCharacteristic = null;

        _service?.Dispose();
        _service = null;

        _device?.Dispose();
        _device = null;

        Console.WriteLine("リソースをクリーンアップしました");
    }

    public async Task SetPinModeAsync(byte pin, PinMode mode)
    {
        if (_writeCharacteristic == null)
        {
            throw new InvalidOperationException("デバイスに接続されていません");
        }

        byte[] data = { pin, (byte)mode };
        var writer = new DataWriter();
        writer.WriteBytes(data);

        var result = await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer());

        if (result == GattCommunicationStatus.Success)
        {
            Console.WriteLine($"GPIO{pin} のモードを {mode} に設定しました");
        }
        else
        {
            Console.WriteLine($"GPIO{pin} の設定に失敗しました");
        }
    }

    public async Task DigitalWriteAsync(byte pin, bool value)
    {
        if (_writeCharacteristic == null)
        {
            throw new InvalidOperationException("デバイスに接続されていません");
        }

        byte command = value ? (byte)11 : (byte)10;
        byte[] data = { pin, command };
        var writer = new DataWriter();
        writer.WriteBytes(data);

        var result = await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer());

        if (result == GattCommunicationStatus.Success)
        {
            Console.WriteLine($"GPIO{pin} を {(value ? "HIGH" : "LOW")} に設定しました");
        }
        else
        {
            Console.WriteLine($"GPIO{pin} の書き込みに失敗しました");
        }
    }

    public async Task<bool> DigitalReadAsync(byte pin)
    {
        if (_readCharacteristic == null)
        {
            throw new InvalidOperationException("デバイスに接続されていません");
        }

        try
        {
            Console.WriteLine($"GPIO{pin} の読み取りを開始します...");

            // 読み取りたいピン番号を書き込む
            byte[] data = { pin };
            var writer = new DataWriter();
            writer.WriteBytes(data);

            Console.WriteLine($"  ピン番号 {pin} を書き込んでいます...");
            var writeResult = await _readCharacteristic.WriteValueAsync(writer.DetachBuffer());
            Console.WriteLine($"  書き込み結果: {writeResult}");

            if (writeResult != GattCommunicationStatus.Success)
            {
                Console.WriteLine($"GPIO{pin} への書き込みに失敗しました: {writeResult}");
                return false;
            }

            // データが準備されるまで待機しながら読み取り (最大 3 秒)
            Console.WriteLine($"  データを読み取っています...");
            const int maxRetries = 30;
            const int delayMs = 100;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (retry > 0)
                {
                    await Task.Delay(delayMs);
                }

                var readResult = await _readCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);

                if (readResult.Status == GattCommunicationStatus.Success)
                {
                    var reader = DataReader.FromBuffer(readResult.Value);
                    byte[] response = new byte[reader.UnconsumedBufferLength];
                    //byte[] response = new byte[2];
                    reader.ReadBytes(response);

                    if (retry == 0 || response.Length > 0)
                    {
                        Console.WriteLine($"  読み取り試行 {retry + 1}: 受信データ長 {response.Length} バイト");
                    }

                    if (response.Length > 0)
                    {
                        Console.Write($"  受信データ: ");
                        foreach (var b in response)
                        {
                            Console.Write($"0x{b:X2} ");
                        }
                        Console.WriteLine();
                    }

                    if (response.Length >= 2)
                    {
                        bool state = response[1] != 0;
                        Console.WriteLine($"GPIO{pin} の状態: {(state ? "HIGH" : "LOW")} (ピン={response[0]}, 値={response[1]})");
                        return state;
                    }
                    else if (response.Length > 0)
                    {
                        Console.WriteLine($"  データ不足 (期待: 2バイト, 実際: {response.Length}バイト), 再試行します...");
                    }
                }
                else
                {
                    Console.WriteLine($"  読み取り試行 {retry + 1}: 通信エラー: {readResult.Status}");
                }
            }

            Console.WriteLine($"タイムアウト: {maxRetries * delayMs / 1000.0} 秒待機しましたがデータが揃いませんでした");

            Console.WriteLine($"GPIO{pin} の読み取りに失敗しました");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GPIO{pin} の読み取り中に例外が発生しました: {ex.Message}");
            Console.WriteLine($"スタックトレース:\n{ex.StackTrace}");
            return false;
        }
    }

    public void Dispose()
    {
        CleanupResources();
    }

    public enum PinMode : byte
    {
        Input = 0,
        Output = 1,
        InputPullup = 2
    }
}
