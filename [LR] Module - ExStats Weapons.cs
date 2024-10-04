using System.Text.Json;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using LevelsRanksApi;

namespace LevelsRanksExStatsWeapons
{
    [MinimumApiVersion(80)]
    public class LevelsRanksExStatsWeapons : BasePlugin
    {
        public override string ModuleName => "[LR] ExStats Weapons";
        public override string ModuleVersion => "1.0";
        public override string ModuleAuthor => "ABKAM";
        public override string ModuleDescription => "Plugin for tracking weapon kills with fixed experience and custom messages.";

        private readonly PluginCapability<ILevelsRanksApi> _levelsRanksApiCapability = new("levels_ranks");
        private ILevelsRanksApi? _levelsRanksApi;

        private string _tableName = string.Empty;
        private bool _experienceEnabled = true; 
        private Dictionary<string, WeaponData> _weaponsData = new();

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            base.OnAllPluginsLoaded(hotReload);
            _levelsRanksApi = _levelsRanksApiCapability.Get();

            if (_levelsRanksApi == null)
            {
                Console.WriteLine("[LR-WEAPONS] LevelsRanksApi is not initialized. Exiting Load method.");
                return;
            }

            Console.WriteLine("[LR-WEAPONS] LevelsRanksApi initialized successfully.");
            CreateDbTableIfNotExists();
            LoadSettings();
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        }

        private HookResult OnPlayerDeath(EventPlayerDeath deathEvent, GameEventInfo info)
        {
            try
            {
                if (!_experienceEnabled)
                {
                    Console.WriteLine("[LR-WEAPONS] Experience is disabled.");
                    return HookResult.Continue;
                }

                var attacker = deathEvent.Attacker;
                var victim = deathEvent.Userid;

                if (attacker == null || victim == null)
                {
                    return HookResult.Continue;
                }
                
                var attackerSteamId64 = ulong.Parse(attacker.SteamID.ToString());
                var victimSteamId64 = ulong.Parse(victim.SteamID.ToString());

                var attackerSteamId = _levelsRanksApi.ConvertToSteamId(attackerSteamId64);
                var victimSteamId = _levelsRanksApi.ConvertToSteamId(victimSteamId64);
                
                if (attacker.IsBot)
                {
                    Console.WriteLine($"[LR-WEAPONS] Attacker is a bot, and experience from bots is disabled. No experience awarded.");
                    return HookResult.Continue;
                }
                
                if (victim.IsBot && !_levelsRanksApi.GetExperienceFromBots())
                {
                    Console.WriteLine($"[LR-WEAPONS] Victim is a bot, and experience from bots is disabled. No experience awarded.");
                    return HookResult.Continue; 
                }               
                Console.WriteLine($"[LRTEST] BOT {_levelsRanksApi.GetExperienceFromBots()}");
                var weapon = deathEvent.Weapon.Trim().ToLower();
                Console.WriteLine($"[LR-WEAPONS] Weapon raw value: {weapon}");

                if (_weaponsData.TryGetValue($"weapon_{weapon}", out var weaponData))
                {
                    RewardPlayer(attackerSteamId64, weaponData.Name, weaponData.Exp, weaponData.Color);
                }
                else
                {
                    Console.WriteLine($"[LR-WEAPONS] No data found for weapon: weapon_{weapon}");
                }

                var weaponClass = $"weapon_{weapon}";
                
                Task.Run(() => UpdateWeaponStatsAsync(attackerSteamId, weaponClass));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LR-WEAPONS] Error in OnPlayerDeath: {ex.Message}");
            }

            return HookResult.Continue;
        }


        private void RewardPlayer(ulong steamId64, string name, int xp, string color)
        {
            var steamId = _levelsRanksApi!.ConvertToSteamId(steamId64);
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID == steamId64);

            if (player != null && _levelsRanksApi.OnlineUsers.TryGetValue(steamId, out var user))
            {
                if (player.Team == CsTeam.Spectator)
                {
                    Console.WriteLine($"Player {steamId} is a spectator. No experience awarded.");
                    return;
                }

                _levelsRanksApi.ApplyExperienceUpdateSync(
                    user, player, xp, 
                    ReplaceColorPlaceholders(name), 
                    ReplaceColorPlaceholders(color));
            }
        }

        public string ReplaceColorPlaceholders(string message)
        {
            if (message.Contains('{'))
            {
                var modifiedValue = message;
                foreach (var field in typeof(ChatColors).GetFields())
                {
                    var pattern = $"{{{field.Name}}}";
                    if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        modifiedValue = modifiedValue.Replace(pattern, field.GetValue(null).ToString(), StringComparison.OrdinalIgnoreCase);
                }

                return modifiedValue;
            }

            return message;
        }

        private async Task UpdateWeaponStatsAsync(string steamId, string weaponClass)
        {
            try
            {
                var connectionString = _levelsRanksApi.DbConnectionString;
                var tableName = $"{_levelsRanksApi.TableName}_weapons";

                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = $@"
                        INSERT INTO `{tableName}` 
                        (`steam`, `classname`, `kills`) 
                        VALUES (@SteamID, @WeaponClass, 1)
                        ON DUPLICATE KEY UPDATE 
                            `kills` = `kills` + 1;";

                    var parameters = new
                    {
                        SteamID = steamId,
                        WeaponClass = weaponClass
                    };

                    Console.WriteLine($"[LR-WEAPONS] Executing query: {query}");
                    await connection.ExecuteAsync(query, parameters);
                    Console.WriteLine($"[LR-WEAPONS] Query executed successfully for SteamID: {steamId}, WeaponClass: {weaponClass}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LR-WEAPONS] Error in UpdateWeaponStatsAsync: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            _tableName = _levelsRanksApi?.TableName ?? "lr_weapons";
            Console.WriteLine($"[LR-WEAPONS] Loaded settings. TableName: {_tableName}");

            var configPath = Path.Combine(Application.RootDirectory, "configs/plugins/LevelsRanks/exstats_weapons.json");

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<WeaponConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                    });

                    _experienceEnabled = config.ExperienceEnabled;
                    _weaponsData = config.Weapons;
                    Console.WriteLine("[LR-WEAPONS] Config loaded successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LR-WEAPONS] Error loading config: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("[LR-WEAPONS] Config file not found, creating default config.");
                CreateDefaultConfig(configPath);
            }
        }

        private void CreateDefaultConfig(string configPath)
        {
            var config = new WeaponConfig
            {
                ExperienceEnabled = true, 
                Weapons = new Dictionary<string, WeaponData>
                {
                    { "weapon_knife", new WeaponData { Name = "за убийство с Knife", Color = "{Green}", Exp = 10 } },
                    { "weapon_taser", new WeaponData { Name = "за убийство с Zeus x27", Color = "{Yellow}", Exp = 8 } },
                    { "weapon_inferno", new WeaponData { Name = "за убийство с Molotov", Color = "{Red}", Exp = 7 } },
                    { "weapon_hegrenade", new WeaponData { Name = "за убийство с Hegrenade", Color = "{Orange}", Exp = 5 } },
                    { "weapon_glock", new WeaponData { Name = "за убийство с Glock", Color = "{Green}", Exp = 6 } },
                    { "weapon_hkp2000", new WeaponData { Name = "за убийство с P2000", Color = "{Blue}", Exp = 6 } },
                    { "weapon_tec9", new WeaponData { Name = "за убийство с Tec-9", Color = "{Purple}", Exp = 7 } },
                    { "weapon_usp_silencer", new WeaponData { Name = "за убийство с USP-S", Color = "{Blue}", Exp = 6 } },
                    { "weapon_p250", new WeaponData { Name = "за убийство с P250", Color = "{Green}", Exp = 6 } },
                    { "weapon_cz75a", new WeaponData { Name = "за убийство с CZ75-Auto", Color = "{Yellow}", Exp = 5 } },
                    { "weapon_fiveseven", new WeaponData { Name = "за убийство с Five Seven", Color = "{Blue}", Exp = 6 } },
                    { "weapon_elite", new WeaponData { Name = "за убийство с Dual Berettas", Color = "{Green}", Exp = 6 } },
                    { "weapon_revolver", new WeaponData { Name = "за убийство с R8 Revolver", Color = "{Orange}", Exp = 7 } },
                    { "weapon_deagle", new WeaponData { Name = "за убийство с Desert Eagle", Color = "{Red}", Exp = 10 } },
                    { "weapon_negev", new WeaponData { Name = "за убийство с Negev", Color = "{Yellow}", Exp = 5 } },
                    { "weapon_m249", new WeaponData { Name = "за убийство с M249", Color = "{Blue}", Exp = 5 } },
                    { "weapon_mag7", new WeaponData { Name = "за убийство с MAG-7", Color = "{Green}", Exp = 7 } },
                    { "weapon_sawedoff", new WeaponData { Name = "за убийство с Sawedoff", Color = "{Orange}", Exp = 7 } },
                    { "weapon_nova", new WeaponData { Name = "за убийство с Nova", Color = "{Yellow}", Exp = 6 } },
                    { "weapon_xm1014", new WeaponData { Name = "за убийство с XM1014", Color = "{Green}", Exp = 6 } },
                    { "weapon_bizon", new WeaponData { Name = "за убийство с PP-Bizon", Color = "{Green}", Exp = 6 } },
                    { "weapon_mac10", new WeaponData { Name = "за убийство с MAC-10", Color = "{Red}", Exp = 7 } },
                    { "weapon_ump45", new WeaponData { Name = "за убийство с UMP-45", Color = "{Blue}", Exp = 6 } },
                    { "weapon_mp9", new WeaponData { Name = "за убийство с MP9", Color = "{Yellow}", Exp = 6 } },
                    { "weapon_mp7", new WeaponData { Name = "за убийство с MP7", Color = "{Green}", Exp = 6 } },
                    { "weapon_p90", new WeaponData { Name = "за убийство с P90", Color = "{Orange}", Exp = 5 } },
                    { "weapon_galilar", new WeaponData { Name = "за убийство с Galil AR", Color = "{Blue}", Exp = 6 } },
                    { "weapon_famas", new WeaponData { Name = "за убийство с Famas", Color = "{Yellow}", Exp = 6 } },
                    { "weapon_ak47", new WeaponData { Name = "за убийство с AK-47", Color = "{Red}", Exp = 9 } },
                    { "weapon_m4a1", new WeaponData { Name = "за убийство с M4A1", Color = "{Green}", Exp = 9 } },
                    { "weapon_m4a1_silencer", new WeaponData { Name = "за убийство с M4A1-s", Color = "{Blue}", Exp = 9 } },
                    { "weapon_aug", new WeaponData { Name = "за убийство с AUG", Color = "{Yellow}", Exp = 8 } },
                    { "weapon_sg556", new WeaponData { Name = "за убийство с SG-553", Color = "{Green}", Exp = 8 } },
                    { "weapon_ssg08", new WeaponData { Name = "за убийство с SSG-08", Color = "{Orange}", Exp = 7 } },
                    { "weapon_awp", new WeaponData { Name = "за убийство с AWP", Color = "{Red}", Exp = 10 } },
                    { "weapon_scar20", new WeaponData { Name = "за убийство с SCAR-20", Color = "{Green}", Exp = 7 } },
                    { "weapon_g3sg1", new WeaponData { Name = "за убийство с G3SG1", Color = "{Yellow}", Exp = 7 } },
                    { "weapon_mp5sd", new WeaponData { Name = "за убийство с MP5-SD", Color = "{Green}", Exp = 7 } }
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(configPath, json);
                Console.WriteLine($"[LR-WEAPONS] Default config created at {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LR-WEAPONS] Error creating default config: {ex.Message}");
            }
        }

        private void CreateDbTableIfNotExists()
        {
            if (_levelsRanksApi == null)
                return;

            var connectionString = _levelsRanksApi.DbConnectionString;
            var tableName = $"{_levelsRanksApi.TableName}_weapons";
            var createTableQuery = $@"
                CREATE TABLE IF NOT EXISTS `{tableName}` 
                (
                    `steam` varchar(32) NOT NULL, 
                    `classname` varchar(64) NOT NULL, 
                    `kills` int NOT NULL DEFAULT 0,
                    PRIMARY KEY (`steam`, `classname`)
                ) CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                connection.Execute(createTableQuery);
                Console.WriteLine($"[LR-WEAPONS] Table {tableName} created or already exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LR-WEAPONS] Error in CreateDbTableIfNotExists: {ex.Message}");
            }
        }
        private class WeaponData
        {
            public string Name { get; set; }
            public string Color { get; set; }
            public int Exp { get; set; }
        }
        private class WeaponConfig
        {
            public bool ExperienceEnabled { get; set; }
            public Dictionary<string, WeaponData> Weapons { get; set; }
        }
    }
}
