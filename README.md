# Anon

Anon is a Dalamud plugin for FFXIV that hides who you and the people around you are, on your own screen. Every player gets a fake name and a random face and outfit, so you can stream, record or take screenshots without showing anyone's real character.

It only changes what your game draws. Nothing is sent to the server and nobody else sees it. Other players still see your real name and look, and turning Anon off brings everyone back.

![The Anon window](images/window.png)

## Install

1. Type `/xlsettings` in game and open the Experimental tab.
2. Under Custom Plugin Repositories, add this link, tick Enabled and press Save:

   ```
   https://github.com/RiddleTheWorld/Anon/releases/latest/download/repo.json
   ```

3. Open `/xlplugins`, search for Anon and install it.
4. Type `/anon` to open the window.

## How it works

When Anon is on, each player gets a fake name and a random look. The fake name is worked out from their real name, so the same person keeps the same fake name every time you see them, even after a restart. That only changes when you reroll.

Looks work the same way. Each player gets a random face, body and race that stays put until you reroll. Changing someone's race or face means the game has to reload their model, so you may see people flicker once as they get swapped.

## General

- **Enabled** turns the whole plugin on or off.
- **Me** disguises your own character.
- **Everyone else** disguises the other players around you.
- **Diagnostics** writes extra info to the Dalamud log (`/xllog`). Leave it off unless you're trying to track down a problem.

The buttons at the bottom:

- **Reroll names** gives everyone new fake names. Custom names you've set, fake worlds and your chosen data center stay the same.
- **Reroll looks** gives everyone new random faces and outfits. Saved looks you've put on people stay.
- **Reroll all** does both, and also picks new fake worlds and a new random data center (unless you picked a specific one on the Names tab).

## Look

The two options at the top control the random faces:

- **Same race as me** keeps every random look the same race as your real character.
- **Include Hrothgar and Viera** lets random looks use those two races as well. Off by default.

### Saved looks

You can copy someone's appearance, save it under a name and put it on any player, including yourself.

- **Copy from target** saves your target's look. **Copy my look** saves yours.
- A copy includes the face and body, all gear with dyes, facewear, hat and visor settings, Viera ear display, free company crest and weapons.
- **To me** and **To target** put a saved look on you or your target. Click it again to take it off. The look stays saved either way.
- Saved looks work even when Me or Everyone else is turned off, as long as Anon is enabled.
- Weapons only get copied onto someone who's holding the same kind of weapon.
- You can rename a saved look by editing its name, and delete it with Delete then Confirm. Deleting one puts anyone wearing it back to normal.
- Once you have more than 20, a search box shows up.

A saved look is a snapshot. It stores what the person looked like the moment you pressed Copy and never changes after that. If they change their glamour later, or you reroll and their fake look changes, your copy stays the old version. Copy them again if you want the new one.

Every copy keeps two versions: how the person really looks, and exactly what was on your screen when you copied them (their fake look, if Anon was disguising them). **Show: Real look / What I see** switches every saved look between those two at once.

Saved looks are kept in `appearances.json` in the plugin's config folder, so they survive restarts.

## Names

Choose where fake names show up:

- **Replace names in chat** swaps names in the chat log. Anon also picks up names from chat messages, so people you only talk to get masked too.
- **Replace names in party list** covers the party list on screen.
- **Replace names in other windows** covers things like the target bar, focus target, the character and examine windows, adventurer plates, item tooltips, right-click menus, the alliance lists, and the friend and free company member lists.

Nameplates above heads are always replaced for anyone who's disguised.

Other options:

- **Hide server and data center names** replaces world and data center names with fake ones, including the server info bar.
- **Fake data center** lets you pick which data center to show. Random is the default and never picks your current or home data center.
- **Hide free company tags** and **Hide titles** remove those from nameplates.

### Custom names

Custom names let you decide exactly what a specific player is called. Type their real name (or use Add target), then type whatever you want them to show as. Any text works. The Random button next to an entry rolls a new name for that one person, and Remove takes them off the list.

A custom name only replaces whole words, so giving someone called "Al" a custom name won't turn "Alliance" into nonsense. Custom names also apply to people on the Players list.

## Gear

**Outfit** picks what disguised players wear:

- **Real** keeps their own gear.
- **Plain** strips everything off.
- **Random** gives each player a random outfit. It stays the same for them until you reroll looks.

Some gear only exists for one race or gender, like the starter outfits. When someone gets a new race or gender, those pieces get swapped for something that fits, so nobody ends up with an invisible body.

**Hide weapons** hides everyone's weapons.

## Players

Players on this list are left completely alone: real name, real look. Use this for friends you don't mind showing.

You can add people three ways: pick from the list of nearby players (with a search box and distances), add your target, or type a real name.

## Fake Convo

This writes a made-up line into your own chat log. It's handy for screenshots. Nothing is sent and only you see it.

- Pick a channel: Say, Yell, Shout, Tell (incoming or outgoing), Party, Alliance, Linkshells 1 to 8, Free Company, Novice Network or PvP Team. The line gets that channel's colour and icon.
- The name field is who's talking. For an outgoing tell it's who you're sending to.
- For Party you can pick a member number (1 to 8) to get the little number badge the game shows before party names.
- Type the message and press Enter or Add line.
- **Read back** shows what the game actually stored for the last line.

It's plain text, so it won't have a clickable name, free company tag or world name unless you type them in yourself. It uses the current time, so it looks like a line that just came in.

## Keybinds

You can set keys for Toggle on/off, Reroll names, Reroll looks and Reroll all. Click a button, then press the key combination you want (Ctrl, Shift and Alt work). Escape cancels and Clear removes a binding.

The key still goes through to the game too, so pick something you don't already use.

## Safety

Swapping someone's face reloads their model, which can cause hitches if a lot of people get swapped at once.

- **Pause redraws in duties** and **Pause redraws in combat** hold off model changes in those situations. Names are still masked while paused.
- **Redraws per frame** limits how many characters get reloaded per frame. Lower is smoother but slower to catch up in busy areas.
- **Disable in listed zones** turns Anon off in zones you add. Stand in a zone and press Add current zone to list it. Everyone shows their real name and look there until you remove the zone.

## Commands

| Command | What it does |
|---|---|
| `/anon` | Opens or closes the window |
| `/anon on` / `/anon off` | Turns Anon on or off |
| `/anon reroll` | New names and looks |
| `/anon names` | New names only |
| `/anon look` | New looks only |

## Found a problem?

Please report it on the [issues page](https://github.com/RiddleTheWorld/Anon/issues). It helps a lot if you include:

- what you were doing and what went wrong
- a screenshot if it's something you can see
- whether it happens every time or only sometimes

If you can, turn on Diagnostics in the General tab, make the problem happen again, then open `/xllog` and copy any lines that mention Anon into the report.

## Building it yourself

You need the .NET 10 SDK and Dalamud installed through XIVLauncher.

```
dotnet build -c Release
```

The plugin ends up in `Anon/bin/x64/Release/`. To try a local build, add the full path to `Anon.dll` under Dev Plugin Locations in `/xlsettings` (Experimental tab), then enable it in `/xlplugins`.

To publish a release, push a version tag such as `v1.0.1`. GitHub Actions builds it and attaches the files people install from.

## License

AGPL-3.0, see [LICENSE.md](LICENSE.md).
