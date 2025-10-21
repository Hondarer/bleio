﻿using System;
using System.Threading.Tasks;
using Hondarersoft.Bleio;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("BLEIO クライアントを起動しています...");

        try
        {
            using var client = new BleioClient();

            // デバイスに接続
            if (!await client.ConnectAsync())
            {
                Console.WriteLine("接続に失敗しました");
                return;
            }

            await client.SetOutputAsync(16, BleioClient.OutputKind.Low); // 白 (ミニ)
            await client.SetOutputAsync(17, BleioClient.OutputKind.Low); // 緑 (ミニ)
            await client.SetOutputAsync(18, BleioClient.OutputKind.Low); // 青 (ミニ)
            await client.SetOutputAsync(19, BleioClient.OutputKind.Low); // 黄 (ミニ)
            await client.SetOutputAsync(21, BleioClient.OutputKind.Low); // 赤 (ミニ)
            await Task.Delay(1000);
            await client.SetOutputAsync(16, BleioClient.OutputKind.High); // 白 (ミニ)
            await Task.Delay(1000);
            await client.SetOutputAsync(17, BleioClient.OutputKind.High); // 緑 (ミニ)
            await Task.Delay(1000);
            await client.SetOutputAsync(18, BleioClient.OutputKind.High); // 青 (ミニ)
            await Task.Delay(1000);
            await client.SetOutputAsync(19, BleioClient.OutputKind.High); // 黄 (ミニ)
            await Task.Delay(1000);
            await client.SetOutputAsync(21, BleioClient.OutputKind.High); // 赤 (ミニ)
            await Task.Delay(1000);

            // 13 (緑)
            // 12 (黄)
            await client.SetOutputAsync(14, BleioClient.OutputKind.Blink500ms); // 14 (赤)
            // 27 (ブザー)

            // 26 (SW in)

            // LED を点滅
            for (int i = 0; i < 5; i++)
            {
                await client.SetOutputAsync(2, BleioClient.OutputKind.High);
                await Task.Delay(500);
                await client.SetOutputAsync(2, BleioClient.OutputKind.Low);
                await Task.Delay(500);
            }

            // 自動点滅
            await client.SetOutputAsync(2, BleioClient.OutputKind.Blink250ms);
            await Task.Delay(3000);

            // GPIO34 (入力専用ピン) を読み取り
            await client.SetInputAsync(34, BleioClient.InputConfig.Floating);
            bool? state = await client.ReadInputAsync(34);
            if (state == null)
            {
                Console.WriteLine($"GPIO34 の状態: null");
            }
            else
            {
                Console.WriteLine($"GPIO34 の状態: {((bool)state ? "HIGH" : "LOW")}");
            }

            // PWM で LED の明るさを制御
            Console.WriteLine("PWM で LED の明るさを制御します...");

            // 25% の明るさ
            await client.SetPwmAsync(2, 0.25, BleioClient.PwmFrequency.Freq10kHz);
            Console.WriteLine("明るさ 25%");
            await Task.Delay(2000);

            // 50% の明るさ
            await client.SetPwmAsync(2, 0.5, BleioClient.PwmFrequency.Freq10kHz);
            Console.WriteLine("明るさ 50%");
            await Task.Delay(2000);

            // 75% の明るさ
            await client.SetPwmAsync(2, 0.75, BleioClient.PwmFrequency.Freq10kHz);
            Console.WriteLine("明るさ 75%");
            await Task.Delay(2000);

            // 100% の明るさ
            await client.SetPwmAsync(2, 1.0, BleioClient.PwmFrequency.Freq10kHz);
            Console.WriteLine("明るさ 100%");
            await Task.Delay(2000);

            // PWM を停止 (LED を消灯)
            await client.SetOutputAsync(2, BleioClient.OutputKind.Low);
            Console.WriteLine("PWM を停止しました");

            // ADC でアナログ電圧を読み取り
            Console.WriteLine("ADC でアナログ電圧を読み取ります...");

            // GPIO32 を ADC 入力として有効化 (11dB 減衰、0-3.3V 測定可能)
            await client.EnableAdcAsync(32, BleioClient.AdcAttenuation.Atten11dB);
            Console.WriteLine("GPIO32 を ADC 入力として有効化しました");
            await Task.Delay(500);

            // GPIO32 の ADC 値を読み取り
            var adcResult = await client.ReadAdcAsync(32);
            if (adcResult != null)
            {
                Console.WriteLine($"GPIO{adcResult.Pin}: Raw={adcResult.RawValue}, Voltage={adcResult.Voltage:F3}V");
            }
            else
            {
                Console.WriteLine("GPIO32 は ADC モードに設定されていません");
            }

            // ADC を無効化
            await client.DisableAdcAsync(32);
            Console.WriteLine("GPIO32 の ADC を無効化しました");

            // BLE 切断時の振る舞いを設定
            Console.WriteLine("BLE 切断時の振る舞いを設定します...");

            // GPIO2 を切断時に LOW にする設定
            await client.SetDisconnectBehaviorAsync(2, BleioClient.DisconnectBehavior.SetLow);
            Console.WriteLine("GPIO2 を切断時に LOW に設定する振る舞いを設定しました");

            // LED を点灯
            await client.SetOutputAsync(2, BleioClient.OutputKind.High);
            Console.WriteLine("GPIO2 を HIGH に設定しました (LED 点灯)");
            await Task.Delay(2000);

            Console.WriteLine("BLE 接続を切断すると、GPIO2 は自動的に LOW になります");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"エラー: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"予期しないエラーが発生しました: {ex.Message}");
            Console.WriteLine($"スタックトレース:\n{ex.StackTrace}");
            return;
        }
    }
}
