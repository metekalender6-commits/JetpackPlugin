using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace JetpackPlugin;

public class JetpackData
{
    public HashSet<string> AllowedSteamIds { get; set; } = new();
}

public class JetpackPlugin : BasePlugin
{
    public override string ModuleName => "Jailbreak Jetpack";
    public override string ModuleVersion => "1.1.6";
    public override string ModuleAuthor => "Custom";
    public override string ModuleDescription =>
        "!jetpack ile ac/kapat, basili tutarak uc, !jetpackver <steamid64> ile yetki ver";

    private readonly Dictionary<ulong, bool> _jetpackActive = new();
    private readonly Dictionary<ulong, bool> _holdingKey = new();
    private readonly Dictionary<ulong, float> _fuel = new();
    private JetpackData _data = new();
    private string _dataPath = "";

    // ---- Ayarlar ----
    private const float MaxFuel = 100f;
    // 1.75 saniye sürekli kullanım (100 / 1.75 ≈ 57.143)
    private const float FuelUseRate = 57.143f;
    // 30 saniyede tamamen dolsun (100 / 30 ≈ 3.333)
    private const float FuelRegenRate = 3.333f;
    private const float ThrustPower = 260f;
    private const string AdminFlag = "@css/root";

    public override void Load(bool hotReload)
    {
        _dataPath = Path.Combine(ModuleDirectory, "jetpack_data.json");
        LoadData();

        AddCommand("css_jetpack", "Jetpacki ac/kapat", OnJetpackCommand);
        AddCommand("css_jetpackver", "Jetpack yetkisi ver (root)", OnJetpackVerCommand);
        AddCommand("css_jetpackuse_on", "Jetpack basili tut", OnJetpackKeyDown);
        AddCommand("css_jetpackuse_off", "Jetpack birak", OnJetpackKeyUp);

        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);

        Console.WriteLine("[Jetpack] Eklenti yuklendi.");
    }

    public override void Unload(bool hotReload)
    {
        SaveData();
    }

    private void LoadData()
    {
        try
        {
            if (File.Exists(_dataPath))
            {
                var json = File.ReadAllText(_dataPath);
                _data = JsonSerializer.Deserialize<JetpackData>(json) ?? new JetpackData();
            }
            else SaveData();
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

    private void OnJetpackCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;

        if (!HasAccess(player))
        {
            player.PrintToChat(" \x04[Jetpack]\x01 Bu ozellige sahip degilsin.");
            return;
        }

        var steamId = player.AuthorizedSteamID!.SteamId64;
        bool current = _jetpackActive.TryGetValue(steamId, out var v) && v;
        _jetpackActive[steamId] = !current;

        if (!current)
        {
            _fuel[steamId] = MaxFuel;
            _holdingKey[steamId] = false;

            // Sadece bind talimatı (açıldı/kapandı yazmıyor)
            player.PrintToChat(" \x04[Jetpack]\x01 Konsola yaz:");
            player.PrintToChat(" \x04alias +jetpackuse \"css_jetpackuse_on\"");
            player.PrintToChat(" \x04alias -jetpackuse \"css_jetpackuse_off\"");
            player.PrintToChat(" \x04bind c \"+jetpackuse\"");
        }
        // Kapandığında chat mesajı yok
    }

    private void OnJetpackVerCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player != null && !AdminManager.PlayerHasPermissions(player, AdminFlag))
        {
            player.PrintToChat(" \x02[Jetpack]\x01 Yetkin yok (root gerekli)");
            return;
        }

        if (info.ArgCount < 2)
        {
            info.ReplyToCommand("Kullanim: !jetpackver <steamid64>");
            return;
        }

        var targetSteamId = info.GetArg(1).Trim();
        if (!ulong.TryParse(targetSteamId, out _))
        {
            info.ReplyToCommand("Gecersiz SteamID64");
            return;
        }

        bool added = _data.AllowedSteamIds.Add(targetSteamId);
        SaveData();

        info.ReplyToCommand(added
            ? $"[Jetpack] {targetSteamId} yetkisi verildi"
            : $"[Jetpack] {targetSteamId} zaten yetkiliydi");

        var target = Utilities.GetPlayers().FirstOrDefault(p =>
            p.IsValid && p.AuthorizedSteamID != null &&
            p.AuthorizedSteamID.SteamId64.ToString() == targetSteamId);

        target?.PrintToChat(" \x04[Jetpack]\x01 Artik kullanabilirsin! Yaz: !jetpack");
    }

    private void OnJetpackKeyDown(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        _holdingKey[steamId.Value] = true;
    }

    private void OnJetpackKeyUp(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;
        var steamId = player.AuthorizedSteamID?.SteamId64;
        if (steamId == null) return;

        _holdingKey[steamId.Value] = false;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var steamId = @event.Userid?.AuthorizedSteamID?.SteamId64;
        if (steamId != null)
        {
            // Yakıtı doldur, tuş durumunu sıfırla (aktif durumu KORU)
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
            // Sadece tuş durumunu sıfırla, jetpack aktif kalsın
            _holdingKey[steamId.Value] = false;
        }
        return HookResult.Continue;
    }

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

            bool onGround = (pawn.Flags & (1 << 0)) != 0;
            bool isHolding = _holdingKey[steamId.Value];
            bool wantsThrust = isHolding && !onGround && _fuel[steamId.Value] > 0f;

            if (wantsThrust)
            {
                // Uçur
                var velocity = pawn.AbsVelocity;
                velocity.Z = ThrustPower;
                pawn.Teleport(null, null, velocity);

                // Yakıt tüket (1.75 saniyede biter)
                _fuel[steamId.Value] = Math.Max(0f, _fuel[steamId.Value] - FuelUseRate / 64f);

                int fuelPercent = (int)(_fuel[steamId.Value] / MaxFuel * 100f);
                player.PrintToCenterHtml($"<font color='orange'>Jetpack Yakit: {fuelPercent}%</font>");
            }
            else if (!isHolding && _fuel[steamId.Value] < MaxFuel)
            {
                // Tuşa basılı değilken 30 saniyede dolsun
                _fuel[steamId.Value] = Math.Min(MaxFuel, _fuel[steamId.Value] + FuelRegenRate / 64f);
            }
        }
    }
}
