// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>The picture that goes with a How to Play page.</summary>
public enum GuideArt
{
    /// <summary>A small base: construction yard, power plant, refinery.</summary>
    Base,
    /// <summary>A group of tanks with a drag box around them.</summary>
    Selecting,
    /// <summary>A tank with a move arrow and an attack arrow.</summary>
    Orders,
    /// <summary>The sidebar tabs and a building ghost.</summary>
    Building,
    /// <summary>A scraper between an ashfall field and a refinery.</summary>
    Harvesting,
    /// <summary>Two tunnel entrances linked underground.</summary>
    Tunnels,
    /// <summary>The Upgrades tab.</summary>
    Upgrades,
    /// <summary>A keyboard cheat sheet (drawn as text only).</summary>
    Keys,
}

/// <summary>One page of the guide.</summary>
/// <param name="Title">Page heading.</param>
/// <param name="Art">The illustration.</param>
/// <param name="Lines">Bullet text; each entry is wrapped to the panel width.</param>
public sealed record GuidePage(string Title, GuideArt Art, IReadOnlyList<string> Lines);

/// <summary>
/// The text of How to Play. Every control named here exists in the game now (checked against GameScreen, OrderController and the
/// sidebar); keep it that way when controls change.
/// </summary>
public static class HowToPlayContent
{
    /// <summary>The pages in order.</summary>
    public static IReadOnlyList<GuidePage> Pages { get; } = new[]
    {
        new GuidePage("The basics", GuideArt.Base, new[]
        {
            "Build a base, gather ashfall, raise an army and destroy everything the enemy has.",
            "Everything starts from the Construction Yard. Buildings need power: keep your Power Plants ahead of what you build.",
            "Ashfall is the only resource. Refineries turn it into credits; credits pay for everything else.",
            "The Authority is a slow, heavy surface army. The Directorate hides its base underground and swarms.",
        }),
        new GuidePage("Selecting", GuideArt.Selecting, new[]
        {
            "Left-click a unit to select it. Click empty ground to clear the selection.",
            "Drag a box with the left mouse button to select every one of your units inside it.",
            "Click a building to select it; its health and what it can do show in the sidebar.",
            "Health bars appear over selected units and damaged buildings.",
        }),
        new GuidePage("Moving and attacking", GuideArt.Orders, new[]
        {
            "Right-click ground to move the selected units there.",
            "Right-click an enemy to attack it.",
            "Press A, then right-click ground: the units attack anything they meet on the way (attack-move). Press A again to cancel.",
            "S stops. G guards the spot a unit stands on. X scatters the group.",
            "Units fire on their own at enemies in range.",
        }),
        new GuidePage("Building", GuideArt.Building, new[]
        {
            "The sidebar has five tabs: Q Structures, W Defences, E Infantry, R Vehicles, T Upgrades.",
            "Click a cameo to start building it; the bar fills while it is made. Right-click a cameo to cancel.",
            "A finished structure waits for you: click it, then click a green spot near your base to place it. Red means blocked.",
            "Units are made at their production building and appear beside it. Set a rally point to send them on.",
            "Sell and Repair work on any of your buildings. Esc backs out of placing.",
        }),
        new GuidePage("Harvesting ashfall", GuideArt.Harvesting, new[]
        {
            "Build a Refinery. A Scraper comes with it and drives out to the nearest ashfall field on its own.",
            "The Scraper scoops ash, carries it back to a Refinery or Silo and unloads; credits rise as it does.",
            "More Scrapers mean faster income, but ash fields run thin. Pitchblende pits are rare and rich.",
            "Ash harms unprotected infantry: keep them out of the thick of it, or inside vehicles.",
            "Guard your Scrapers. The enemy hunts them first.",
        }),
        new GuidePage("Tunnels", GuideArt.Tunnels, new[]
        {
            "The Directorate lives under the map. Its entrances and vents on the surface are the only part you can hit.",
            "Right-click one of your own tunnel entrances with a unit that fits to send it in. It can come out of any other entrance of your network.",
            "A Bore digs new entrances: select it, press D, then right-click the cell to dig at.",
            "Land-trains and transports unload with D.",
            "Watch for entrances opening near your base. Surface attacks come with no warning.",
        }),
        new GuidePage("Upgrades", GuideArt.Upgrades, new[]
        {
            "Press T (or click the UPG tab) for upgrades. Only upgrades your buildings can research right now are listed as buildable.",
            "Research takes time and credits, like building. The upgrade then applies to your whole army.",
            "Some upgrades need a particular building first; the cameo tells you what is missing.",
            "Late in the game a superweapon structure can be built. It charges for a long time; so does the one aimed at you.",
        }),
        new GuidePage("Hotkeys", GuideArt.Keys, new[]
        {
            "Mouse: left select / drag box / place, right order / cancel, wheel zoom, middle-drag scroll the map.",
            "Arrow keys scroll the map. Click or drag the minimap to jump.",
            "A attack-move   S stop   G guard   X scatter   D deploy / unload.",
            "Q W E R T sidebar tabs.   Esc pause menu (or back out of placing).   Space pause the game.",
            "F3 occupancy, F4 paths and F5 ranges overlays (for testing).",
        }),
    };
}
