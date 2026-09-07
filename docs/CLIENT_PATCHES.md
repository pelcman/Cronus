# クライアント側パッチ(`DevTools/`)

サーバーからは変えられないクライアント側の制限を、バイナリ/データの最小変更で外すスクリプト群です。
どれもバックアップを残し、`revert` で元に戻せます。対象は**ショートカットが起動しているクライアント
`Client\MapleStory_v186`**(`RunEmu.ini` の `TargetEXE`)で、各スクリプトの既定値もそこです。
`Client\MapleStory_v186_edit` は公開IP向け `LocalHost.ini` を持つ2つ目のコピーで、2026-09-07 までの
パッチはこちらにだけ当たっていました(exe 27か所と fly=1 の Map.wz)。起動していないクライアントに
当てても何も変わらないので、`RunEmu.ini` の `TargetEXE` を見て対象を確かめてください。原本は各ファイルの
`.orig` / `.bak` です。
EmuClient のローダー(`EmuMain.dll` の MSCRC バイパス)が改変済み exe を通すので、Riremito さんの
`_L` 版 exe 自体と同じ扱いで動きます。

| スクリプト | 対象 | 何を変えるか | いつ必要か |
|---|---|---|---|
| `wzpatch_namespace.py apply` | `NameSpace.dll`(2バイト×2) | 遅延WZアーカイブ open の `push 1`→`push 2`(iGPUplz と同じ) | ゲーム入場で落ちる環境(必須) |
| `clientpatch_speedcap.py apply` | `JMS_v186.1_L.exe`(27か所) | 速度140% / ジャンプ123% / 攻撃速度段階2 の clamp 無効化(表示用・物理用の両方)、歩行アニメ上限140%→1000% | `/gmmove` の倍率を効かせたい時 |
| `wz_graft_airship.bat <元Map.wz>` | `Map.wz`(`ship/ossyria/97`) | バルログ船の画像を別バージョンから移植 | 飛行船襲撃の演出を出したい時 |

## 速度・ジャンプ・攻撃速度の上限(`clientpatch_speedcap.py`)

`JMS_v186.1_L.exe` は圧縮されていない(`.text` エントロピー 6.5)ので、逆アセンブルで clamp を特定
しました。

```
速度:       mov ecx,140 ; cmp eax,ecx ; jge +2 ; mov ecx,eax  → nSpeed へ格納        (0x33C2D3, 0x358817)
ジャンプ:   cmp eax,123 ; … ; jl +3 ; push 123 ; pop eax        → nJump へ格納         (0x33C336, 0x48AD2C)
移動制御:   同じ2形(速度側の上限は引数)                             (0x44B0C0, 0x44B0F2)
攻撃速度:   cmp eax,2 ; jg +3 ; push 2 ; pop eax ; (…min 10) ; フレーム時間 = 基準×(段階+10)/16
                                                                     (0x61FAE, 0x6264E, 0x6534E, 0x65BBD)
歩行アニメ: cmp ecx,70 ; jg +3 ; push 70 ; pop ecx ; mov eax,140  (歩行アニメの再生速度 70〜140%)
                                                                     (0x625CA, 0x65B6D)
--- 2次(2026-09-07): ステータス窓と、ローカルプレイヤーが実際に動く物理値の clamp ---
SecondaryStat C: mov esi,140 ; cmp eax,esi ; mov ecx,eax ; jl +2 ; mov ecx,esi
                 (速度・ジャンプの2組、ステータス窓の 140% / 123% 表示の元)    (0x32F97D, 0x32F9FA)
CUser→移動制御: mov ecx,140 ; cmp eax,ecx ; jl +3 ; mov [ebp-10h],ecx (他プレイヤー表示用) (0x34C4AF)
CUserLocal 物理: 速度 = min(合計, 上限)  上限 = 140(既定即値 0x687AA4)/ 乗車時 190(0x687EA3)
                 ジャンプ = min(max(合計,80),123)  → ×0.01 で移動制御(+0x84 速度, +0x48 ジャンプ)へ
                 徒歩・乗車2経路の3コピー                (0x687B91, 0x687BA9, 0x687ECA, 0x688009)
--- 3次(2026-09-07): ステータス窓(CUIStatDetail)の表示値 ---
表示値:          窓の描画関数が SecondaryStat の GetSpeed()/GetJump()(装備値とバフ値の max + 基本値、丸め無し)
                 を呼び、自前で 速度 = min(上限140 [即値 0x5DAA32] / ペット乗車 190, x)、
                 ジャンプ = x > 123 ? 123 : x と丸めてから文字列 0x83f で描く。徒歩・乗り物・
                 ペット乗車の3経路、計6か所            (0x5DAA33, 0x5DAAC5, 0x5DACAA, 0x5DACC8, 0x5DADA3, 0x5DADB2)
```

2次の物理値パッチ後もステータス窓が 140% / 123% のままだったのは、窓が SecondaryStat の格納値ではなく
描画時に自前で丸めていたためです。3次で `jl/jge/jg` を `nop`(6バイトの jcc は `nop + jmp` か `nop`×6)に、
即値 140/190 を 10000 に変えています。

1次の12か所だけでは、ステータス窓が 140% / 123% のままで、実際の移動速度も変わりませんでした
(1次のサイトはリモートプレイヤーの SecondaryStat と別の移動制御経路)。2次で `CUserLocal` の
物理値関数を読み切り、既定上限の即値 140/190 を 10000 に、ジャンプの `jl +3` を `jmp` に変えています。

条件ジャンプ2バイトを `jge→nop nop` / `jl,jg→jmp` に変える(命令長は不変)か、即値 140→1000 を
書き換えます(物理上限の即値は 140/190→10000)。書き換え前に27か所の周辺バイト列を照合し、別ビルドなら拒否します。`status` で
現在の状態を確認できます。攻撃速度は「段階」が 2 未満(負数)になれるようになり、サーバー側の
`/gmmove` は段階 −8(フレーム時間 1/8)を下限に Booster 値を計算します。

```
python DevTools\clientpatch_speedcap.py status
python DevTools\clientpatch_speedcap.py apply     ← クライアントを閉じてから
python DevTools\clientpatch_speedcap.py revert
```

適用後は `/gmmove <速度> [<ジャンプ> [<攻撃速度>]]` の倍率がそのまま効きます(速度は上限 100倍 = 10000% まで、
これは物理上限の新しい即値と一致させています)。
未適用のクライアントでは従来どおり 140% / 123% / 段階2 で頭打ちです。

## 飛行について(`wz_enable_fly.bat` は撤去)

以前は全マップの `info/fly` を 1 にして飛行を全マップで許可していましたが、`info/fly` は
**マップ自体を飛行マップにする**フラグで、Flying 一時ステータスの有無に関係なくそのマップの全員が
飛んでしまうと判明しました(クライアントの移動モード設定ルーチンがマップの fly フラグだけを見て
飛行モードにするため)。プレイヤー単位の GM 飛行はこのクライアントでは実現できないため、
`/gmfly` と `wz_enable_fly.bat` は撤去しました。飛行は元から飛べる 72 マップの属性のままです。
