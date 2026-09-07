# クライアント側パッチ(`DevTools/`)

サーバーからは変えられないクライアント側の制限を、バイナリ/データの最小変更で外すスクリプト群です。
どれもバックアップを残し、`revert` で元に戻せます。対象は遊ぶ側のクライアント
`Client\MapleStory_v186_edit`(`Client\MapleStory_v186` は無改変の原本として残します)。
EmuClient のローダー(`EmuMain.dll` の MSCRC バイパス)が改変済み exe を通すので、Riremito さんの
`_L` 版 exe 自体と同じ扱いで動きます。

| スクリプト | 対象 | 何を変えるか | いつ必要か |
|---|---|---|---|
| `wzpatch_namespace.py apply` | `NameSpace.dll`(2バイト×2) | 遅延WZアーカイブ open の `push 1`→`push 2`(iGPUplz と同じ) | ゲーム入場で落ちる環境(必須) |
| `clientpatch_speedcap.py apply` | `JMS_v186.1_L.exe`(2バイト×6) | 速度上限140% / ジャンプ上限123% の clamp を無効化 | `/gmmove` で3倍速・高ジャンプにしたい時 |
| `wz_enable_fly.bat` | `Map.wz`(全マップの `info/fly`=1) | 飛行(CTS_Flying)を全マップで許可 | `/gmmove` で飛びたい時 |
| `wz_graft_airship.bat <元Map.wz>` | `Map.wz`(`ship/ossyria/97`) | バルログ船の画像を別バージョンから移植 | 飛行船襲撃の演出を出したい時 |

## 速度・ジャンプ上限(`clientpatch_speedcap.py`)

`JMS_v186.1_L.exe` は圧縮されていない(`.text` エントロピー 6.5)ので、逆アセンブルで clamp を特定
しました。同じ2つの慣用句が6か所にあります。

```
速度:     mov ecx,140 ; cmp eax,ecx ; jge +2 ; mov ecx,eax    → nSpeed へ格納   (0x33C2D3, 0x358817)
ジャンプ: cmp eax,123 ; … ; jl +3 ; push 123 ; pop eax          → nJump へ格納    (0x33C336, 0x48AD2C)
移動制御: 同じ2形(速度側の上限は引数)                               (0x44B0C0, 0x44B0F2)
```

各所の条件ジャンプ2バイトだけを `jge→nop nop` / `jl→jmp` に変えます(命令長は不変)。書き換え前に
6か所の周辺バイト列を照合し、別ビルドなら拒否します。`status` で現在の状態を確認できます。

```
python DevTools\clientpatch_speedcap.py status
python DevTools\clientpatch_speedcap.py apply     ← クライアントを閉じてから
python DevTools\clientpatch_speedcap.py revert
```

適用後は `/gmmove on` の Speed +200 / Jump +80 がそのまま効き、300% / 180% になります。
未適用のクライアントでは従来どおり 140% / 123% で頭打ちです。

## 全マップ飛行可(`wz_enable_fly.bat`)

クライアントは `Flying` 一時ステータスを、マップデータの `info/fly` が 1 のマップ(v186 では72枚:
リプレ天空地域、母船キューブ、時間の神殿への飛行船など)でだけ飛行として扱います。
`DevTools\wz set-int Map.wz "Map/Map*/*.img" info/fly 1` で全マップ(5536 img、72 枚は元から 1)にフラグを立て、
検証(全 img の解析比較: 変更した img 以外は同一)を通してから差し替えます。
`Flying` を持たないプレイヤーの歩行は変わりません(飛行船の甲板マップも fly=1 で普通に歩けています)。

```
DevTools\wz_enable_fly.bat            ← Client\MapleStory_v186_edit\Map.wz を差し替え(.bak を作成)
```

元に戻すには `Map.wz.bak` を `Map.wz` に戻します。サーバーの gamedata.db には影響しません。
