# Slapshot Replay Mod

A custom replay mod for Slapshot Rebound, built using MelonLoader and .NET 6.0. This mod adds advanced replay functionality to enhance your gameplay experience.

## Features

- **Clipping**: Save specific moments from your gameplay.
- **Manual Recording Control**: Start and stop recordings manually using keyboard shortcuts.
- **Time Navigation**: Skip forward and backward by 5 seconds during playback.
- **Pause/Resume**: Pause and resume replays.
- **Cosmetics and Nametags**: Stores and displays player cosmetics and nametags during replays.

## Installation

1. **Install MelonLoader**: Follow the [MelonLoader installation guide](https://melonwiki.xyz/) to set up MelonLoader for Slapshot Rebound.

2. **Download the Mod**: Download the latest release from the [releases page](https://github.com/sudhakara-ambati/slapshot-replay-mod/releases).

3. **Copy the DLL**: Extract the downloaded ZIP file and place the `SlapshotReplayMod.dll` file into your game's `Mods` folder.

4. **Launch the Game**: Start Slapshot Rebound. The mod should load automatically.

## Usage

- **Start Recording**: Press `F1` to start recording your gameplay.
- **Stop Recording**: Press `F2` to stop recording and save the file.
- **Save Clip**: Press `F4` to save the current circular buffer as a clip.
- **Toggle GUI**: Press `F5` to show or hide the mod's GUI.
- **Pause/Resume Playback**: Press the `Down Arrow` to toggle pause/resume during playback.
- **Seek Forward**: Press the `Right Arrow` to skip forward 5 seconds.
- **Seek Backward**: Press the `Left Arrow` to skip backward 5 seconds.
- **Stop Playback**: Press the `Up Arrow` to stop playback and reset.

## Configuration

Configuration for key bindings and other settings is managed within the code. If needed, you can modify the key bindings directly in the `ReplayMod` class.
