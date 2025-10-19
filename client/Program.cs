using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("ESP32 BLE GPIO クライアントを起動しています...");

        using var client = new BleGpioClient();

        // デバイスに接続
        if (!await client.ConnectAsync("ESP32-GPIO"))
        {
            Console.WriteLine("接続に失敗しました");
            return;
        }

        // GPIO2 (LED) を出力モードに設定
        await client.SetPinModeAsync(2, BleGpioClient.PinMode.Output);

        // LED を点滅
        for (int i = 0; i < 5; i++)
        {
            await client.DigitalWriteAsync(2, true);
            await Task.Delay(500);
            await client.DigitalWriteAsync(2, false);
            await Task.Delay(500);
        }

        // GPIO34 (入力専用ピン) を読み取り
        await client.SetPinModeAsync(34, BleGpioClient.PinMode.InputFloating);
        bool? state = await client.DigitalReadAsync(34);
        if (state == null)
        {
            Console.WriteLine($"GPIO34 の状態: null");
        }
        else
        {
            Console.WriteLine($"GPIO34 の状態: {((bool)state ? "HIGH" : "LOW")}");
        }

        // 自動点滅の開始
        await client.StartBlinkAsync(2, BleGpioClient.BlinkMode.Blink250ms);
    }
}
