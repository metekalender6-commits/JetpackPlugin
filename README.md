# Jailbreak Jetpack – CounterStrikeSharp Eklentisi (v1.1)

## Önemli not
Bu paket **derlenmemiş kaynak kod**dur. Hazır bir `.dll` gönderemedim çünkü bende
gerçek bir CS2 sunucusu / derleme ortamı ve internet erişimi yok. Aşağıdaki
adımlarla kendi `.dll` dosyanı birkaç dakikada çıkarabilirsin.

## Neler değişti (v1.1)
- **Tuş artık sabit değil.** Oyuncu jetpack'i istediği herhangi bir tuşa kendi
  bind'ler. Sunucu tarafında hiçbir ayar yok.
- **HUD** ekranın ortasında jetpack kullanılırken görünür ve %100'den aşağı akar.
- **Süre kısaltıldı**: sürekli basılı tutunca yakıt ~6.5 saniyede biter.
- **!jetpackver artık sadece `@css/root` yetkisine sahip adminlerde çalışır.**

## Nasıl çalışır?
1. Root admin, oyuncuya yetki verir:
   ```
   !jetpackver 76561198000000000
   ```
   Bu SteamID64 kalıcı olarak `jetpack_data.json` dosyasına kaydedilir —
   sunucu restart olsa bile silinmez.
2. Yetkili oyuncu round içinde `!jetpack` yazarak jetpack'i o hayat için **açar**.
3. Oyuncu kendi tuşunu kendi konsoluna/`autoexec.cfg` dosyasına şöyle bind'ler
   (bir kere yapması yeterli, sonra her oyunda kalıcı):
   ```
   bind "MOUSE4" "+jetpackuse"
   ```
   İstediği herhangi bir tuşu kullanabilir (`Q`, `MOUSE5`, `CAPSLOCK` vs.) —
   `+jetpackuse` neyse ona bağladığı tuş, jetpack tuşu olur.
4. Havadayken o tuşa **basılı tuttukça** yukarı iter, ortada yakıt yüzdesi
   görünür, yakıt biterse itiş durur. Yerdeyken yakıt tekrar dolar.
5. Ölünce jetpack o hayat için otomatik kapanır, tekrar spawn olunca `!jetpack`
   ile yeniden açması gerekir (istersen bunu da otomatik açık başlasın diye
   değiştirebiliriz, söylemen yeterli).

## Gereksinimler
1. Sunucuda **Metamod:Source** + **CounterStrikeSharp** kurulu olmalı:
   https://docs.cssharp.dev/docs/guides/getting-started.html
2. Derleme için bilgisayarında **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0

## Derleme
```
cd JetpackPlugin
dotnet restore
dotnet build -c Release
```
Çıktı: `bin/Release/net8.0/JetpackPlugin.dll` (klasördeki tüm dosyaları kopyala).

## Kurulum
1. `csgo/addons/counterstrikesharp/plugins/JetpackPlugin/` klasörünü oluştur.
2. `bin/Release/net8.0/` içindeki tüm dosyaları oraya kopyala.
3. Sunucuyu restart et ya da konsolda: `css_plugins load JetpackPlugin`

## Admin yetkisi (root) ayarı
`!jetpackver` komutunu sadece `@css/root` yetkisine sahip adminler kullanabilir.
Bu yetki `addons/counterstrikesharp/configs/admins.json` dosyasında tanımlanır,
örnek:
```json
{
  "RootAdminExample": {
    "identity": "76561198000000000",
    "flags": ["@css/root"]
  }
}
```
Yetki seviyesini değiştirmek istersen `JetpackPlugin.cs` içindeki şu satırı
değiştirip yeniden derle:
```csharp
private const string AdminFlag = "@css/root";
```

## Süre / güç ayarları
`JetpackPlugin.cs` üstündeki değerleri değiştirip yeniden derleyerek
dengeyi ayarlayabilirsin:
```csharp
private const float MaxFuel = 100f;
private const float FuelUseRate = 15.4f;   // ~6.5 saniyede biter, arttırırsan süre kısalır
private const float FuelRegenRate = 20f;   // yerdeyken dolma hızı
private const float ThrustPower = 260f;    // yukarı itiş gücü
```

## Veri dosyası
```
addons/counterstrikesharp/plugins/JetpackPlugin/jetpack_data.json
```
```json
{
  "AllowedSteamIds": ["76561198000000000", "76561198111111111"]
}
```
Elle de düzenlenebilir.
