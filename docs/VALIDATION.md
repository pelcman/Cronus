# Validating Cronus against a real JMS v186 client

Cronus' unit and integration tests prove **internal consistency** (a Cronus client session
round-trips with a Cronus server session, byte for byte). They do **not** yet prove
**fidelity** to the real JMS v186 client — that the exact bytes Cronus emits are what the
Nexon client expects. This guide is the workflow to close that gap.

> This is the project's top open risk. Every packet below is a reverse-engineered hypothesis
> from [Riremito/JMSv186](https://github.com/Riremito/JMSv186); confirm each against a real
> client before trusting it.

## Tools

- **JMS v186 client** — the Nexon client binary for this version.
- **[EmuClient](https://github.com/Riremito/EmuClient)** — injects into the client to redirect
  it to `127.0.0.1` and bypass the CRC/version checks. Verified on JMS v164/165/186/188/194.
- **[RirePE](https://github.com/Riremito/RirePE)** — a packet editor/logger. It shows every
  packet the client sends and receives, decrypted, with the opcode and hex payload.
- **The Java oracle** — build and run JMSv186 itself. Because it is known to work with the
  real client, its packets are the ground truth. Diff Cronus' bytes against it.

## Workflow

1. **Capture the oracle.** Run the Java JMSv186 server, connect the real client through
   EmuClient, and use RirePE to log the full session: handshake → login → world/char select →
   game entry → a few moves, a chat, an NPC talk. Save the hex dumps per opcode.
2. **Capture Cronus.** Run `dotnet run --project src/Cronus.Server.Host`, connect the same
   client, and log the same steps with RirePE.
3. **Diff per opcode.** For each server→client packet, compare Cronus' bytes to the oracle's.
   The first differing byte points at the field to fix. Dynamic fields (timestamps, random
   IVs, object ids) will differ legitimately — compare structure, not those values.
4. **Pin a golden vector.** Once a packet matches, capture the oracle's bytes into a unit test
   as a fixed expected buffer (with dynamic fields zeroed/masked), so regressions are caught.
   This is the "golden vector" TODO referenced throughout the code and `AGENTS.md`.

## Handshake & crypto (verify first — everything depends on it)

Get the client past the login screen and you have validated the whole network core.

- Server sends a **plaintext Hello**: `[size:2 LE][version:2 LE][subVer:str][recvIv:4][sendIv:4][region:1]`.
  Region byte = `3` (JMS). Implemented in `Cronus.Network.Handshake`.
- Then framed packets: `[header:4][encrypted body]`. AES-OFB with the JMS default key; the
  4-byte header carries the length XOR'd with the IV/version marker. Implemented in
  `Cronus.Network.Crypto.AesOfbCipher`.
- If the client shows the ID/PW screen and accepts a login, the handshake, crypto, framing,
  opcode table, and `LP_CheckPasswordResult` are all correct.

## Implemented packet checklist

Server → client (build these; verify their bytes):

| Packet | Code | Milestone |
|---|---|---|
| Hello (handshake) | `Handshake.BuildHello` | M2 |
| `LP_CheckPasswordResult` | `LoginPackets.CheckPassword*` | M3 |
| `LP_WorldInformation` (+terminator) | `LoginPackets.WorldInformation` / `WorldListEnd` | M4 |
| `LP_RecommendWorldMessage` / `LP_LatestConnectedWorld` | `LoginPackets` | M4 |
| `LP_SelectWorldResult` | `LoginPackets.SelectWorld*` | M4 |
| `LP_ViewAllCharResult` | `LoginPackets.ViewAllChar*` | M4 |
| `LP_CheckDuplicatedIDResult` | `LoginPackets.CheckDuplicatedIdResult` | M5 |
| `LP_CreateNewCharacterResult` | `LoginPackets.CreateNewCharacter*` | M5 |
| `LP_SelectCharacterResult` (migrate) | `LoginPackets.SelectCharacterResult` | M4 |
| GW_CharacterStat / AvatarLook | `Cronus.Server.Login.CharacterEncoder` | M5 |
| `LP_SetField` (enter game / map change) | `ChannelPackets.SetField*` + `CharacterDataEncoder` | M6/M7 |
| `LP_UserEnterField` / `LP_UserLeaveField` | `ChannelPackets` | M7 |
| `LP_UserMove` / `LP_UserChat` | `ChannelPackets` | M7 |
| `LP_TransferFieldReqIgnored` | `ChannelPackets` | M7 |
| `LP_NpcEnterField` / `LP_MobEnterField` | `ChannelPackets` | M8 |
| `LP_ScriptMessage` (NPC dialog) | `ChannelPackets.ScriptMessage` | M8 |

Client → server (parse these; verify field offsets):

| Packet | Handler |
|---|---|
| `CP_CheckPassword` | `LoginHandler` |
| `CP_WorldInfoRequest` / `CP_SelectWorld` / `CP_ViewAllChar` | `LoginHandler` |
| `CP_CheckDuplicatedID` / `CP_CreateNewCharacter` / `CP_SelectCharacter` | `LoginHandler` |
| `CP_MigrateIn` | `ChannelHandler` |
| `CP_UserMove` / `CP_UserChat` | `ChannelHandler` |
| `CP_UserTransferFieldRequest` | `ChannelHandler` |
| `CP_UserSelectNpc` / `CP_UserScriptMessageAnswer` | `ChannelHandler` |

## Known gaps (expected to need real-client work)

- **CharacterData blob** (`CharacterDataEncoder`): the largest, most conditional structure.
  Empty inventories/skills/quests are encoded; the exact ordering for JMS v186 is the most
  likely place for a first-byte mismatch.
- **Combat** (`CP_UserMeleeAttack` etc.): not implemented — the attack packet is variadic and
  version-sensitive; implement it against a live capture, not from the source alone.
- **Items / equipment**: characters render without visible equips until item serialization
  (`DataGW_ItemSlotBase`) lands.
