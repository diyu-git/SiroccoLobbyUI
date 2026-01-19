# Third-Party Licenses

This project uses the following third-party software:

## Steamworks.NET

- **License**: MIT License
- **Copyright**: (c) 2013-2022 Riley Labrecque
- **Source**: https://github.com/rlabrecque/Steamworks.NET
- **License Text**: See `SLL/steamworks/LICENSE.txt`
- **Redistribution**: This project redistributes `Steamworks.NET.dll` under the terms of the MIT License.

### Steamworks.NET License

```
The MIT License (MIT)

Copyright (c) 2013-2022 Riley Labrecque

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

---

## Runtime Dependencies (Not Redistributed)

The following dependencies are **required at runtime** but are **NOT redistributed** with this mod. Users must install them separately:

### MelonLoader

- **License**: Apache License 2.0
- **Source**: https://github.com/LavaGang/MelonLoader
- **Installation**: Users must download and install from official releases
- **Note**: This mod does not redistribute MelonLoader or any of its components

### Steamworks SDK (Native DLLs)

- **License**: Proprietary (Valve Corporation)
- **Source**: Included with Steam games
- **Note**: This mod does **NOT** redistribute `steam_api64.dll` or other native Steamworks binaries. These are loaded from the user's game installation.

### Unity Engine Runtime

- **License**: Proprietary (Unity Technologies)
- **Source**: Included with the game installation
- **Note**: This mod does **NOT** redistribute Unity runtime DLLs. These are loaded from the user's game installation.

### Game Assemblies

- **License**: Proprietary (Game Developer/Publisher)
- **Source**: Included with the game installation
- **Note**: This mod does **NOT** redistribute any game assemblies or decompiled game code.
