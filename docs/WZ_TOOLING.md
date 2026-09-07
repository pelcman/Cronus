# WZ 編集環境(`DevTools/wz.bat`)

クライアントの `.wz` を読み書きするための自前ツールです。外部の GUI ツール(HaRepacker 等)に
依存せず、リポジトリだけで完結します。用途は「別バージョンの wz から画像やアバターを移植する」
「中身を確認する」「移植後の wz が壊れていないことを検証する」の3つです。

実装は `src/Cronus.Data/Wz/`(サーバーの gamedata 取り込みと同じリーダーを共有)、CLI は
`DevTools/WzTool/`(`dotnet run`)です。`DevTools/wz.bat` がラッパーです。

## 検証済みの前提

- JMS v186 の全 `.wz` を `Cronus.Ingest` が読めている(gamedata.db はそこから生成)。
- `wz rewrite` で **Map.wz(598MB、5983 img)を無変更で再構築 → 全 img の解析結果が一致**(0.9秒)。
  未変更の img はバイト列をそのまま複製し、ディレクトリだけを再直列化するので、
  変更していない部分は元と同一です(ファイルは数百バイトだけ大きくなります: チェックサム欄を
  固定幅で書くため。クライアントはチェックサムを見ません)。
- 単体テスト(`tests/Cronus.Data.Tests/WzRoundTripTests.cs`): 全ノード種別の往復、IV 3種
  (none/gms/ems)でのバージョン自動検出、鍵の異なるアーカイブ間の graft、キャンバス復号。

## コマンド

```
DevTools\wz info    <file.wz>                              バージョン・IV・ヘッダ・img 数
DevTools\wz ls      <file.wz> [path]                       ディレクトリ一覧 / img 内のノード一覧
DevTools\wz dump    <file.wz> <image path> [--out x.xml]   img を wz_xml 形式で出力(取り込みと同じ形)
DevTools\wz png     <file.wz> <canvas path> <out.png>      キャンバスを PNG に(目視確認用)
DevTools\wz rewrite <file.wz> --out <new.wz>               無変更で再構築 → 自動 verify(往復チェック)
DevTools\wz graft   <src.wz> <src path> <dst.wz> <dst path> --out <new.wz> [--no-verify]
DevTools\wz verify  <a.wz> <b.wz>                          全 img の解析結果を比較
DevTools\wz set-int <file.wz> <image glob> <node path> <値> --out <new.wz>   一括で int を設定/追加
```

`set-int` はグロブ(`Map/Map*/*.img` など)に合う全 img を解析し、ノードパスの int を書き換え(無ければ
追加)て再構築します。例: 全マップに飛行フラグ `info/fly=1`(`DevTools\wz_enable_fly.bat` の中身)。
実 Map.wz で 5536 img を書き換え(72 枚は元から fly=1)、verify で「変更した img だけが変わっている」ことを確認しています(10秒)。

`path` は `<ディレクトリ>/<名前>.img[/ノード/ノード…]` です。例: `Obj/vehicle.img/ship/ossyria/97`。

`graft` は src のノード(サブツリーごと)を dst の指定位置に置きます(同名があれば置換、無ければ追加)。
文字列は dst の鍵で再符号化、キャンバスのピクセルデータは **平文 zlib** に正規化して格納します
(鍵が違うアーカイブ間でも移植でき、クライアントはどちらの形式も読みます)。書き出し後に元の dst と
全 img を比較し、「変更した img だけが変わっていて他は同一」を確認して `verify: OK` を出します。

## 例1: 飛行船のバルログ船の画像を移植する

JMS v186 の `Map.wz/Obj/vehicle.img/ship/ossyria/97`(飛行マップの敵船オブジェクト)は
**1×1 の空画像**で、そのため襲撃時に敵船が現れません(サーバー側のパケットは正しいことを確認済み)。
この画像を持つ別バージョンの Map.wz(GMS v83 等では 97 が実画像)から移植します。

```
DevTools\wz_graft_airship.bat <移植元の Map.wz> [<クライアントのフォルダ>]
```

このバッチは、移植元の `97` を確認(1×1 なら中止)→ `Map.wz.new` を生成 → verify → 元の `Map.wz` を
`Map.wz.bak` に退避 → 差し替え、まで行います。クライアントのフォルダ既定は `.env` の
`CRONUS_CLIENT`… ではなく、実際に遊ぶ **`Client\MapleStory_v186_edit`** です(引数で変更可)。
手動で行う場合:

```
DevTools\wz ls    other\Map.wz Obj/vehicle.img/ship/ossyria/97        (実画像か確認)
DevTools\wz png   other\Map.wz Obj/vehicle.img/ship/ossyria/97 ship97.png
DevTools\wz graft other\Map.wz Obj/vehicle.img/ship/ossyria/97 ^
                  Client\MapleStory_v186_edit\Map.wz Obj/vehicle.img/ship/ossyria/97 ^
                  --out Client\MapleStory_v186_edit\Map.wz.new
```

## 例1b: 全マップで飛行を許可する(`/gmmove` 用)

```
DevTools\wz_enable_fly.bat [<クライアントのフォルダ>]
```

詳細は [CLIENT_PATCHES.md](CLIENT_PATCHES.md)。

## 例2: 別バージョンのアバター(装備)を移植する

装備1点は概ね次の3か所に分かれています。全部を移植すれば見た目・アイコン・名前が揃います。

| 内容 | 場所 | graft のノード |
|---|---|---|
| 見た目(フレーム、アイコン `info/icon`) | `Character.wz/<部位>/<ID>.img` | img 全体を移植: `Character.wz` 側 `<部位>/<ID>.img` を、dst の同じ `<部位>` に **`<部位>/<ID>.img` として** — 現在の `graft` は img 内のノード単位なので、img 全体は「dst に同名 img が存在する場合の置換」になります。新規 img の追加は下記「制限」を参照 |
| 名前・説明 | `String.wz/Eqp.img/Eqp/<部位>/<ID>` | `graft other\String.wz Eqp.img/Eqp/Cap/1002140 Client\...\String.wz Eqp.img/Eqp/Cap/1002140 --out ...` |
| 装備ステータス(サーバー側) | gamedata.db(`Cronus.Ingest` で再生成) | wz を差し替えたら `ingest.bat` を再実行 |

部位名は `Cap / Coat / Longcoat / Pants / Shoes / Glove / Shield / Cape / Accessory / Weapon / Face / Hair` など
(`DevTools\wz ls Character.wz` で確認できます)。

## 制限と注意

- **新規 img の追加**(dst に存在しない `<ID>.img` を足す)は現状未対応です(既存 img の置換と、
  img 内ノードの置換/追加のみ)。必要になったら `WzArchiveModel` に entry を足す形で拡張できます
  (`WzDirModel.AddImage` は書き込みモデル側には既にあります)。
- PNG からキャンバスを作る(自作画像の取り込み)は未対応です。移植は wz 間コピーが対象です。
- 移植元のアーカイブは `info` で必ず開けることを確認してください(IV/バージョン自動検出)。読めない
  バージョン(2021年以降の新形式 `.wz`/`.ms`)は対象外です。
- `List.wz` に列挙された img は別鍵で暗号化されています(pre-BB)。graft 先がそれに該当する場合も、
  リーダーが鍵を試行して開き、同じ鍵で書き戻します。
- 差し替え前に **必ずバックアップ**(バッチは自動で `.bak` を作ります)。クライアントの表示に
  問題が出たら `.bak` を戻してください。サーバーの gamedata.db は影響を受けません
  (`ingest.bat` を再実行するまで)。
