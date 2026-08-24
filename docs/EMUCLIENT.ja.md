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
| `LocalHost.dll` | **接続先 IP の書き換え**（メモリ展開前に読む必要あり） |
| `EmuMain.dll` | HackShield / MSCRC バイパス（メモリ展開後、最初に読む） |
| `EmuExtra.dll` | その他のメモリ書き換え |

配置先は `Client/MapleStory_v186/`（`MapleStory.exe` と同じフォルダ）。

## 使い方

1. サーバーを起動（`Cronus/run-server.bat`）
2. `Client/MapleStory_v186/RunEmu.exe` をダブルクリック
3. ログイン画面が出れば成功（ID/パスワードは任意 — 自動登録）

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
DLL_1=LocalHost.dll

[DelayLoad]                ; メモリ展開後（DLL_1..DLL_10）
DLL_1=EmuMain.dll
DLL_2=EmuExtra.dll
```

**順序が重要です。** `LocalHost.dll` は FastLoad（展開前）、`EmuMain.dll` は
DelayLoad の先頭（MSCRC バイパスを最初に効かせるため）。

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
