# Ultimate Fortnite Modding Tool

A modding tool for importing the latest Fortnite assets to older Fortnite builds.

## Features

- **Automated mesh conversion** — Converts exported .psk mesh files to .fbx format automatically using Blender
- **Automated animation conversion** — Converts exported .psa (lobby) animation file to .fbx format using Blender and applies the correct animation length automatically
- **Automatic Physics Asset generation** — Converts exported .json physics asset(s) file(s) to physics assets using Physics Importer (All credits go to JsonAsAsset - More listed below)
- **Automatic texture assignment** — Connects exported textures with their corresponding materials, with manual override options if needed
- **Real-time preview rendering** — Render a preview of your skin before export (Note: The preview won't be 100% accurate since Blender is a different rendering engine than Unreal Engine)
- **Automatic asset generation** — Automatically generates all required Fortnite cosmetic assets (CID, EID, Animation montages, Sound cues, HID, HS, CPs) and modifies them for game compatibility
- **AssetRegistry generation** — Creates the AssetRegistry.bin file so Fortnite recognizes your custom cosmetics
- **Official Engine Support** — Compatible with standard Unreal Engine versions (no custom FnGameProj builds required). See compatible versions below.
- **Automatic packing** — u4Pak is not required, you can find the .pak next to FortniteGame folder in `[cosmetic codename]\[Fortnite Version]\Output`
- **Fast workflow** — Backport a custom cosmetic in under 1 minute

## Requirements

Before using this tool, you need to install and configure:

- **Windows OS** (tested on Windows 10\11)
- **.NET Framework** (version required by your system)
- **Blender 5.0** — [Download here](https://www.blender.org/download/)
  - Install these plugins via: Edit → Preferences → Add-ons → Install from File, select the plugin, and enable it
  - **PSA\PSK Importer Plugin** — [Download here](https://extensions.blender.org/download/sha256:a134bfa33f54804b9434149dea11472fc6ea43bb851a72d07df3511c843a4a4d/add-on-io-scene-psk-psa-v9.1.2.zip?repository=%2Fapi%2Fv1%2Fextensions%2F&blender_version_min=5.0.0)
  - **Better FBX Exporter** — [Download here](https://github.com/notzyron/Ultimate-Fortnite-Modding-Tool/releases/download/v1.5.3/better_fbx-6.3.5.zip)
- **Unreal Engine (Check the app settings to see which UE version your Fortnite build requires)** — [Download from Epic Games Launcher](https://www.epicgames.com/store/en-US/download)
Open Unreal Engine, create a new project, then go to Edit → Plugins and enable:
  - `Python Editor Script Plugin` (built-in)
  - `Editor Scripting Utilities` (built-in)

## Youtube Tutorials
- **[Installation & Setup Guide](https://www.youtube.com/watch?v=6Q10xLEo6HM)** — full walkthrough on how to install & set up UFMT
- **[How To Import Custom Skins](https://www.youtube.com/watch?v=TRdgbyHvc3Y)** — step-by-step tutorial on backporting your first skin with UFMT

## Installation

### Option 1: Use the Compiled .EXE (Recommended)
1. Download the latest `.exe` from [Releases](https://github.com/notzyron/Ultimate-Fortnite-Modding-Tool/releases)
2. Run the `.exe`
3. **Configure settings** (first launch):
   - Go to Settings page
   - Set **Blender executable path** (e.g., `C:\Program Files\Blender Foundation\Blender 5.0\blender.exe`)
   - Set **UE executable path** (e.g., `C:\Program Files\Epic Games\UE_4.26\Engine\Binaries\Win64\UE4Editor.exe`)
   - Set **UE project path** (path to your Unreal Engine project for UFMT)
   - Select **UE version** (Official UE build or a modded FnGameProj build)
   - Select **FN version**
   - Save settings

### Option 2: Build from Source
1. Clone or download this repository
2. Open `UFMT.sln` in Visual Studio
3. Build the solution (Build → Build Solution)
4. Run the application
5. Follow the settings configuration steps above

## Usage

### Setup

SKINS
1. Go to Skins page
2. Create a folder anywhere on your pc, that will contain your skin folders
3. Click **"Create Skin Folder"** and enter your skin's codename (e.g., `QuarterClaspZoom` from the Character ID)
4. Place your exported .psk meshes in `[skin codename]\Source\Meshes\[mesh character part type]` (eg. if your mesh is a body, place it in [skin codename]\Source\Meshes\Body)
5. Place your exported .psa lobby pose in `[skin codename]\Source\Lobby_Animation` (if your skin has one) and .json if the pose has custom facial animations
6. Place textures and icons in `[skin codename]\Source\Textures\`
7. Place .json Physics Assets in `[skin codename]\Source\Physics\[mesh character part type]`

EMOTES
1. Go to Emotes page
2. Create a folder anywhere on your pc, that will contain your emote folders
3. Click **"Create Emote Folder"** and enter your emote's codename (e.g., `JanuaryBop` from the Emote ID)
4. Place your exported animations' .psa and .json files in `[emote codename]\Source\Animations\` (make sure to keep the animation names unchanged from their original names when exported from FModel)
5. Place your exported .wav sound/music in `[emote codename]\Source\Sound`
6. Place your exported icons in `[emote codename]\Source\Icons\`

### Configure & Preview

SKINS
1. Specify your skin folder path in **"Current Skin Path"**
2. The program auto-detects materials and assigns textures (may have issues with reskins)
3. Manually adjust texture assignments if needed using the dropdowns
4. If the skin has a skin boost color and exponent, enable the option in the material dropdowns and enter the option's red, green, blue and alpha values
5. Enter skin details: **name**, **description**, **rarity**, **series**, **gender**
6. Click **"Render"** to preview the skin
7. If the skin looks too shiny, enable **"Swizzle Roughness to Green"** (This usually happens on skins that were made after chapter 4, due to Engine version switch)

EMOTES
1. Specify your emote folder path in **"Current Emote Path"**
2. The program auto-detects animation length and icons
3. Manually adjust animation length if the emote doesn't use the full animation length
4. Check emote animation montages in FModel if they have a different frame position for loop section (e.g "Jabba's Switchway" uses 2,5684574 instead of 0)
5. Check the emote sound wave compression quality in FModel and set the same value in UFMT.
6. Enter emote details: **name**, **description**, **rarity**, **series**

### Export & Deploy

1. Click **"Export"**
2. The program will:
   - Convert .psk and .psa files to .fbx in Blender
   - Import meshes, textures and animations into Unreal Engine and apply correct settings to them
   - Generate all required cosmetic files
   - Create AssetRegistry.bin for game recognition
3. Your finished cosmetic will be in `[cosmetic codename]\[Fortnite Version]\Output\z_[cosmetic codename].pak` (eg. If you're doing a cosmetic for Fortnite 14.30, the FortniteGame folder will be in `[cosmetic codename]\14.30\Output`)
4. Move the .pak to your Fortnite build Paks folder (`[Path To Your Fortnite Build]\FortniteGame\Content\Paks`), copy any .sig file from that folder and rename it the same as your custom .pak, then launch the game!

**Note:** The export process may take a minute as Blender and UE scripts run in the background (export time varies depending on PC specs; typical systems take 15-30 seconds). Wait until the console displays "Your custom skin is ready! Check the output folder"

## Compatibility

- **Unreal Engine:** 4.22, 4.23 Modded (FnGameProj8.51, FnGameProj9.10, FnGameProj9.41), 4.25, 4.25 Modded (FnGameProj12.41), 4.26, 4.26 Modded (FnGameProj14.30)
- **Fortnite Versions:** v8.51, v9.10, v9.41, v12.41, v13.40, v14.30
- **Blender:** 5.0

## Discord Server
Join the Discord server for help and more info
<p align="left">
  <img src="https://cdn-icons-png.flaticon.com/512/5968/5968756.png" width="28" align="middle" alt="Discord Logo">&nbsp;&nbsp;
  <a href="https://discord.gg/z74WCJS5N">Join our Discord Server</a>
</p>

## Credits
- **[UAssetAPI](https://github.com/atenfyr/UAssetAPI)** — Asset handling and parsing library
- **WinUI 3** — UI framework
- **[QueenIO](https://github.com/Code-Vein-Tool-Hub/QueenIO)** — Asset extraction techniques (adapted code with attribution)
- **[AssetRegistryInjector](https://github.com/Code-Vein-Tool-Hub/AssetRegistryTool)** — Registry injection utilities (adapted code with attribution)
- **[Zylox](https://github.com/zyloxmods)** — BetterFBX exporter plugin, Base Head (for facial animations)
- **[JsonAsAsset](https://github.com/JsonAsAsset/JsonAsAsset)** — Converting .json Physics Assets into Physics Assets and importing .json animation sequences
- **[U4pak](https://github.com/panzi/u4pak)** — Packing the skin after it's finished
- **.teksik** — Shape key combiner (Converting newer 3L metahuman Fortnite heads's facial poses to the old Legacy ones)
- **sspookifyy** — Fix for right food twitching on lobby idle animations

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

**Have questions or found a bug?** Open an issue on GitHub!
