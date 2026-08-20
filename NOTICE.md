# NOTICE — Credits & Upstream Attribution

Cronus is an independent C#/.NET reimplementation of a JMS (Japan MapleStory) v186
private server. It is a derivative work informed by, and interoperating with, the
following upstream projects. We gratefully credit their authors.

## Upstream projects

- **[Riremito/JMSv186](https://github.com/Riremito/JMSv186)** — Java. The primary
  reference implementation and protocol oracle for Cronus. Portions of Cronus'
  network core (AES-OFB cipher, packet framing, opcode tables, serialization
  primitives) are ports of logic originating here.
  - Files under `tacos.*` are © Riremito, licensed **GPL-3.0-or-later**.
  - `tacos.network.MapleAESOFB` / `MapleCustomEncryption` derive from the
    **OdinMS** lineage (© 2008–2010 Patrick Huy, Matthias Butz, Jan Christian Meyer),
    licensed **AGPL-3.0**.
- **[Riremito/EmuClient](https://github.com/Riremito/EmuClient)** — client-side DLL
  injection (localhost redirect, CRC bypass). Used as-is; not modified by Cronus.
- **[Riremito/RirePE](https://github.com/Riremito/RirePE)** — packet editor used for
  differential packet verification between the Java oracle and Cronus.

## Licensing consequence

Because Cronus incorporates ports of AGPL-3.0 and GPL-3.0 logic, the combined work
is distributed under **AGPL-3.0** (the strongest applicable copyleft). See
[LICENSE](LICENSE).

## Trademark / IP

"MapleStory" and related assets are trademarks and intellectual property of Nexon.
Cronus is an unofficial, fan-made emulator for local research and educational use.
It bundles **no** copyrighted game client data (wz/nx assets are git-ignored).
