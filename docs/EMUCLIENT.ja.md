## Themida の壁（原本を使う場合）

原本 `MapleStory.exe` は Themida でパックされており、この環境では

> A monitor program has been found running in your system.

を出して起動を拒否します。**EmuClient を介さない単体起動でも同じ**なので、
Themida と実行環境の相性問題です（監視ツールは 1 つも動いていない状態で確認）。

`FixThemida.dll` も同梱してビルドしてありますが、これは作者のコメントによれば
**「Win10 で古い Themida が kernel32/user32 の EAT を誤って解決してクラッシュする」
問題への対処で、対象は JMS v176 以下**です。v186 のこのダイアログには効きません
（実際に FastLoad に入れて試し、変化なしを確認）。

アンパック済みの `JMS_v186.1_L.exe` には Themida 自体が存在しないため、
この問題は起きません。

# EmuClient でクライアントを起動する

クライアントを起動し、**接続先を実行時に書き換える**ツール群です。
作者は Riremito 氏（このプロジェクトの移植元 JMSv186 サーバーと同じ作者）。

## この環境での結論（2026-08-24 実測）

| 対象 | 結果 |
|---|---|
| 原本 `MapleStory.exe` | ❌ **Themida に阻まれて起動不可**（EmuClient 抜きの単体起動でも同じ） |
| アンパック済み `JMS_v186.1_L.exe` | ✅ **EmuClient 経由で正常動作**（ゲーム内まで到達を確認） |

**原本は EmuClient とは無関係に起動できません。** `MapleStory.exe` を直接
ダブルクリックしても `A monitor program has been found running in your system.`
のダイアログが出ます（監視ツールは一切動いていない状態で確認済み）。
Riremito 氏がアンパック版を用意しているのは、まさにこの壁を越えるためです。

したがって **TargetEXE にはアンパック版を指定します**。

### それでも EmuClient を使う利点

アンパック版は接続先が `127.0.0.1` にハードコードされていますが、
`LocalHost.dll` が**全接続の宛先を書き換える**ため、**exe を作り直さずに
接続先を変えられます**。参加者に配るときは `LocalHost.ini` の 1 行だけ
書き換えてもらえば済みます。

```ini
ServerIP=203.0.113.9    ; 各参加者はここだけ変更
```

さらに `EmuLoader` が多重起動ロックを解除するので、**1 台で複数クライアント**を
起動できます（2 人プレイの動作確認に便利）。

## 構成

| ファイル | 役割 |
|---|---|
| `RunEmu.exe` | ランチャー。`MapleStory.exe` を起動して `EmuLoader.dll` を注入 |
| `EmuLoader.dll` | 各 DLL を適切なタイミングで読み込む。多重起動ロックも解除 |
| `LocalHost.dll` | **接続先 IP の書き換え**（メモリ展開前に読む） |
| `EmuMain.dll` | HackShield / MSCRC バイパス（メモリ展開後、最初に読む） |
| `EmuExtra.dll` | その他のメモリ書き換え |

配置先は `Client/MapleStory_v186/`（`MapleStory.exe` と同じフォルダ）。

## 使い方

1. サーバーを起動（`Cronus/run-server.bat`）
2. `Client/MapleStory_v186/run-client.bat` をダブルクリック（UAC の確認が出ます）
3. ログイン画面が出れば成功（ID/パスワードは任意 — 自動登録）

### 管理者権限について

クライアントは管理者権限を要求しますが（`MapleStory.exe.manifest`）、
**`RunEmu.exe` 自身のマニフェストも `requireAdministrator` を宣言している**ため、
ダブルクリックすれば自動で昇格します。`run-client.bat` は残留プロセスの警告も
出すので便利ですが、必須ではありません。

また、**古い `MapleStory.exe` プロセスが残っていると起動に失敗します**。
`run-client.bat` は残留プロセスを検出したら警告します。

### ini は必ず CRLF で保存する

`Config` は `GetPrivateProfileStringW` で ini を読みます。この API は
**CRLF 改行を前提**としており、LF only のファイルではキーを読み取れません。
その場合 `TargetEXE` が空扱いになり、**何も起動せずに終了**します
（症状としては「ダブルクリックしても無反応」に見えます）。

Linux 系のツールやエディタで編集した場合は改行コードに注意してください。
正しく読めているかは PowerShell で確認できます:

```powershell
Add-Type -Name I -Namespace W -MemberDefinition '[DllImport("kernel32.dll")] public static extern int GetPrivateProfileStringW(string s,string k,string d,System.Text.StringBuilder r,int n,string f);'
$b = New-Object System.Text.StringBuilder 512
[W.I]::GetPrivateProfileStringW('RunEmu','TargetEXE','',$b,512,"$PWD\RunEmu.ini"); $b.ToString()
```

### うまくいかないときのログ取得

EmuClient の各 DLL は `OutputDebugString` でデバッグ出力を出します。
同梱の `debug-log.ps1` を**管理者 PowerShell で実行したまま** `run-client.bat` を
起動すると、どの DLL がどこまで進んだかが見えます。

```powershell
powershell -ExecutionPolicy Bypass -File debug-log.ps1
```

`FastLoad:...` / `DelayLoad:...` / `[Redirect]...` といった行が出れば、
その段階までは正常に動いています。

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

**順序が重要です。** `LocalHost.dll` が FastLoad（展開前）、
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
