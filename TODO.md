# TODO & NOTE

## TODO

### 排他制御が正しくない箇所があるはず

```test
I (86645) BLEIO-ESP32: Advertising with 128-bit Service UUID
I (116795) BLEIO-ESP32: BLE connection established; status=0
I (116795) NimBLE: GAP procedure initiated:
I (116795) NimBLE: connection parameter update; conn_handle=0 itvl_min=40 itvl_max=40 latency=0 supervision_timeout=500 min_ce_len=0 max_ce_len=0
I (116805) NimBLE:

I (116815) BLEIO-ESP32: Updated connection parameters for stable power consumption
I (118865) BLEIO-ESP32: Received 1 commands
I (118865) BLEIO-ESP32: Command 1: pin=18, command=17, param1=2, param2=128, param3=0, param4=0
I (118865) BLEIO-ESP32: Turned off all WS2812B LEDs on GPIO18

assert failed: spinlock_acquire spinlock.h:142 (lock->count == 0)


Backtrace: 0x40082fcd:0x3ffba990 0x4008f9b5:0x3ffba9b0 0x40095d61:0x3ffba9d0 0x40090696:0x3ffbaaf0 0x4008fdf7:0x3ffbab20 0x40126c45:0x3ffbab60 0x400d4467:0x3ffbaba0 0x400d4545:0x3ffbabf0 0x400ee155:0x3ffbac30 0x400ee19d:0x3ffbac60 0x40090465:0x3ffbac80




ELF file SHA256: 967c3d454

Rebooting...
```

## NOTE

- Notify は、サポートしないほうがシンプル。
