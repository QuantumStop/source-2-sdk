<div align="center">

<img width="560" height="249" alt="ignis_sdk" src="https://github.com/user-attachments/assets/7b12805f-ec65-4671-877a-88e1d66cea3e" />

</div>


## What is Ignis?

Ignis is a fork of Facepunch's Source 2 engine, available [here](https://github.com/Facepunch/sbox-public). Designed to be similar to legacy Source 2 in workflows and UX, as time goes this will have less and less overlap with ongoing Facepunch version of that engine.

This fork is used by QuantumStop (title pending), giving it modifications we require while developing our games.

## Compiling

If you want to build, this repository includes all the necessary files to compile the engine yourself.

| Platform | Setup | Notes |
|----------|-------|-------|
| Windows 10 / 11 (x64) | `Setup.bat` | |
| Linux (x64) | `./Setup.sh` | Binaries target the Steam Linux Runtime, most distros should work. |
| macOS (Apple Silicon) | `./Setup.sh` | Intel Macs are not supported. |

#### Prerequisites

* [Git](https://git-scm.com/downloads)
* [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
* An IDE for the C# code is recommended: [Visual Studio 2026](https://visualstudio.microsoft.com/) or
  [Rider](https://www.jetbrains.com/rider/) on Windows, Rider or [VS Code](https://code.visualstudio.com/) on Linux and macOS.

#### Setup

```bash
# Clone the repo
git clone https://github.com/Facepunch/sbox-public.git

cd sbox-public

# Windows
Setup.bat

# Linux / macOS
./Setup.sh
```

Once you've cloned the repo simply run `Setup.bat` which will download dependencies and build the engine.

The game and editor can be run from the binaries in the game folder.


## License

The s&box engine source code is licensed under the [MIT License](LICENSE.md).

Certain native binaries in `game/bin` are not covered by the MIT license. These binaries are distributed under the s&box EULA. You must agree to the terms of the EULA to use them.

This project includes third-party components that are separately licensed.
Those components are not covered by the MIT license above and remain subject
to their original licenses as indicated in `game/thirdpartylegalnotices`.
