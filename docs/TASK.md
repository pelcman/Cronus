# TASK.md — 当時の MapleStory(JMS v186)を全部作り直すまでの作業フロー

> **これがマスタープラン(2026-09-08 改訂)。** 作業はこのファイルのチェックリストに沿って進め、
> 完了のたびにチェックを更新してコミットに含める。経緯・履歴は [AGENTS.md](../AGENTS.md)
> (時系列ログ)、NPC個別の進捗は [NPC_COVERAGE.md](NPC_COVERAGE.md)(生成物)に置く。
> 旧計画(2026-08-21〜09-08「身内サーバー完成版」)は git 履歴(`INITIAL COMMIT 2` 以前の docs/TASK.md)にある。
> このファイルは「今なにをやるべきか」だけを持つ。

## 目標(2026-09-08 転換)

**Cosmic・JMSv186・Maple2 のいいとこどりをした、セットアップから保守運用までが簡単で、
再現度が最も高いサーバー/クライアントを作る。**

| 取り入れる相手 | 何を取るか | 何を取らないか |
|---|---|---|
| **Riremito/JMSv186**(Java, JMS v186) | パケットのバイト配置・送信タイミング・enum/効果ID(**唯一のオラクル**)、JMS 原文スクリプト | v131〜302 の多バージョン同居、MINA |
| **P0nk/Cosmic**(Java, GMS v83, `Reference/Cosmic`) | コンテンツの網羅(PQ・イベント・エリアボス・クエスト・リアクター・NPC の流れ)、設定ファイル1枚+handbook の運用の簡単さ、feature_list の粒度 | パケット・opcode・暗号(v83≠v186)、GMS 固有の ID/マップ |
| **MS2Community/Maple2**(C#, `Reference/Maple2`) | レイヤ分離・DI・EF Core・サービス構成、**Login / World / Game のプロセス分割と gRPC 連携**、1 bat で全プロセス起動(`start.bat`) | コードそのもの(MS2 用)、Web サーバー(v186 クライアントには不要) |

**転換の理由**: 公開IPで外部プレイヤーと遊べるところまでは 2026-09-08 に確認できたが、
多くのコンテンツでクライアントがクラッシュした。「最小限の遊べる」は基準として低すぎた。
基準は「v186 クライアントが持つ全コンテンツ」に上げる。

**「完成」の定義**:
1. v186 クライアントが持つ全コンテンツ(マップ・NPC・ポータル・リアクター・クエスト・PQ・ボス・
   イベント・職業/スキル・アイテム・システム)がサーバー側に実装され、リファレンスと同じ挙動をする。
2. **どの操作でもクライアントが落ちない。** 未実装のものは必ず `[DEV]` 付きの文言で明示され、
   無害に終わる(沈黙・クラッシュにしない)。
3. セットアップ(`setup.bat` → `run-server.bat` → `port_open.bat`)、バックアップ、更新が
   ドキュメントだけで誰でもできる。

---

## 0. 軸 — 進め方の方法論(全フェーズ共通)

過去の重大バグ(カード効果ID・クエスト完了E3・攻撃ミラーのnSkillID・クライアントフォルダ取り違え)の
反省から、実装は必ずこの順で行う:

1. **逐語移植** — パケットの中身・送信タイミングは自作しない。JMSv186 の該当箇所
   (`ReqC*` → 送信呼び出し)を読み、呼び出し構造ごと移植する。
2. **契約監査** — 「リファレンスが送らない場面で送っていないか / 送る場面で送り忘れていないか」を
   机上で突き合わせる。
3. **差分検証** — 解析で決まらないものだけ検証に回す。ラウンドトリップテスト(自動)、
   golden vector(`CRONUS_BOT_CAPTURE` でJavaオラクルと比較)、実機二分探索(最終手段)。
4. **実機確認** — クライアント状態を壊しうる変更(ダイアログ・クエスト・インベントリ・
   フィールド遷移・エフェクト)は実クライアントで一度は踏む。**起動しているクライアントのフォルダを
   `RunEmu.ini` で確かめてから**(2026-09-07 の教訓)。

**リファレンスの使い分け(改訂)**:

| 質問 | 使うもの |
|---|---|
| パケットのバイト配置は? | JMSv186 `ResC*`/`Data*`(逐語移植) |
| いつ・何を送る? | JMSv186 `ReqC*` ハンドラの呼び出し構造 |
| enum/効果IDの値は? | JMSv186 `Ops*` の `init()` の JMS186 分岐。**無ければ疑う** |
| データの意味は? | gamedata.db(=クライアントと同一)+ Java側 `Wz*` ローダ |
| **コンテンツの流れは?**(PQ の手順、イベント、エリアボス、クエストの条件分岐、リアクターの反応、NPC の会話構造) | **Cosmic** `scripts/{event,npc,quest,reactor,portal}`・`docs/feature_list.md`・`handbook/`。**ID・マップ・数値は必ず v186 データで確かめて置き換える** |
| Cosmic から絶対に持ってこないもの | パケットのバイト配置・opcode・暗号・v83 固有の ID |
| どうしても決まらない | Javaオラクル起動+bot capture / 実機 `/booktest` 式二分探索 |

**`[DEV]` 規約(2026-09-08〜、必須)**:
- 未実装・簡易実装・創作(オラクルに無い挙動)のコンテンツは、**プレイヤーに見える文言の中に
  その旨と `[DEV]` を必ず含める**。スクリプトは `cm.sendDev(text)`(`[DEV] ` を先頭に付けて表示)、
  C# は `NpcConversation.DevPrefix`。例: `[DEV] 遠征隊システムは未実装（簡易入場）。`
- 料金など数値だけが創作の場合はスクリプト先頭のコメントで足りる。**挙動や進行が本来と違う**場合は
  プレイヤー向け文言にも入れる。
- 実装が完了したら `[DEV]` を外す。これが「完成」の進捗そのものになる。

**クラッシュゼロ契約**: 実装が無い入口(スクリプト無しのポータル・リアクター・クエストスクリプト・
イベント入場・未対応パケット)は、`[DEV]` 文言か無害な拒否応答で必ず止める。クライアントを落とす
沈黙・不正パケットは全てバグとして扱う。

---

## フェーズ0: クラッシュ撲滅(最優先)

外部プレイ(9/7 21:08〜、9/8 12:13〜)で落ちたコンテンツを全て潰す。落ちたら即
`python DevTools/wirelog.py`(切断直前のパケット文脈)。

- [x] **クラッシュ棚卸し(自動): マップ入場は完了**(2026-09-09) — `/sweep maps` で **3,264 マップ全てに入場して
      クライアントが落ちない**ことを確認(TunaYukke、4回の実行、途中のクラッシュ3件を下記のとおり修正)。
      入場時のクラッシュはこれで棚卸し済み。残るクラッシュ源は「操作」(NPC 会話・アイテム使用・スキル・
      クエスト・取引・PQ 進行)で、こちらは `[dev] unhandled` ログと下記の静的検査で追う。
      `DevTools/script_lint.py`(2026-09-09): スクリプト中の `#t/#i/#m/#o/#p/#q` タグと gainItem/warp 等の ID を
      クライアントデータ(String.wz / Map.wz)と照合 → 179 参照、未知 ID なし。あわせて fieldType の分布を出力
      (0: 2390 / 6: 1818 / 14: 486 / 23: 345 / 21: 106 / 35: 79 / 25: 52 / 20: 41 / 11: 39 / 15・16・1014: 30 …)。
      特殊フィールドへの**入場**は全種通過済みだが、その中での**操作**(カーニバル・雪玉・ココナツ・道場等)は未実装。
      落ちた場所の手動列挙は不要(ユーザー談)。**無人化**: `DevTools/crash_harness.py`
      (実クライアント起動→ログイン→`/sweep`→死活監視→記録→再起動→`resume`)。RunAsInvoker で UAC 回避・
      強制終了可を確認、ログイン画面の座標は校正済み。**残: ワールド/キャラ選択の座標校正**(stage1/stage2)。
      スイープ中に切断されたキャラは救済マップに保存し `# crash? <map>` を progress に記録(実装済み)。
- [x] **操作の自動検証(bot / ヘッドレス)**(2026-09-09) — 「膨大すぎる」(ユーザー談)ため人手の操作は前提にしない。
      3層: ①`AllScriptsExerciseTests` — 全 NPC スクリプト 248 本を実エンジンで実行し、選択肢・はい/いいえ・
      承諾/拒否の全分岐(上限付き)を 3 プロファイル(初心者 Lv10 / 戦士 Lv45 / 全所持 Lv200)で辿り、
      JS 例外・プロンプトのループ・未知 ID(アイテム/マップ/Mob/NPC/スキル、gamedata.db 照合)で失敗。
      クエスト(start/end)・ポータル・リアクターも実行。初回で **INpcPlayer に無い 6 メソッド**
      (warpPortal / airshipBoarding / airshipMinutes / openParcel / parcelCount / receiveParcels)を検出し
      契約に追加。以後は緑(82 秒)。あわせてスクリプト例外を `NpcConversation.Error` と `[script]` ログに
      出す(従来は握りつぶし)。②`AllQuestsExerciseTests` — bot がクライアントデータの全 2,956 クエストに
      受注/完了/放棄を要求し、セッションが生き残ること(サーバー例外なし)を確認(0.7 秒)。
      ③`/sweep npcs` — 実クライアントに全 NPC の会話の**1ページ目**を流して描画クラッシュを棚卸し(キャラへの
      効果は無効)。初版は 2 体目の会話を 1 体目の開いたダイアログの上に送り、クライアントが自ら切断して
      ログイン画面へ戻った(会話中の別会話は DC、この世代のクライアントの既知挙動)。サーバーからページは
      進められないため、1 体ごとに同マップ SetField でダイアログを閉じる方式に変更。深いページは①が担当、
      実機で全ページを辿るには crash_harness のキー入力が必要。
      ④`AllNpcsWireExerciseTests`(2026-09-09、ユーザーの指摘「ワープ・クエスト受注・アイテム移動が起きる会話は
      考慮されているか」への回答) — bot が `/talk <npc>` で全 248 NPC の会話を**本物のサーバー経路**で開き、
      分岐(NPC ごと上限 6 経路)を辿る。マップ・アイテム・クエストは実データ(gamedata.db)、スクリプトの
      プレイヤーは本物 → ワープ・付与・受注・ショップ開店が実キャラに実際に起き、その応答がワイヤに流れる。
      セッション生存(最後の `/pos` 応答)と `[script]` エラー無しで判定(2 分 12 秒、緑)。
      これで「操作」の自動検証は①ロジック ②クエスト ③描画 ④実効果 の 4 層。
      **③完了(2026-09-09 02:31)**: スクリプトのある全 243 NPC の会話 1 ページ目を実クライアントで描画、
      クラッシュは #4(画像の無い GMS ID、修正済み)のみ。フェーズ0の自動棚卸しは「マップ入場」「NPC 会話」
      「全クエスト」「全スクリプト分岐」「未対応パケット」まで完了。残るクラッシュ源は実装が無い機能の
      入口(フェーズ1で `[DEV]` 化)と、PQ/ボス/特殊フィールド内の操作(フェーズ4)。
- [x] **クラッシュ #1: 910320100 ホコリだらけのプラットフォーム(地下鉄殲滅 1ステージ)**(2026-09-08) —
      初回スイープ(TunaYukke、2171/3264 マップ通過)で唯一落ちた地点。入場3秒後に切断。原因はマップの
      `info/fieldType 23`(Massacre 特殊フィールド): クライアントのゲージ UI が入場時に受け取るはずの
      パケット(カウントダウン Clock、`killing/first/*` 画面エフェクト、`massacre_*` セッション値6つ、
      ゲージ)を Cronus が一切送っていなかった。oracle `Event_PyramidSubway` を `MassacreEvent` として移植
      (下記フェーズ4)。MapData に `FieldType` / `OnUserEnter` / `OnFirstUserEnter` / `ForcedReturn` を追加。
      **他の fieldType≠0 のマップも同種の危険がある** → スイープ再開で洗う。
- [x] **クラッシュ #2: 910330200(同・91033 系の殲滅ステージ)**(2026-09-08, 再開スイープ) — 結果マップ 910330001 を
      通過した時点でイベントが終了(成功扱い)し、終了済みのまま握っていたため次の fieldType 23 マップで
      新しいイベントを作らず、パケットが出ずに落ちた。oracle は終了時に null に戻して `Massacre_first` で
      毎回作り直す → 同じ形に修正(終了済みなら作り直す)。91033 系は地下鉄扱い(`TypeOf`)。
- [x] **クラッシュ #3: 913020000 隠れ道: 第4訓練場**(2026-09-09, 3回目のスイープ、42 マップ通過後) — 入場1秒後に
      切断。マップの life は `mobTime -1` のボス 9300291 1体だけで、直前の第3訓練場(9300290、同じ mobTime -1)
      は通過。送信内容は SetField / MobEnterField / MobChangeController のみで、テンプレート ID 以外は同一
      → クライアント側でこの mob を実体化すると落ちる(9300291 は `attack3/info` に disease 127 Lv6、
      `info/default` キャンバスを持つ)。**契約監査の結果**: oracle は `mobTime < 0` の life を
      `SpawnPoint.shouldSpawn()==false` で一切湧かせない(スクリプト/イベントが湧かせる)。Cronus は
      マップ生成時に湧かせていた → 湧かせないよう修正。9300291 自体の実体化クラッシュは、訓練場の
      チュートリアルを実装する時に再確認(スクリプト湧きで再現するなら client 側データの問題)。
- [x] **クラッシュ #4: NPC 2151003 の会話(`/sweep npcs` 188 体目)**(2026-09-09) — error 0x80030002
      (STG_E_FILENOTFOUND)。2151003〜2151007(ミハエル〜ホークアイ)は JMS の String.wz に名前だけあり
      Npc.wz に画像が無い GMS 側の ID。JMS の転職官は 1101003〜1101007(画像・スクリプトあり)なので
      重複スクリプト 5 本を削除。再発防止: `INpcNameProvider.HasImage`(`Npc/{id:D7}.img`)を追加し、
      `/sweep npcs` はスキップ、`/talk` は `[DEV]` で拒否、`AllScriptsExerciseTests` は画像の無い NPC 用
      スクリプトを失敗として検出。187 体分の会話描画は通過。
- [x] **未対応パケットの安全化**(2026-09-08) — クライアントが送る 219 opcode のうち **132** をサーバーは
      黙って捨てていた(if/else 連鎖に default 無し)。`HandleUnhandledAsync` を追加: `*UseRequest` と
      修理/製作/ガシャポン等はインベントリのロック解除(空 InventoryOperation)、マップ転送系は
      TransferFieldReqIgnored、プレイヤー操作には `[DEV] この操作はまだ実装されていません（CP_…）` を
      セッション内1回表示、全件を `[dev] unhandled` としてログ。未対応一覧はログから積み上がる。
- [x] **DB 楽観的並行性例外で切断**(2026-09-09 修正) — 9/7 の外部プレイで拾得直後に
      `expected to affect 1 row(s), but actually affected 0 row(s)` → セッション切断。原因は自動保存 tick と
      セッション自身が同じ Character を別コンテキストで同時保存し、片方が削除したアイテム行をもう片方が
      更新しようとしたこと。`DbCharacterRepository.Save` をキャラ単位で直列化し、それでも起きた
      `DbUpdateConcurrencyException` はメモリ側を正として再挿入/切り離して再保存(テスト1件)。
- [ ] **未実装の入口を全て安全化** — wz が指すのにスクリプトの無いポータル/リアクター/クエスト
      スクリプト/イベント NPC を機械的に列挙し、`[DEV]` 応答で止める(フェーズ1の適用と同時)。
- [ ] **未対応パケット 132 件の実装** — `[dev] unhandled` ログに出た順に、契約監査(JMSv186 `ReqC*`)
      を踏んで本実装する。当面の安全化(ロック解除+`[DEV]`)は上の項目で済み。
- [ ] **クラッシュ報告テンプレ** — [CLIENT_TEST_CHECKLIST.ja.md](CLIENT_TEST_CHECKLIST.ja.md) に
      「落ちたら: 時刻・マップ・直前の操作」を追記。
- [ ] 完了基準: **外部テスター2名×2時間でクライアントクラッシュ0**。

## フェーズ1: `[DEV]` 規約の全面適用

- [x] 汎用フォールバック行(スクリプト無しNPC)を `[DEV] このNPCはまだ実装されていません` に変更、
      `cm.sendDev()` を追加(2026-09-08)。
- [x] 既存の「準備中/改装中/簡易入場」文言7件を `[DEV]` 化(2026-09-08: 林次長・レナリウ・
      ザクム案内・イルカ・ホーンテイルの道標・遠征隊の標識・武陵道場掲示板)。
- [ ] 「創作」「簡易」のスクリプト(コメント上 36+23 件)を棚卸しし、**挙動が本来と違うもの**は
      プレイヤー向け文言にも `[DEV]` を入れる(料金だけ創作のものは対象外)。
- [ ] 未実装の機能に触れる応答(ギルドBBS・アライアンス・道場・遠征隊・PQ 入口)に `[DEV]`。
- [ ] `npc_coverage.py` に「`[DEV]` 表示あり」の列を足し、`[DEV]` 残数を進捗指標にする。

## フェーズ1b: プロセス分割(Maple2 型の基盤、フェーズ0の棚卸し後に着手)

Maple2 は `start.bat` 1つで **World / Login / Web / Game** を別プロセスとして起動する
(Windows Terminal の `wt ... ; nt ...` でタブ分割、無ければ `start` で別窓)。World が
ワールド共通の状態(パーティ・ギルド・バディ・チャット経路・プレイヤー所在・ワールドボス湧き・
グローバルポータル=イベント・ログイン→ゲームの移送トークン・ロック)を gRPC(`Maple2.Server.Core/proto`:
`world.proto` / `channel.proto` / `login.proto` / `global.proto`)で持ち、Game は各チャンネルを World に
登録し(`ChannelClientLookup`)、World からの呼び戻し(`channel.proto`)を受ける。Login は World に
ハートビートを送り、チャンネル一覧と移送トークンを World から得る。

Cronus は 2026-09-09 まで `Cronus.Server.Host` 1プロセスに Login + N チャンネル + キャッシュショップ + 全 tick
(Mob 湧き・回復・バフ期限・飛行船)を載せ、パーティ/ギルド/メッセンジャー等はプロセス内の
レジストリを共有している。同じ形に寄せる:

| プロセス | 役割 | Cronus での中身 |
|---|---|---|
| **Login** | 認証・キャラ選択・チャンネル案内 | `Cronus.Server.Login`(既存)を単独ホスト化。チャンネル一覧と移送トークンは World から |
| **World** | ワールド共通イベントと共有状態 | 新規 `Cronus.Server.World`: 飛行船運航(`AirshipService` の時刻線)、エリア/ワールドボス湧きタイマー、パーティ・ギルド・バディ・メッセンジャー・`/find`・拡声器・お知らせの経路、プレイヤー所在、チャンネル登録・ポート割当、移送トークン |
| **Game** | チャンネル(フィールド・戦闘・NPC・PQ インスタンス) | `Cronus.Server.Channel` + `Cronus.Server.Game`。段階 A は 1 プロセスで N チャンネル、段階 B で 1 プロセス 1 チャンネル。キャッシュショップは Game に同居 |
| (Web) | MS2 のクライアント用 HTTP | **不要**(v186 クライアントは HTTP を使わない) |

**Maple2 → Cronus のプロジェクト対応(倣う部分と、MS2 固有で捨てる部分)**:

| Maple2 | 役割 | Cronus(既存 → 目標) |
|---|---|---|
| `Maple2.Server.Core` | Session(Pipelines)・`PacketRouter`(`PacketHandler<T>` を DI で集めて opcode 表に)・gRPC proto・DI モジュール | `Cronus.Network` + 新設 `Cronus.Server.Core`(proto・共通ハンドラ基底・DI モジュール)。`ChannelHandler.*` の巨大 partial を opcode ごとの `PacketHandler<ChannelSession>` に分割 |
| `Maple2.Server.World` | 共有状態の Lookup/Manager(Party/Guild/Buddy/GroupChat/PlayerInfo/GlobalPortal/WorldBoss)+ `WorldService`(gRPC) | 新設 `Cronus.Server.World` |
| `Maple2.Server.Login` | 認証・キャラ選択、World とハートビート | `Cronus.Server.Login` を単独ホスト化 |
| `Maple2.Server.Game` | `GameServer`+`Manager/*`(Buff/Quest/Skill/Shop/Trade/Party…)+`Session`+`PacketHandlers` | `Cronus.Server.Channel`(セッション/ハンドラ)+ `Cronus.Server.Game`(Manager 群) |
| `Maple2.Model` | Enum/Game/Metadata/Error/Validators | `Cronus.Domain` + `Cronus.Common` |
| `Maple2.Database` | EF Core コンテキスト・Migrations・Storage | `Cronus.Database` |
| `Maple2.File.Ingest` | クライアントデータ → DB 取り込み | `Cronus.Ingest`(gamedata.db、既に同型) |
| `Maple2.Server.DebugGame` | デバッグ用クライアント | `Cronus.Debug.Bot` |
| `Maple2.Server.Tests` | テスト | `tests/*` |
| `appsettings.json` + Serilog + Autofac | 設定・ログ・DI | `.env` → `appsettings.json`(+ `.env` 上書き)、Serilog、Microsoft DI(Autofac は不要) |
| `Maple2.Server.Web` / `Trigger` / `Navmeshes` / `LuaFunctions` | MS2 固有(HTTP 配信・トリガー・3D ナビ) | **採用しない**(v186 は 2D、HTTP 無し。スクリプトは Jint のまま) |

- [x] **段階 A: プロセス分割**(2026-09-09、`feat/process-split`) — `Cronus.Server.Core`(`proto/world.proto`、
      `WorldState`、`WorldGrpcService`、`GrpcWorldClient`、`ServerBootstrap`)と `Cronus.Server.World`(gRPC ハブ、
      既定 127.0.0.1:8585)を新設し、`Cronus.Server.Login` / `Cronus.Server.Channel` をそれぞれプロセス(exe)化、
      `Cronus.Server.Host` を削除。World が持つもの: チャンネル登録(ID 割当・5 秒ハートビート・15 秒で失効)、
      在席(誰がどのチャンネルか)、移送(`MigrateOut` で記録 → 受け側が `MigrateIn` で照合、30 秒で失効、
      World が送っていないクライアントは切断、二重ログインは古い方を切断)、全体放送(`/notice all` は
      World 経由で全プロセスへ)、飛行船時刻表(World が配り Channel が `AirshipSchedule.Apply`)。
      Channel→World の呼び戻しは Channel 側に gRPC サーバーを立てず `Subscribe` ストリームで受ける。
      World 不達時: Login/Channel は 3 回(15 秒)再試行して終了コード 3、稼働中の不達は「チャンネル無し/移送不可」に
      退化してログ 1 行(復帰も 1 行)。`run-server.bat` は Maple2 の `start.bat` と同じ「ビルド 1 回 → `wt` タブ 3 つ
      (World / Login / Channel)、無ければ `start` 窓」、`stop-server.bat` 追加。テスト: `Cronus.Server.Core.Tests`
      (WorldState 8 件 + Kestrel 実ホストの gRPC 往復 3 件)。World 無しで作った Handler は `LocalWorld`(同じ状態を
      プロセス内で)を持つので既存テストは無変更。残: ボス湧きタイマー(対象がまだ無い)、`_worldFields` の
      「index = channel id」前提(段階 B で World に移す)。
- [ ] **段階 B: 共有レジストリの World 移設** — パーティ・ギルド・バディ・メッセンジャー・`/find`・
      ささやきをプロセス内共有から World の gRPC サービス+Game への呼び戻しへ。これで
      1 Game プロセス=1 チャンネルにでき、チャンネル単位の再起動が可能になる。
- [x] **DB は MySQL 既定**(2026-09-08 決定・実装) — 既定 DB 名 **`Cronus186`**(127.0.0.1:3306、root/root、
      初回起動で作成、テーブルは追加型マイグレーション)。`CRONUS_DB_HOST/PORT/NAME/USER/PASSWORD` で変更、
      `CRONUS_DB=sqlite|memory|<接続文字列>` は代替。MySQL 不達時は黙ってメモリに落ちず終了コード2で停止。
      初回の MySQL 起動時に隣の `cronus.db` を1回だけ取り込み(`DatabaseCopy`、キー保持)`cronus.db.imported` に改名。
      `setup.bat` が接続情報を尋ねて `.env` に記録し、mysql.exe があれば接続確認。
- [x] 完了基準: 1 bat で 3 プロセスが立ち上がり、bot スイート 99 ステップ(チャンネル移動・
      キャッシュショップ往復を含む)がそのまま通る — 2026-09-09 に 99/99(World 経由の移送で `/gender` の
      同一チャンネル再入場も通過)。実機ログインの確認は develop マージ後に依頼。

## フェーズ2: ワールド(マップ・NPC・ポータル・リアクター)を 100% に

[NPC_COVERAGE.md](NPC_COVERAGE.md) 基準(2026-09-07 時点: 1,447体中 800対応 = 55%、none 647)。
`python DevTools/npc_coverage.py` で再生成。

- [~] **現行スクリプトと Cosmic のスクリプトの突き合わせ**(2026-09-09 一覧化完了) — `DevTools/cosmic_gap.py` が
      Cosmic と Cronus を ID/名前で突き合わせ、**JMS v186 クライアントに実在するものだけ**(画像のある NPC・
      Check.img にあるクエスト・マップが使うポータルスクリプト名・マップに置かれたリアクター)に絞った作業表
      [COSMIC_GAP.md](COSMIC_GAP.md)(生成物)を出す。現在値: NPC 418(うち JMS マップに配置 304)、
      クエストスクリプト 220、ポータル 324(+Cosmic にも無い JMS 固有 208)、リアクター 235、
      JMS マップを参照するイベントスクリプト 85。配置マップ数の多い順に並ぶので上から潰す。
      **武陵道場は実装済み**(2026-09-09、`feat/mu-lung-dojo`、実機確認待ち): NPC 2091005 素公パンダ
      (挑戦/ベルト/点数リセット、パーティー挑戦と勲章は [DEV])、ポータル dojang_next/up/exit/tuto、
      1〜38階のソロ進行(インスタンス探索・ボス湧き・制限時間クロック・開始/クリア演出)、修練点数の
      永続化(`MuLungDojo` + `ChannelHandler.Dojo`)。
      **2026-09-09 追加バッチ**(すべて JMS データと Cosmic で突き合わせ、実機確認はキュー #4〜#8):
      タイムロード timeQuest ポータル(16 マップ、関門クエスト 3501〜3522 が JMS と 1:1 一致)、風来坊錬金術師 2040050、
      移送/退出 NPC 5 種(2007/1013001/1063016/2022004/2133002)、シャレニアン GQ 周辺 NPC 5 種(9040004/5/7/11/12)、
      村長タタモ 2081000。見送り: 番人ヴォルフ 1202009(装備中判定が必要)、森のキャンプ 2131xxx(交換先 4310000 が JMS では
      別アイテム「ゴールドピック」= GMS 限定イベント)、月うさぎ 9001102(Cosmic 版は GMS の Gaga/UFO で JMS と別物)。
      **プロトコル TODO**: 数値入力ダイアログ SM_ASKNUMBER(=4) は JMSv186 リファレンスが未実装(`getNPCTalkNum` not coded、
      応答も未デコード)。フレーム [str][int][int][int] は確認済みだが 3 つの int の意味(def/min/max の順)と応答の int は
      GMS 由来で未検証。実機で検証できる段階で `askNumber` を追加し、タタモ等の数量選択式を置き換える。
      **エンジン修正**: `INpcPlayer.warp` を 1 メソッド(`warp(int, int portal = 0)`)に統合(Jint はオーバーロードを
      会話の後に誤解決する)。テスト基底は抽象クラスの仮想メソッドで同じ癖を踏むため具象 `RecordingNpcPlayer` に。
      **クエストスクリプト バッチ**(JMS Check/Act の startscript/endscript 宣言 + Cosmic 実装のある 203 件から、
      自己完結する物を順に。実機確認はキュー #11〜#14): `player.startQuest/completeQuest` を本物の
      forceStart/forceComplete 経路に接続(Act 報酬・日誌パケット・完了演出)。1021 ローザーとリンゴ、噂の真相 2148〜2152、
      砂絵団 2124/2126/2127、沼地小屋 2214/2215(`hourOfDay()` フック、17〜20時)、カニング聞き込み 2216〜2219、リッチの感謝 2228、
      ゾンビキノコの信号体系3 2251、ラクダ 2257/2259/2260。Act が付与する報酬(EXP・メソ・人気度・アイテム)はスクリプトで
      二重に渡さない。見送り: 2238(開始のみ forceStart = データ経路と同じ)、2230(ペット卵/キャッシュ)、2232(ファミリー)、
      2245(イベントマネージャ)、2258(時間制限討伐イベント)。
      **キノコ王国 入口**(2026-09-10、`feat/mushroom-kingdom-entry`、キュー #15): マップ入場スクリプト機構を追加
      (`MapData.OnUserEnter` の名前 → `scripts/map/{name}.js`、ポータルと同じ `start()`+`player`、`ChannelHandler.OnFieldEnteredAsync`
      で実行。JMSv186 `MapScriptMethods` の名前表がそのまま置き場になる)。`TD_MC_title` = temaD/enter/mushCatle(JMS 準拠)、
      `player.showScreenEffect`。`player.setQuestData` はクライアントにも started レコードを送るようにした(Check の infoex 完了条件は
      クライアント側でこの文字列を見る)。クエスト 2300〜2310(教官 11 人、同内容)、2325/2327 ジェイムズ、2338 予備スプレー、
      調査ポータル investigate1/2 → セルフ 1300014(forself)の独り言で 2314/2322/2324 の印。蔓の壁 obstacle(2321 以降で通す)と
      gotocastle(2324 の除去剤使用後に 106020501)は JMS スクリプト本体が参照に無いため Check からの推定 = [DEV]。
      2312〜2324/2326/2328〜2331/2336/2337 はデータ経路。**未着手**: 東の塔以降 = TD_MC_keycheck / TD_MC_bossEnter(東石塔門 1300012) /
      TD_MC_violetaEnter(1300013) / 結婚式場インスタンス(106021500〜 summon_pepeking・pepeking_effect・findvioleta・in_secretroom、
      fieldLimit 271096)と、その先のビオルタ 2332〜2335・2342、TD_MC_Openning/TD_MC_gasi の演出マップ、`onFirstUserEnter`。
      いずれも JMS/Cosmic に本体が無く、ボス戦インスタンスの設計が要る(実機で検証できる段階で)。
      **称号クエスト**(2026-09-10、`feat/quest-title-medals`、キュー #16): 冒険家 29900〜29903(ダリア 9000040、受注で知らせ →
      ダリアで勲章)、シグナス騎士団 29906〜29909、アラン 29924〜29928(その場で勲章+完了)。勲章 ID は JMS Check の「未所持」条件と
      String/Eqp で確認(騎士 1142066〜69 は Cosmic と同じ)。ダリア 9000066 は JMS のマップに未配置(自動開始専用)。
      **マガティア協会/授業・単発報酬**(2026-09-10、`feat/quest-magatia-and-single-rewards`、キュー #17): 3301/3303 は JMS Check[1] に
      残る会話文(韓国語)どおりメソ払い(30,000/50,000; Cosmic の原石方式は GMS)、3305/3306 は受注のみ(材料・料金は Act)、6030〜6032 は
      講義のみ(料金は Act、残高チェックだけ)、2186/3452/3833/3382 は Act が空なので Cosmic の報酬値をスクリプトで付与(全アイテム ID を
      JMS String で確認)、2197 は完了のみ。見送り: 6033/6036(メイカー スキル習得 teachSkill が未実装)、4647(ペットスキル)、3514(バフ元判定)。
      **未対応の機構**: Check の `fieldEnter`(マップ入場での自動開始)はクライアント任せ。サーバー側で必要と分かれば `OnFieldEnteredAsync` に足す。
      残る最上位:
      モンスターカーニバル(シュピゲルマン 2042000〜2042007 + 助手)、月うさぎ 9001102(ヘネシス PQ、19 町)、
      帰還碑/名誉の石碑 9040004/9040005、忍耐の森 1061007、timeQuest(思い出の道 16)、rankRoom/tutorialNPC
      (職業別施設)。実装のたびに再生成して残数を更新(ID・数値は v186 データで置換、`[DEV]` 規約)。
- [ ] **NPC 100%** — none 647 → 0。順序: ビクトリア → オシリア → ジパング/自由市場 → イベント
      NPC(期限切れイベントは `[DEV]` 案内)。会話の流れは JMS 原文(Riremito/jms_scripts)→ Cosmic
      `scripts/npc`(708) の順で参照。
- [ ] **ポータルスクリプト 100%** — wz の全 `script` 名を列挙し、未実装を Cosmic `scripts/portal`
      (458)の流れで実装。
- [ ] **リアクター 100%** — Cosmic `scripts/reactor`(292)を v186 のリアクター ID に写す。
- [ ] **交通** — 飛行船の残り路線(オルビス⇔ルディ/リプレ/アリアント、エレヴ⇔エリニア/オルビス、
      リス港⇔リエン)の実飛行化(`AirshipRoute.All` に路線追加)、地下鉄・船の演出。
      バルログ船画像は Map.wz への移植待ち(`DevTools\wz_graft_airship.bat <元Map.wz>`)。
- [ ] **マップ属性** — `fieldLimit`(Cosmic `docs/fieldlimits.txt`)、`info/swim`・`fly`・
      帰還マップ・時間制限マップの挙動確認。
- [ ] 完了基準: 地域ごとのチェックリスト(町・狩場・ダンジョン)を実機で全通し。

## フェーズ3: クエスト 100%

- [ ] **データ駆動クエストの網羅性監査** — Quest.wz の Check/Act 全項目(前提クエスト・
      レベル/職業・アイテム・Mob 討伐・時間・報酬の全型)を実装表にして未対応を潰す。
- [ ] **スクリプト型クエスト** — Cosmic `scripts/quest`(253)を参考に、v186 の `Quest.wz`
      で script 指定のあるものを実装。
- [ ] **転職クエスト全職** — 1〜4次(Cosmic `3rdJob_*`・`4j*` イベント)、シグナス・アランの
      各段階、エヴァンの成長クエスト。
- [ ] **クエスト付随 UI** — メダル/称号、クエスト帳の全状態、`LP_QuestResult` 全型の契約監査。
- [ ] 完了基準: 主要クエストライン(各町の初心者〜、転職、ボス前提)の実機通し。

## フェーズ4: パーティクエスト・遠征・ボス

FieldSet.img(BMS Server.wz)はオラクル欠落。**Cosmic の EventManager / EventInstanceManager
(`scripts/event`, 108本)の設計を C# のインスタンス機構として移植**し、その上に各 PQ を載せる。

- [ ] **インスタンス基盤** — 時間制限・専用マップ複製・参加者管理・タイマー表示(`LP_Clock`)・
      失敗/成功の退出。Maple2 型のサービスとして実装。
- [ ] **PQ**(存在は wz で確認して列挙): カニングシティ PQ(999番の客車 — 林次長 1052115 の
      `[DEV]` 案内が入口)、ルディブリアム PQ、オルビス PQ、LMPQ、ヘネシス PQ、アモリア PQ、
      ピラミッド PQ。
- [x] **地下鉄殲滅 / ピラミッド殲滅(Massacre)**(2026-09-08) — oracle `Event_PyramidSubway` の逐語移植
      `MassacreEvent`(`IMassacreHost` 越しにセッション・パーティ・フィールドへ): エネルギーバー(毎秒 −5/−10、
      討伐 +5、ミス −5、0 で失敗)、ステージ計時(地下鉄 180 秒、ピラミッド 240/300 秒)と次ステージへの
      空きインスタンス探索(5面)、結果マップでのランク(討伐数)と経験値表、記録クエスト 7662/7760、
      パケット5種(ClockCountdown / FieldEffectScreen / SessionValue / MassacreIncGauge / MassacreResult)。
      入口 NPC 林次長 1052115 は Cosmic の流れで実装(台詞は創作)。**残**: ピラミッドの入口 NPC、
      スキル使用時の `onSkillUse`(ピラミッドのスキル回数)、イェティ湧きの実機確認、
      勲章 1142141 / クエスト 29931 の v186 データ確認。テスト 11 件。
- [ ] **モンスターカーニバル 1/2**。
- [ ] **遠征隊** — ホーンテイル入口(道標/標識は現在 `[DEV]` 簡易入場)、人数・時間管理。
- [ ] **ボス** — エリアボス群(Cosmic `AreaBoss*`)、ザクム(祭壇の完全フロー)、ホーンテイル、
      パプラトゥス、ピンクビーン、武陵道場(掲示板は現在 `[DEV]`)。
- [ ] 完了基準: 各 PQ/ボスを 2〜3 人のパーティで実機通し。

## フェーズ5: 職業・スキル・戦闘

- [ ] **全職のスキル効果を全件監査** — 冒険家5系統・シグナス5職・アラン・エヴァン
      (データあり・未実装)・デュアルブレイド(v186 データに含まれるか確認)。X値・バフ・
      パッシブ・召喚・乗り物スキルを表にして未対応を潰す。
- [ ] **状態異常(病気)の有効化** — `GameConstants.PlayerDiseasesEnabled=false` で封印中。
      v186 の病気形 `TemporaryStatSet` を実機二分探索で確定。
- [ ] **Mob スキル全種・属性・反射・分裂/召喚・復活**。
- [ ] **マスタリーブック**(現在: wz 最大まで直接) — 本仕様(成功率・回数)へ。
- [ ] **サーバー検証** — ダメージ上限のステ由来計算・攻撃頻度・射程(創作になるため
      `[DEV]` ではなく設定で切替、既定は oracle 同様の無検証)。

## フェーズ6: アイテム・経済・システム

- [ ] ペット(スキル・装備・複数連れ)、乗り物、メイカースキル、ガシャポン実プール
      (BMS Gachapon.img 入手次第)、モンスターブック網羅、結婚式場、家族(Family)、
      キャッシュショップ全品・パッケージ、ドロップ表監査(Cosmic `database` の drop data と
      v186 のクロス確認)、露店/雇用商人の残課題、宅配の受取 UI。

## フェーズ7: ソーシャル

- [ ] ギルド BBS(LP opcode がリファレンスでも未解決 → 実機二分探索)、アライアンス、
      ミニゲーム招待 UI、ランキング、メッセンジャーの残差。

## フェーズ8: 検証基盤(随時)

- [ ] golden vector 拡張(戦闘・ショップ・クエスト・PQ)、Java 差分ハーネス自動化、
      長時間ソーク、bot スイートを PQ/ボスまで拡張。
- [ ] `[DEV]` 残数・NPC カバレッジ・クラッシュ件数をリリースノートに載せる。

## フェーズ9: 運用・デプロイ(維持)

公開IP接続は確認済み(2026-09-08)。以後は「簡単さ」を上げる。**Docker サポートは 2026-09-08 に廃止**
(Dockerfile / compose / .dockerignore を削除。セットアップは bat + `.env` のみ)。

- [ ] 設定ファイル1枚化(Cosmic `config.yaml` 型、`.env` からの移行)、
      バックアップ/復元の実地テスト、更新手順(サーバー停止→更新→起動)の1コマンド化。
- [ ] 非技術者向けスクリーンショット付き手順、第三者による docs だけのセットアップ検証。
- [ ] 運用ダッシュボード(接続数・クラッシュ・`[DEV]` 踏破ログ)。

---

## 完了済み(旧計画からの引継ぎ、詳細は git 履歴の旧 TASK.md)

- パケット契約監査 10系統(修正21件+被弾ミラー移植)、NPC 選択肢 ID 検証、
  移動系 NPC の行き先復元、宅配、飛行船(エリニア⇔オルビス)、Mob 攻撃特殊効果、
  初心者 AP 自動振り、存在しないマップの救済、`/gmmove`(exe パッチ27か所)、
  wz 編集環境(`DevTools/wz`)、wirelog、LAN/公開IP リハーサル、配布手順。
- リポジトリ整理(2026-09-08): Docker 廃止、リポジトリに入っていた編集済み exe の zip
  (`DevTools/edited_exe`)を削除、空の `DevTools/cronus-wz` と 8/22〜9/7 の古いログを削除、
  役目を終えた文書(旧 TASK、入場クラッシュ診断、デプロイリハーサル記録、AGENTS.md の
  マイルストーン/バックログ 780 行)を削除
- MySQL 既定化(2026-09-08): `Cronus186`、SQLite 保存の1回取り込み、setup.bat の接続手順
  (外部プレイの2本 9/7 21:08・9/8 12:13 は棚卸し用に保持)。
- **stable-2(2026-09-09)**: フェーズ0 完了時点の `develop` を `main` へマージ(5b7ce0a)。全マップ・全 NPC
  会話の実機スイープ、ヘッドレスの全スクリプト・全クエスト・全 NPC、bot 99 ステップ、外部プレイヤーとの
  プレイまで確認済み。以後はテスト項目ごとのブランチ運用(進行ルール 7)。

---

## 進行ルール

1. 着手順は **0 → 1 → 1b(プロセス分割) → 2/3 並行 → 4 → 5/6 → 7**。8 は随時、9 は維持。
2. 1コミット=1まとまり。チェック更新はそのコミットに含める。
3. クライアントを落としうる変更は、コミット前にテスト+botスイート、コミット後に
   実機確認を依頼する(「◯◯を踏んでみて」と明示)。
4. **未実装・簡易・創作は `[DEV]` をプレイヤー向け文言に入れる**(数値だけの創作はコメントで可)。
5. リファレンスの優先順位: **JMSv186(バイト・タイミング)→ Cosmic(コンテンツの流れ)→ 創作**。
   Cosmic から写した流れは、ファイル先頭に出典(`Reference/Cosmic/scripts/...`)を書く。
6. 週次目安で `npc_coverage.py` を再生成し、`[DEV]` 残数と none 数を更新する。
8. **実機確認の後回し(2026-09-09〜、運用者の指示)**: 運用者がしばらく実クライアント確認をできない間は、
   テストと bot が緑の機能は実機確認を待たずに develop へマージしてよい。ただし確認すべき項目は
   [CLIENT_VERIFICATION_QUEUE.ja.md](CLIENT_VERIFICATION_QUEUE.ja.md) に運用者宛で積み、**キューが空になるまで
   `main` への `stable-N` タグは切らない**。実装ごとにキューへ追記する。

7. **ブランチ運用(2026-09-09 改訂)**: `main` = 安定版、`develop` = 統合、作業は **テスト項目ごとのブランチ**。
   - 作業単位は「1 つのテスト項目」= 検証できるまとまり(例: `feat/mu-lung-dojo`、`feat/process-split`、
     `fix/npc-image-guard`)。`develop` から `feat/<項目>` / `fix/<項目>` を切り、コミットは全部そこへ、
     push も同名ブランチへ(バックアップ)。
   - 完了条件 = その項目のテストが全部緑: `dotnet test`、bot スイート、クライアントを落としうる変更は
     実機確認(ユーザーに「◯◯を踏んでみて」と依頼し、結果を待つ)。TASK.md のチェック更新も同じブランチで。
   - 緑になったら `develop` へ `git merge --no-ff`(項目の境界を履歴に残す)→ push → 作業ブランチを削除
     (ローカルと origin)。
   - 体系的な作業(フェーズや大きな機能群)が終わり、`develop` が実機で安定して動くことを確認できたら
     `main` へ `--no-ff` でマージし、注釈付きタグ `stable-N`(`stable-1` からの連番、メッセージに確認した内容)
     を付けて `git push origin main --tags`。`main` へ直接コミットしない。
   - 例外: ドキュメントや生成物(COSMIC_GAP.md など)だけの更新はテスト項目にならないので `develop` へ直接
     コミットしてよい。
