# EmuClient でクライアントを起動する

改造済み exe（`JMS_v186.1_L.exe`）を使わず、**自分の原本クライアントをそのまま起動して、
接続先だけを実行時に切り替える**方式です。ディスク上に改造 exe が存在しないため、
セキュリティソフトに「再ビルドされたバイナリ」として検出される問題が起きません。

作者は Riremito 氏（このプロジェクトの移植元 JMSv186 サーバーと同じ作者）。

## 構成

| ファイル | 役割 |
|---|---|
| `RunEmu.exe` | ランチャー。`MapleStory.exe` を起動して `EmuLoader.dll` を注入 |
| `EmuLoader.dll` | 各 DLL を適切なタイミングで読み込む。多重起動ロックも解除 |
| `FixThemida.dll` | **Themida 保護の Win10 対応**（メモリ展開前・最初に読む） |
| `LocalHost.dll` | **接続先 IP の書き換え**（メモリ展開前に読む） |
| `EmuMain.dll` | HackShield / MSCRC バイパス（メモリ展開後、最初に読む） |
| `EmuExtra.dll` | その他のメモリ書き換え |

配置先は `Client/MapleStory_v186/`（`MapleStory.exe` と同じフォルダ）。

## 使い方

1. サーバーを起動（`Cronus/run-server.bat`）
2. `Client/MapleStory_v186/run-client.bat` をダブルクリック（UAC の確認が出ます）
3. ログイン画面が出れば成功（ID/パスワードは任意 — 自動登録）

### 管理者権限が必須です

`MapleStory.exe.manifest` が `requestedExecutionLevel level="requireAdministrator"`
を宣言しているため、クライアントは**管理者権限でしか起動できません**。
`RunEmu` は `CreateProcessW` で対象を起動しますが、この API は昇格を伴う起動が
できないため、**通常権限で `RunEmu.exe` を実行すると何も起こらずに終了します**。

`run-client.bat` は自分自身を昇格させてから `RunEmu.exe` を呼ぶので、
これを使えば問題ありません（`RunEmu.exe` を右クリック →「管理者として実行」でも可）。

また、**古い `MapleStory.exe` プロセスが残っていると起動に失敗します**。
`run-client.bat` は残留プロセスを検出したら警告します。

多重起動ロックが解除されるので、**同じ PC で複数クライアントを同時起動できます**
（2 人プレイの動作確認に便利）。

## 設定

### `LocalHost.ini` — 接続先

```ini
[LocalHost]
ServerIP=127.0.0.1     ; 接続先。リモートなら公開 IP を書く
AuthHook=0             ; JMS は 0 のまま（他リージョンは 1）
FixedPortNumber=0      ; 0 = 無効
```

> **重要: `ServerIP` はドット区切りの IPv4 のみ。ドメイン名は使えません。**
> 実装が `swscanf_s(L"%d.%d.%d.%d")` で数値 4 つとして読むためです。
> ドメインを運用している場合も、ここには解決後の IP を書く必要があります。

`LocalHost.dll` は**すべての接続先を `ServerIP` に書き換えます**（ポート 80/443 は遮断）。
つまりログインもチャンネルもポイントショップも一括で飛ばされるため、
**サーバー側の `CRONUS_HOST` が何であっても、クライアントは `ServerIP` に繋ぎます。**
リモート参加者は `ServerIP` にサーバーの公開 IP を入れるだけで済みます。

### `RunEmu.ini` — 起動対象と読み込む DLL

```ini
[RunEmu]
TargetEXE=<MapleStory.exe のフルパス>
CmdLine=
LoaderDLL=EmuLoader.dll

[FastLoad]                 ; メモリ展開前（DLL_1..DLL_10）
DLL_1=FixThemida.dll
DLL_2=LocalHost.dll

[DelayLoad]                ; メモリ展開後（DLL_1..DLL_10）
DLL_1=EmuMain.dll
DLL_2=EmuExtra.dll
```

**順序が重要です。** `FixThemida.dll` → `LocalHost.dll` が FastLoad（展開前）、
`EmuMain.dll` が DelayLoad の先頭（MSCRC バイパスを最初に効かせるため）。

> RunEmu は初回起動時に `DLL_2=` 〜 `DLL_10=` の空行を自動追記します。
> あとから項目を足すときは**キーが重複しないよう**注意してください。

## Themida の壁

原本 `MapleStory.exe` は **Themida でパックされています**。古い Themida は
Windows 10 上でシステム DLL のヘッダを誤読し、

> A monitor program has been found running in your system.

というダイアログを出して起動を拒否することがあります（監視ツールが実際に動いて
いなくても発生します）。

`FixThemida.dll` はこれの対処で、メモリ上の `kernel32.dll` / `user32.dll` の
セクションヘッダの `PointerToRawData` を `VirtualAddress` に書き換え、古い Themida の
想定（ファイルオフセット = RVA）に合わせます。メモリ展開**前**に読ませる必要があるため
FastLoad の先頭に置きます。

**それでも解消しない場合の切り分け:** `RunEmu.exe` を使わず `MapleStory.exe` を
直接起動してみてください。同じダイアログが出るなら EmuClient は無関係で、
Themida と実行環境の相性問題です。その場合は、Themida ごと取り除いてある
配布版 `JMS_v186.1_L.exe`（Riremito 氏がまさにこの問題を回避するために作ったもの）を
使うのが現実的です。

## WZ パッチとの関係

`NameSpace.dll` の 2 バイトパッチ（マップ入場時のクラッシュ対策）は**この方式でも必要**です。
同じフォルダの DLL を読むため、適用済みならそのまま効きます。

## ソースからのビルド手順

配布バイナリは無いため、ソースからビルドします（Visual Studio + C++ ワークロード）。

```bash
# 1. 3 リポジトリを取得（DevTools/riremito/ に配置）
git clone --recursive https://github.com/Riremito/EmuClient.git
git clone --recursive https://github.com/Riremito/tools.git
git clone --recursive https://github.com/Riremito/LocalHost.git

# 2. 依存ライブラリを先にビルド（tools/Simple → tools/Hook の順）
#    Release|x86。プロジェクトは古い SDK/ツールセットを指しているので上書き指定する:
#      -p:WindowsTargetPlatformVersion=<インストール済み SDK> -p:PlatformToolset=<現行>

# 3. tools/Simple/Share/Simple と tools/Hook/Share/Hook を
#    EmuClient/Share/ と LocalHost/Share/ にコピー
#    （付属の CopyLib.bat / GetLib.bat は相対パス前提なので手動コピーが確実）

# 4. EmuClient.sln と LocalHost.sln を Release|x86 でビルド
```

生成物は各リポジトリの `Release/` に出ます。

## セキュリティソフトについて

`RunEmu.exe` は DLL インジェクターなので、**この方式でも検出される可能性はあります**
（判定名は `HackTool:Win32/Injector` 系）。改造 exe とは検出のされ方が変わるだけで、
ゼロにはなりません。検出された場合の対処は改造 exe の場合と同じです。
