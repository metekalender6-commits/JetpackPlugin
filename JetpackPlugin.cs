using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace JetpackPlugin;

/// <summary>
/// JSON dosyasina kaydedilen kalici veri: jetpack kullanma izni olan SteamID64'ler.
/// </summary>
public class JetpackData
{
    public HashSet<string> AllowedSteamIds { get; set; } = new();
}

public class JetpackPlugin : BasePlugin
{
    public override string ModuleName => "Jailbreak Jetpack";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "Custom";
    public override string ModuleDescription =>
        "!jetpack ile ac/kapat, +jetpackuse ile istedigin tusa basili tutarak uc, !jetpackver <steamid64> ile (root) yetki ver";

    // Oyuncunun jetpack'i su an acik (equip edilmis) mi
    private readonly Dictionary<ulong, bool> _jetpackActive = new();

    // Oyuncu su an kendi bind'ledigi tusu basili mi tutuyor
    private readonly Dictionary<ulong, bool> _holdingKey = new();

    // Oyuncunun anlik yakiti (0-100)
    private readonly Dictionary<ulong, float> _fuel = new();

    private JetpackData _data = new();
    private string _dataPath = "";

    // ---- Ayarlanabilir degerler ----
    private const float MaxFuel = 100f;
    // Toplam sürekli kullanımda ~6.5 saniyede biter (100 / 15.4 ~= 6.5s)
    private const float FuelUseRate = 15.4f;
    private const float FuelRegenRate = 20f;   // Yerdeyken saniyede dolan yakit
    private const float ThrustPower = 260f;    // Yukari itis hizi (units/sn)
    private const string AdminFlag = "@css/root"; // !jetpackver icin GEREKEN yetki (sadece root)

    public override void Load(bool hotReload)
    {
        _dataPath = Path.Combine(ModuleDirectory, "jetpack_data.json");
        LoadData();

        // "css_" onekli komutlar otomatik olarak hem konsol komutu hem de
        // sohbet komutu (!jetpack, /jetpack) olur.
        AddCommand("css_jetpack", "Jetpacki ac/kapat (bu hayat icin)", OnJetpackCommand);
        AddCommand("css_jetpackver", "Bir oyuncuya kalici jetpack yetkisi ver (sadece root)", OnJetpackVerCommand);

        // Oyuncunun kendi sectigi tusa bind'leyecegi basili-tut komutlari.
        // Oyuncu konsoluna veya autoexec.cfg'sine sunu yazar:
        //   bind "MOUSE4" "+jetpackuse"
        // Istedigi HERHANGI bir tusa atayabilir, sunucu tarafinda bir sey degismez.
        AddCommand("+jetpackuse", "Jetpack itisini basli tut", OnJetpackKeyDown);
        AddCommand("-jetpackuse", "Jetpack itisini birak", OnJetpackKeyUp);

        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);

        Console.WriteLine("[Jetpack] Eklenti yuklendi.");
    }

    public override void Unload(bool hotReload)
    {
        SaveData();
    }

    // ---------------- Veri kaydetme/yukleme ----------------

    private void LoadData()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = File.ReadAllText(_dataPath);
                _data = JsonSerializer.Deserialize<JetpackData>(json) ?? new JetpackData();
            }
            else
            {
                SaveData();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Jetpack] Veri okunamadi: {e.Message}");
        }
    }

    private void SaveData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Jetpack] Veri kaydedilemedi: {e.Message}");
        }
    }

    private bool HasAccess(CCSPlayerController player)
    {
        var steamId = player.AuthorizedSteamID;
        return steamId != null && _data.AllowedSteamIds.Contains(steamId.SteamId64.ToString());
    }

    // ---------------- Komutlar ----------------

    private void OnJetpackCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
        {
            info.ReplyToCommand("Bu komut sadece oyun icinden kullanilabilir.");
            return;
        }

        if (!HasAccess(player))
        {
            player.PrintToChat(" \x04[Jetpack]\x01 Bu ozellige sahip degilsin. Magazadan satin alabilirsin.");
            return;
        }

        var steamId = player.AuthorizedSteamID!.SteamId64;
        bool current = _jetpackActive.TryGetValue(steamId, out var v) && v;
        _jetpackActive[steamId] = !current;

        if (!current)
        {
            _fuel[steamId] = MaxFuel;
            _holdingKey[steamId] = false;
            player.PrintToChat(" \x04[Jetpack]\x01 ACIK. Istedigin tusa bind'ledin mi?");
            player.PrintToChat(" \x04[Jetpack]\x01 Ornek: konsola yaz -> bind \"MOUSE4\" \"+jetpackuse\"");
        }
        else
        {
            player.PrintToChat(" \x04[Jetpack]\x01 \x02KAPATILDI\x01.");
        }
    }

    private void OnJetpackVerCommand(CCSPlayerController? player, CommandInfo info)
    {
        // player == null ise komut sunucu konsolundan/rcon'dan calistiriliyordur, izin verilir.
        if (player != null && !AdminManager.PlayerHasPermissions(player, AdminFlag))
        {
            player.PrintToChat(" \x02[Jetpack]\x01 Bu komutu kullanma yetkin yok. (root gerekli)");
            return;
        }

        if (info.ArgCount < 2)
        {
            info.ReplyToCommand("Kullanim: !jetpackver <steamid64>  (ornek: 76561198000000000)");
            return;
        }

        var targetSteamId = info.GetArg(1).Trim();

        if (!ulong.TryParse(targetSteamId, out _))
        {
            info.ReplyToCommand("Gecersiz SteamID64. 17 haneli sayisal SteamID64 girmelisin.");
            return;
        }

        bool added = _data.AllowedSteamIds.Add(targetSteamId);
        SaveData();

        if (added)
            info.ReplyToCommand($"[Jetpack] SteamID {targetSteamId} icin jetpack yetkisi verildi ve kaydedildi.");
        else
            info.ReplyToCommand($"[Jetpack] SteamID {targetSteamId} zaten yetkiliydi.");

        var target = Utilities.GetPlayers().FirstOrDefault(p =>
            p.IsValid && p.AuthorizedSteamID != null &&
            p.AuthorizedSteamID.SteamId64.ToString() == targetSteamId);

        target?.PrintToChat(" \x04[Jetpack]\x01 Artik jetpack kullanabilirsin! Acmak icin yaz: \x04!jetpack");
    }

    // Oyuncu kendi bind'ledigi tusa BASTIGINDA calisir
    private void OnJetpackKeyDown(CCSPlayerController? player, CommandInfo info)
    {
        var steamId = player?.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;
        _holdingKey[steamId.Value] = true;
    }

    // Oyuncu tusu BIRAKTIGINDA calisir
    private void OnJetpackKeyUp(CCSPlayerController? player, CommandInfo info)
    {
        var steamId = player?.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;
        _holdingKey[steamId.Value] = false;
    }

    // ---------------- Oyun olaylari ----------------

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var steamId = @event.Userid?.AuthorizedSteamID?.SteamId64;
        if (steamId != null)
        {
            _fuel[steamId.Value] = MaxFuel;
            _holdingKey[steamId.Value] = false;
        }
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var steamId = @event.Userid?.AuthorizedSteamID?.SteamId64;
        if (steamId != null)
        {
            _jetpackActive[steamId.Value] = false;
            _holdingKey[steamId.Value] = false;
        }
        return HookResult.Continue;
    }

    // ---------------- Ana dongu: itis + yakit + HUD ----------------

    private void OnTick()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || !player.PawnIsAlive) continue;

            var steamId = player.AuthorizedSteamID?.SteamId64;
            if (steamId == null) continue;

            if (!_jetpackActive.TryGetValue(steamId.Value, out var active) || !active) continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid) continue;

            if (!_fuel.ContainsKey(steamId.Value)) _fuel[steamId.Value] = MaxFuel;
            if (!_holdingKey.ContainsKey(steamId.Value)) _holdingKey[steamId.Value] = false;

            bool onGround = (pawn.Flags & (1 << 0)) != 0; // FL_ONGROUND
            bool wantsThrust = _holdingKey[steamId.Value] && !onGround && _fuel[steamId.Value] > 0f;

            if (wantsThrust)
            {
                var velocity = pawn.AbsVelocity;
                velocity.Z = ThrustPower;
                pawn.Teleport(null, null, velocity);

                _fuel[steamId.Value] = Math.Max(0f, _fuel[steamId.Value] - FuelUseRate / 64f);

                int fuelPercent = (int)(_fuel[steamId.Value] / MaxFuel * 100f);
                player.PrintToCenterHtml($"<font color='orange'>Jetpack Yakit: {fuelPercent}%</font>");
            }
            else if (onGround && _fuel[steamId.Value] < MaxFuel)
            {
                _fuel[steamId.Value] = Math.Min(MaxFuel, _fuel[steamId.Value] + FuelRegenRate / 64f);
            }
        }
    }
}
