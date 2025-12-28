/*
 * アロマキャンドル用明るさ制御プログラム
 * File: main.c
 * Author: Tetsuo Honda
 * PIC12F683
 * Created on 2015/12/20, 00:15
*/

#include <xc.h>
#include <stdlib.h>

// コンフィギュレーションの設定
// Program Files (x86)/Microchip/xc8/v1.35/docs/chips/12f683.html による

// Watchdog Timer Enable bit
//  ON WDT enabled 
// OFF WDT disabled 
#pragma config WDTE     = OFF

// Power-up Timer Enable bit 
// OFF PWRT disabled 
//  ON PWRT enabled 
#pragma config PWRTE    =  ON

// CP = Code Protection bit 
// OFF Program memory code protection is disabled 
//  ON Program memory code protection is enabled 
#pragma config CP       = OFF

// BOREN = Brown Out Detect 
//  ON BOR enabled 
// OFF BOR disabled 
// NSLEEP BOR enabled during operation and disabled in Sleep 
// SBODEN BOR controlled by SBOREN bit of the PCON register 
#pragma config BOREN    =  ON

// FCMEN = Fail-Safe Clock Monitor(クロック停止検出) Enabled bit 
//  ON Fail-Safe Clock Monitor is enabled 
// OFF Fail-Safe Clock Monitor is disabled 
#pragma config FCMEN    = OFF

// MCLRE = MCLR Pin Function(外部リセット) Select bit 
//  ON MCLR pin function is MCLR 
// OFF MCLR pin function is digital input, MCLR internally tied to VDD 
#pragma config MCLRE    =  ON

// CPD = Data Code Protection bit 
// OFF Data memory code protection is disabled 
//  ON Data memory code protection is enabled 
#pragma config CPD      = OFF

// IESO = Internal External Switchover(内部クロック／外部クロック切替) bit 
//  ON Internal External Switchover mode is enabled 
// OFF Internal External Switchover mode is disabled 
#pragma config IESO     = OFF

// FOSC = Oscillator Selection bits 
// HS HS oscillator: High-speed crystal/resonator on RA4/OSC2/CLKOUT and RA5/OSC1/CLKIN 
// INTOSCIO INTOSCIO oscillator: I/O function on RA4/OSC2/CLKOUT pin, I/O function on RA5/OSC1/CLKIN 
// INTOSCCLK INTOSC oscillator: CLKOUT function on RA4/OSC2/CLKOUT pin, I/O function on RA5/OSC1/CLKIN 
// LP LP oscillator: Low-power crystal on RA4/OSC2/CLKOUT and RA5/OSC1/CLKIN 
// EXTRCIO EXTRCIO oscillator: External RC on RA5/OSC1/CLKIN, I/O function on RA4/OSC2/CLKOUT pin 
// EC EC: I/O function on RA4/OSC2/CLKOUT pin, CLKIN on RA5/OSC1/CLKIN 
// XT XT oscillator: Crystal/resonator on RA4/OSC2/CLKOUT and RA5/OSC1/CLKINT 
// EXTRCCLK EXTRC oscillator: External RC on RA5/OSC1/CLKIN, CLKOUT function on RA4/OSC2/CLKOUT pin 
#pragma config FOSC     = INTOSCIO  // 内部発振使用

// グローバル変数の定義

// 明度(目標値)
double x = 0.5;

// 明度(現在値)
double y = 0.5;

// 明度移行カウンタ
unsigned char count = 0;

// 明度移行目標値
unsigned char maxloop = 4;

// メイン関数
void main(void)
{
    // OPTION REGISTER 設定、PIC12F683 Data Sheet による
    OSCCON  = 0b01100000; // Internal OSC 4MHz == 初期値
    ANSEL   = 0b00000000; // Def == 0b00000111 すべてデジタルに設定
    CMCON0  = 0b00000111; // コンパレータ無効化
    WPU     = 0b00110000; // プルアップビット指定
    TRISIO  = 0b00111000; // GP3, 4, 5 入力、他は出力設定
    nGPPU   = 0b00000000; // GPIO プルアップ有効化

    // 初期値セット
    GP0 = 0;
    GP1 = 0;
    GP2 = 0;

    /* タイマ 1 初期設定 20msec 周期 PIC12F683 Data Sheet による */
    TMR1H = 0xB1; // TMR1 上位セット(プリセット値)
    TMR1L = 0xDF; // TMR1 下位セット、0xFFFF-(TMR1H,TMR1L)が TMR1 の設定値となるので、この場合は 0x4E20 == 20000 カウント
    T1CON = 0b00000001; // タイマクロック = Fosc/4 * プリスケーラ 1/1 = 1MHz = 1usec、TMR1 スタート

    /* タイマ 2 初期設定 256usec 周期  PIC12F683 Data Sheet による */
    PR2 = 255; // TMR2 の設定値 = 256、Priod = タイマクロック * PR2 = 256usec
    T2CON = 0b00000100; // タイマクロック = Fosc/4 * プリスケーラ 1/1 * ポストスケーラ1/1 = 1MHz = 1usec、TMR2 スタート

    /* PWM1 初期設定 PalsWidth = 50 PIC12F683 Data Sheet による */
    CCPR1L = 208; // Capture / Compare / PWM Register 1 Low Byte
    CCP1CON = 0b00001100; // CCP1 CONTROL REGISTER, PWM mode active-high

    /* 割り込み許可 PIC12F683 Data Sheet による */
    TMR1IE = 1; // TMR1 割り込み許可
    PEIE = 1; // TMR1/2, CCP1 割り込み許可
    GIE = 1; // 割り込み許可

    // メインループ
    while(1)
    {
        // NOP
    }
}

// 割り込み処理関数
// タイマ 1 は 20msec 周期の割り込み

void __interrupt() ISR(void)
{
    if (TMR1IF != 0)
    {
        TMR1IF = 0; // 割り込みフラグクリア
        
        /* タイマ再設定 */
        TMR1H = 0xB1; // TMR1 上位セット
        TMR1L = 0xDF; // TMR1 下位セット

        if (count == 0)
        {
            // 間欠カオス法による 1/f ゆらぎの実装
            if (x < 0.5)
            {
                x = x + 2 * x * x ;
            } 
            else
            {
                x = x - 2 * (1.0 - x) * (1.0 - x);
            }

            // 間欠カオス法だと、最小値や最大値に張り付くおそれがあるため
            // 最小・最大に近づいたら乱数で離してやる
            if (x < 0.005)
            {
                x += (float)((rand() % 1000) / 10000.0);
            }
            else if (x > 0.995)
            {
                x -= (float)((rand() % 1000) / 10000.0);
            }
        }

        // 光の増減がスムーズになるよう徐々に明るさを切り替える
        y = y * (1 - 1 / (double)maxloop) + x * 1 / (double)maxloop;
        
        // 一定の明るさを最低限保つようオフセットをつける
        CCPR1L = y * 96.0 + 160.0; // Capture/Compare/PWM Register 1 Low Byte

        count++;
        if (count > maxloop)
        {
            count = 0;
            
            // 移行スピードを乱数で調整する
            // ある程度のオフセットがないと、チカチカしてしまう
            maxloop = (rand() % 9) + 3;
        }
    }
}
