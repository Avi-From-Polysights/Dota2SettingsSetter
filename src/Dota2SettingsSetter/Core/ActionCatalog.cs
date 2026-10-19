using System;
using System.Collections.Generic;
using System.Linq;

namespace Dota2SettingsSetter.Core;

/// <summary>
/// Where a hotkey is live. Hotkeys only clash with others in the same context:
/// the shop's tab keys or talent-tree keys can reuse Q or 1 without conflict.
/// </summary>
public enum BindContext
{
    Game,
    Shop,
    TalentTree,
    NeutralPick,
}

public sealed class ActionInfo
{
    /// <summary>Entry name in dotakeys_personal.lst, e.g. "Inventory1".</summary>
    public string Name { get; }
    public string Label { get; }
    public string Group { get; }
    public BindContext Context { get; }

    /// <summary>Command for a console <c>bind</c>, or null when it can't be switched from the console.</summary>
    public string ConsoleCommand { get; }

    /// <summary>For "Alt + key" style actions that follow another action's key (e.g. item autocast).</summary>
    public string DependsOn { get; }

    /// <summary>The modifier a dependent action adds to its dependency's key.</summary>
    public string DependencyModifier => DependsOn == null ? null : Name == "ChatGlobal" ? "SHIFT" : "ALT";

    /// <summary>Hidden actions aren't listed in the editor but still follow their dependency.</summary>
    public bool Hidden { get; }

    public int Order { get; internal set; }

    public ActionInfo(string name, string label, string group, string consoleCommand,
        BindContext context = BindContext.Game, string dependsOn = null, bool hidden = false)
    {
        Name = name;
        Label = label;
        Group = group;
        ConsoleCommand = consoleCommand;
        Context = context;
        DependsOn = dependsOn;
        Hidden = hidden;
    }

    public bool CanUseConsole => ConsoleCommand != null && Context == BindContext.Game;

    public override string ToString() => Name;
}

/// <summary>
/// The hotkeys this app knows how to edit. Names, actions and labels come from the game's
/// cfg/dotakeys.lst and English localization. Console commands use the single-argument
/// forms Dota itself has written into dotakeys_personal.lst.
/// </summary>
public static class ActionCatalog
{
    public const string Abilities = "Abilities";
    public const string Items = "Items";
    public const string UnitActions = "Unit Actions";
    public const string ControlGroups = "Control Groups";
    public const string Camera = "Camera";
    public const string Interface = "Interface";
    public const string Chat = "Chat";
    public const string ShopPanel = "Shop Panel";

    public static IReadOnlyList<string> Groups { get; } = new[]
    {
        Abilities, Items, UnitActions, ControlGroups, Camera, Interface, Chat, ShopPanel,
    };

    public static IReadOnlyList<ActionInfo> All { get; }

    static readonly Dictionary<string, ActionInfo> ByName;
    static readonly Dictionary<string, List<ActionInfo>> DependentsByName;

    static ActionCatalog()
    {
        var list = Build();
        for (int i = 0; i < list.Count; i++)
            list[i].Order = i;
        All = list;
        ByName = list.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        DependentsByName = list.Where(a => a.DependsOn != null)
            .GroupBy(a => a.DependsOn, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public static ActionInfo Find(string name) =>
        name != null && ByName.TryGetValue(name, out var info) ? info : null;

    public static IReadOnlyList<ActionInfo> DependentsOf(string name) =>
        DependentsByName.TryGetValue(name, out var list) ? list : (IReadOnlyList<ActionInfo>)Array.Empty<ActionInfo>();

    static List<ActionInfo> Build()
    {
        var a = new List<ActionInfo>();
        string[] abilityNames = { "AbilityPrimary1", "AbilityPrimary2", "AbilityPrimary3", "AbilitySecondary1", "AbilitySecondary2", "AbilityUltimate" };
        string[] abilityLabels = { "Ability 1", "Ability 2", "Ability 3", "Ability 4", "Ability 5", "Ultimate" };

        for (int i = 0; i < 6; i++)
            a.Add(new ActionInfo(abilityNames[i], "Cast " + abilityLabels[i], Abilities, $"dota_ability_execute {i}"));
        for (int i = 0; i < 6; i++)
            a.Add(new ActionInfo(abilityNames[i] + "QuickCast", "Quickcast " + abilityLabels[i], Abilities, $"dota_ability_quickcast {i}"));
        for (int i = 0; i < 6; i++)
            a.Add(new ActionInfo(abilityNames[i] + "AutoCast", "Autocast " + abilityLabels[i] + " (Alt)", Abilities,
                $"dota_ability_autocast {i}", dependsOn: abilityNames[i]));
        for (int i = 0; i < 6; i++)
            a.Add(new ActionInfo(abilityNames[i] + "AutoCastAlternate", "Autocast " + abilityLabels[i], Abilities, $"dota_ability_autocast {i}"));
        for (int i = 0; i < 6; i++)
            a.Add(new ActionInfo(abilityNames[i] + "AutoCastAlternate2", "Autocast " + abilityLabels[i] + " (Alt, quickcast key)", Abilities,
                $"dota_ability_autocast {i}", dependsOn: abilityNames[i] + "QuickCast", hidden: true));
        a.Add(new ActionInfo("LearnAbilities", "Learn Ability", Abilities, "dota_ability_learn_mode"));
        a.Add(new ActionInfo("LearnStats", "Upgrade Talents", Abilities, "dota_learn_stats"));
        a.Add(new ActionInfo("TalentUpgradeStatLeft", "Upgrade Left Talent", Abilities, null, BindContext.TalentTree));
        a.Add(new ActionInfo("TalentUpgradeStatRight", "Upgrade Right Talent", Abilities, null, BindContext.TalentTree));
        a.Add(new ActionInfo("TalentUpgradeAttribute", "Upgrade Attributes", Abilities, null, BindContext.TalentTree));

        var slots = new List<(string Name, string Label, int Slot)>();
        for (int i = 1; i <= 6; i++)
            slots.Add(("Inventory" + i, "Inventory Slot " + i, i - 1));
        slots.Add(("InventoryTP", "Teleport Scroll", 15));
        slots.Add(("InventoryNeutral", "Neutral Item", 16));

        foreach (var s in slots)
            a.Add(new ActionInfo(s.Name, "Activate " + s.Label, Items, $"dota_item_execute {s.Slot}"));
        foreach (var s in slots)
            a.Add(new ActionInfo(s.Name + "QuickCast", "Quickcast " + s.Label, Items, $"dota_item_quick_cast {s.Slot}"));
        foreach (var s in slots)
            a.Add(new ActionInfo(s.Name + "AutoCast", "Autocast " + s.Label + " (Alt)", Items, $"dota_item_autocast {s.Slot}", dependsOn: s.Name));
        foreach (var s in slots)
            a.Add(new ActionInfo(s.Name + "QuickAutoCast", "Autocast " + s.Label + " (Alt, quickcast key)", Items,
                $"dota_item_autocast {s.Slot}", dependsOn: s.Name + "QuickCast", hidden: true));
        a.Add(new ActionInfo("ShopToggle", "Open Shop", Items, "toggleshoppanel"));
        a.Add(new ActionInfo("PurchaseQuickbuy", "Purchase Quickbuy", Items, "dota_purchase_quickbuy"));
        a.Add(new ActionInfo("PurchaseSticky", "Purchase Sticky", Items, "dota_purchase_stickybuy"));
        a.Add(new ActionInfo("GrabStashItems", "Take Stash Items", Items, "stash_grab_all"));
        a.Add(new ActionInfo("ToggleBuybackProtection", "Toggle Buyback Protection", Items, "dota_toggle_buyback_protection"));
        for (int i = 1; i <= 5; i++)
            a.Add(new ActionInfo("NeutralItemSelect" + i, "Select Neutral Item Option " + i, Items, null, BindContext.NeutralPick));

        a.Add(new ActionInfo("HeroAttack", "Attack Move / Force Attack", UnitActions, "mc_attack"));
        a.Add(new ActionInfo("HeroMove", "Move", UnitActions, "mc_move"));
        a.Add(new ActionInfo("HeroMoveDirection", "Directional Move", UnitActions, "+dota_unit_movetodirection"));
        a.Add(new ActionInfo("HeroPatrol", "Patrol", UnitActions, "mc_patrol"));
        a.Add(new ActionInfo("HeroStop", "Cancel Current Action", UnitActions, "dota_stop"));
        a.Add(new ActionInfo("HeroHold", "Hold Position", UnitActions, "dota_hold"));
        a.Add(new ActionInfo("HeroSelect", "Select Hero", UnitActions, "+dota_camera_follow"));
        a.Add(new ActionInfo("SelectAll", "Select All Controlled Units", UnitActions, "dota_select_all"));
        a.Add(new ActionInfo("SelectAllOthers", "Select All Other Units", UnitActions, "dota_select_all_others"));
        a.Add(new ActionInfo("CourierSelect", "Select Courier", UnitActions, "dota_select_courier"));
        a.Add(new ActionInfo("CourierDeliver", "Deliver Items", UnitActions, "dota_courier_deliver"));
        a.Add(new ActionInfo("CourierBurst", "Courier Burst", UnitActions, "dota_courier_burst"));
        a.Add(new ActionInfo("CourierShield", "Courier Shield", UnitActions, "dota_courier_shield"));
        a.Add(new ActionInfo("HeroTaunt", "Taunt", UnitActions, "use_item_client current_hero taunt"));
        a.Add(new ActionInfo("ToggleAutoattack", "Toggle Autoattack", UnitActions, "dota_toggle_autoattack"));
        a.Add(new ActionInfo("FilterEnemy", "Interact only with Enemy Units", UnitActions, "+dota_filter_enemy"));
        a.Add(new ActionInfo("FilterAlly", "Interact only with Allied Units", UnitActions, "+dota_filter_ally"));
        a.Add(new ActionInfo("FilterHero", "Interact only with Hero Units", UnitActions, "+dota_filter_hero"));
        a.Add(new ActionInfo("FilterNonHero", "Interact only with Non-Hero Units", UnitActions, "+dota_filter_non_hero"));
        for (int i = 1; i <= 5; i++)
            a.Add(new ActionInfo("SelectAlly" + i, "Select Ally " + i, UnitActions, $"dota_select_ally {i}"));

        for (int i = 1; i <= 10; i++)
            a.Add(new ActionInfo("ControlGroup" + i, "Control Group " + i, ControlGroups, $"+dota_control_group {i}"));
        a.Add(new ActionInfo("ControlGroupCycle", "Next Unit", ControlGroups, "dota_cycle_selected"));
        a.Add(new ActionInfo("ControlGroupCyclePrev", "Previous Unit", ControlGroups, "dota_cycle_prev_selected"));

        a.Add(new ActionInfo("CameraUp", "Camera Up", Camera, "+forward"));
        a.Add(new ActionInfo("CameraDown", "Camera Down", Camera, "+back"));
        a.Add(new ActionInfo("CameraLeft", "Camera Left", Camera, "+left"));
        a.Add(new ActionInfo("CameraRight", "Camera Right", Camera, "+right"));
        a.Add(new ActionInfo("CameraGrip", "Camera Grip", Camera, "+cameragrip"));
        a.Add(new ActionInfo("CameraYawGrip", "Camera Rotate Grip", Camera, "+camerayawgrip"));
        a.Add(new ActionInfo("CameraZoomIn", "Camera Zoom In", Camera, "camerazoomin"));
        a.Add(new ActionInfo("CameraZoomOut", "Camera Zoom Out", Camera, "camerazoomout"));
        a.Add(new ActionInfo("RecentEvent", "Show Recent Event", Camera, "dota_recent_event"));
        for (int i = 1; i <= 10; i++)
            a.Add(new ActionInfo("CameraSavedPosition" + i, "Camera Position " + i, Camera, $"dota_camera_saved_position {i}"));
        a.Add(new ActionInfo("InspectHeroInWorld", "Showcase View", Camera, "inspectheroinworld"));

        a.Add(new ActionInfo("ScoreboardToggle", "Scoreboard", Interface, "+showscores"));
        a.Add(new ActionInfo("OverlayMapToggle", "Show Overlay Map", Interface, "+dota_hud_toggle_overlaymap"));
        a.Add(new ActionInfo("CombatLogToggle", "Combat Log", Interface, "dota_toggle_combatlog"));
        a.Add(new ActionInfo("ActivateGlyph", "Activate Glyph", Interface, "dota_glyph"));
        a.Add(new ActionInfo("ActivateRadar", "Activate Scan", Interface, "dota_radar"));
        a.Add(new ActionInfo("DeathSummary", "Death Summary", Interface, "dota_show_killgraph"));
        a.Add(new ActionInfo("Pause", "Pause", Interface, "dota_pause"));
        a.Add(new ActionInfo("ScreenshotSettings", "Screenshot", Interface, "jpeg_screenshot"));
        a.Add(new ActionInfo("Console", "Console", Interface, "toggleconsole"));

        // Chat entry keys open Dota's own chat box; there's no plain console command for them.
        a.Add(new ActionInfo("ChatTeam", "Chat to Team", Chat, null));
        a.Add(new ActionInfo("ChatGlobal", "Chat to Everyone", Chat, null, dependsOn: "ChatTeam"));
        a.Add(new ActionInfo("ChatVoiceParty", "Voice Chat (Party)", Chat, "+voicerecord_party"));
        a.Add(new ActionInfo("ChatVoiceTeam", "Voice Chat (Team)", Chat, "+voicerecord"));
        a.Add(new ActionInfo("ChatWheel", "Chat Wheel 1", Chat, "+chatwheel"));
        a.Add(new ActionInfo("ChatWheel2", "Chat Wheel 2", Chat, "+chatwheel2"));
        a.Add(new ActionInfo("HeroChatWheel", "Hero Chat Wheel", Chat, "+herochatwheel"));
        a.Add(new ActionInfo("SprayWheel", "Spray Wheel", Chat, "+spraywheel"));
        string[] phrases = { "Care", "Get Back", "Need Wards", "Stun", "Help", "Push", "Well Played", "Missing", "Missing Top", "Missing Middle", "Missing Bottom" };
        string[] phraseNames = { "Care", "GetBack", "NeedWards", "Stun", "Help", "GetPush", "GoodJob", "Missing", "MissingTop", "MissingMiddle", "MissingBottom" };
        for (int i = 0; i < phrases.Length; i++)
            a.Add(new ActionInfo("ChatWheel" + phraseNames[i], "Say: " + phrases[i], Chat, $"chatwheel_say {i + 1}"));

        string[] tabs = { "Consumables", "Attributes", "Armaments", "Arcane", "Basics", "Support", "Caster", "Weapons", "Armor", "Artifacts", "SidePage1", "SidePage2", "Secret" };
        string[] tabLabels = { "Consumables", "Attributes", "Armaments", "Arcane", "Basics", "Support", "Caster", "Weapons", "Armor", "Artifacts", "Side Page 1", "Side Page 2", "Secret" };
        for (int i = 0; i < tabs.Length; i++)
            a.Add(new ActionInfo("ShopTab" + tabs[i], "Shop Tab: " + tabLabels[i], ShopPanel, null, BindContext.Shop));
        a.Add(new ActionInfo("ShopTabSearchBox", "Shop Search", ShopPanel, null, BindContext.Shop));
        for (int i = 1; i <= 14; i++)
            a.Add(new ActionInfo("ShopSlot" + i, "Shop Item Row " + i, ShopPanel, null, BindContext.Shop));

        return a;
    }
}
