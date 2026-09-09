# Cosmic → Cronus script gap (generated — do not edit by hand)

Regenerate with `python DevTools/cosmic_gap.py`. Cosmic = GMS v83 content reference; every row is filtered to what the JMS v186 client has (image / quest / map / reactor), so each is real work. Ids and numbers must still be re-checked against v186 data when porting (`[DEV]` rule).

| | Cosmic | Cronus | gap (JMS-relevant) |
|---|---|---|---|
| NPC scripts | 682 | 244 | 417 (on JMS maps: 303) |
| quest scripts | 250 | 1 | 220 |
| portal scripts | 458 | 29 | 318 (+208 JMS portal scripts neither has) |
| reactor scripts | 292 | 1 | 235 |
| event scripts (PQ / boss / ride) | 108 | — | 85 reference JMS maps |

## NPCs Cosmic scripts and Cronus does not (with a JMS image), most-placed first

| npc | JMS name | JMS maps | Cosmic file |
|---|---|---|---|
| 2042002 | シュピゲルマン | 20: 103000000 カニングシティー, 980000010 モンスターカーニバル出口, 980000103 カーニバルフィールド1&lt;勝者ルーム&gt; … | `Reference/Cosmic/scripts/npc/2042002.js` |
| 9001102 | 月うさぎ | 19: 100000000 ヘネシス, 101000000 エリニア, 102000000 ぺリオン … | `Reference/Cosmic/scripts/npc/9001102.js` |
| 2042001 | シュピゲルマン | 9: 980000100 カーニバルフィールド1&lt;控え室&gt;, 980000200 カーニバルフィールド2&lt;控え室&gt;, 980000300 カーニバルフィールド3&lt;控え室&gt; … | `Reference/Cosmic/scripts/npc/2042001.js` |
| 9040005 | 帰還碑 | 9: 990000100 守護の谷, 990000200 遺跡の入口, 990000300 シャレニアン城門 … | `Reference/Cosmic/scripts/npc/9040005.js` |
| 1061007 | 崩れている石像 | 7: 105040310 忍耐の森「1段階」, 105040311 忍耐の森「2段階」, 105040312 忍耐の森「3段階」 … | `Reference/Cosmic/scripts/npc/1061007.js` |
| 9000040 | ダリア | 7: 100000200 広場, 101000000 エリニア, 102000000 ぺリオン … | `Reference/Cosmic/scripts/npc/9000040.js` |
| 9000041 | 寄付 | 7: 100000200 広場, 101000000 エリニア, 102000000 ぺリオン … | `Reference/Cosmic/scripts/npc/9000041.js` |
| 2013001 | 侍従イク | 6: 920010400 休憩室, 920010500 封印された部屋, 920010600 ラウンジ … | `Reference/Cosmic/scripts/npc/2013001.js` |
| 2042003 | 助手レッド | 6: 980000100 カーニバルフィールド1&lt;控え室&gt;, 980000300 カーニバルフィールド3&lt;控え室&gt;, 980000500 カーニバルフィールド5&lt;控え室&gt; … | `Reference/Cosmic/scripts/npc/2042003.js` |
| 2042007 | シュピゲルマン | 6: 980030010 出口, 980031000 カーニバルフィールド1&lt;控え室&gt;, 980031300 カーニバルフィールド1&lt;勝者ルーム&gt; … | `Reference/Cosmic/scripts/npc/2042007.js` |
| 9040004 | 名誉の石碑 | 6: 102000000 ぺリオン, 200000300 出会いの丘, 211000000 エルナス … | `Reference/Cosmic/scripts/npc/9040004.js` |
| 2112003 | ジュリエット | 5: 261000021 アルカドノ秘密の部屋, 926110000 怪しい研究室, 926110001 暗い通路 … | `Reference/Cosmic/scripts/npc/2112003.js` |
| 2133001 | エリン | 5: 930000000 森の前, 930000010 森入口, 930000300 霧の森 … | `Reference/Cosmic/scripts/npc/2133001.js` |
| 9201021 | ロビン | 5: 680000300 ウェディングフォトスタジオ, 680000401 ウェディング披露宴会場, 680000600 … | `Reference/Cosmic/scripts/npc/9201021.js` |
| 2040050 | 風来坊錬金術師 | 4: 105040300 スリーピーウッド, 211000000 エルナス, 220000000 ルディブリアム … | `Reference/Cosmic/scripts/npc/2040050.js` |
| 2094002 | キキョウコライ | 4: 925100200 甲板突破1, 925100300 甲板突破2, 925100500 海賊船長の威厳 … | `Reference/Cosmic/scripts/npc/2094002.js` |
| 2112004 | ロミオ | 4: 261000011 ジェニミスト秘密の部屋, 926100000 怪しい研究室, 926100300 ユレテの事務所 … | `Reference/Cosmic/scripts/npc/2112004.js` |
| 9000002 | ピエトロ | 4: 108000200 木のダンジョン1, 108000201 木のダンジョン2, 108000202 木のダンジョン3 … | `Reference/Cosmic/scripts/npc/9000002.js` |
| 9001105 | 할아버지 월묘 | 4: 922231001 月うさぎの遊び場, 922240000 ガガを救出せよ！, 922240200 ガガ救出失敗 … | `Reference/Cosmic/scripts/npc/9001105.js` |
| 9201108 | マスターボウマン | 4: 803001100 統一の試練, 803001140 弓使いを統べし間, 803011100 統一の試練 … | `Reference/Cosmic/scripts/npc/9201108.js` |
| 1012112 | ウサチャン | 3: 100000200 広場, 910010100 近道, 910010400 近道 | `Reference/Cosmic/scripts/npc/1012112.js` |
| 2042000 | シュピゲルマン | 3: 200000000 オルビス, 220000000 ルディブリアム, 980000000 モンスターカーニバル入口 | `Reference/Cosmic/scripts/npc/2042000.js` |
| 2042004 | 助手ブルー | 3: 980000200 カーニバルフィールド2&lt;控え室&gt;, 980000400 カーニバルフィールド4&lt;控え室&gt;, 980000600 カーニバルフィールド6&lt;控え室&gt; | `Reference/Cosmic/scripts/npc/2042004.js` |
| 2042008 | 助手レッド | 3: 980031000 カーニバルフィールド1&lt;控え室&gt;, 980032000 カーニバルフィールド2&lt;控え室&gt;, 980033000 カーニバルフィールド3&lt;控え室&gt; | `Reference/Cosmic/scripts/npc/2042008.js` |
| 2101017 | セザール | 3: 980010100 一番目の闘技場&lt;控え室&gt;, 980010200 二番目の闘技場&lt;控え室&gt;, 980010300 三番目の闘技場&lt;控え室&gt; | `Reference/Cosmic/scripts/npc/2101017.js` |
| 2101018 | セザール | 3: 102000000 ぺリオン, 220000000 ルディブリアム, 260000000 アリアント | `Reference/Cosmic/scripts/npc/2101018.js` |
| 2103013 | デュアート | 3: 926010000 ピラミッドの丘, 926010001 ネトのピラミッド, 926020001 ピラミッドの陰 | `Reference/Cosmic/scripts/npc/2103013.js` |
| 2112013 | 調査結果 | 3: 926110000 怪しい研究室, 926110001 暗い通路, 926110203 ユレテの研究室 | `Reference/Cosmic/scripts/npc/2112013.js` |
| 9201022 | トーマス | 3: 680000500 出口, 680000501 出口, 680000502 出口 | `Reference/Cosmic/scripts/npc/9201022.js` |
| 22000 | シャンクス | 2: 2000000 サウスペリ, 104000000 港口 | `Reference/Cosmic/scripts/npc/22000.js` |
| 1012113 | ウサクン | 2: 910010100 近道, 910010300 村への帰り道 | `Reference/Cosmic/scripts/npc/1012113.js` |
| 1013200 | 赤ちゃんオオカミ | 2: 900020100 鬱蒼とした森, 900020110 鬱蒼とした森 | `Reference/Cosmic/scripts/npc/1013200.js` |
| 1061018 | ムヨン | 2: 105100300 バルログの墓, 105100400 イージーモード_バルログの墓 | `Reference/Cosmic/scripts/npc/1061018.js` |
| 1102003 | キダン | 2: 130000100 騎士の殿堂, 130000101 騎士の殿堂 | `Reference/Cosmic/scripts/npc/1102003.js` |
| 1202010 | プディン | 2: 140010110 英雄の殿堂, 140010111 英雄の殿堂 | `Reference/Cosmic/scripts/npc/1202010.js` |
| 1300014 | セルフ | 2: 106020500 城壁の端, 106021500 結婚式場入口 | `Reference/Cosmic/scripts/npc/1300014.js` |
| 2013000 | 妖精ウィンキー | 2: 200080101 見知らぬ塔, 920010000 入口 | `Reference/Cosmic/scripts/npc/2013000.js` |
| 2081010 | ムス(FieldsetEnterance) | 2: 924000000 修練場への道, 924000002 修練場出口 | `Reference/Cosmic/scripts/npc/2081010.js` |
| 2112007 | 調査結果 | 2: 926100000 怪しい研究室, 926100203 ユレテの研究室 | `Reference/Cosmic/scripts/npc/2112007.js` |
| 9000012 | ハーリー | 2: 109080000 ココナッツシーズン, 109080003 G★ココナッツシーズン | `Reference/Cosmic/scripts/npc/9000012.js` |
| 9000049 | 童話妖精クレコス | 2: 180000000 撮影現場, 980040000 魔女の塔入口 | `Reference/Cosmic/scripts/npc/9000049.js` |
| 9040011 | 掲示板 | 2: 101030104 遺跡発掘ベースキャンプ, 990000000 遺跡発掘現場 | `Reference/Cosmic/scripts/npc/9040011.js` |
| 9201002 | 教皇ジョン | 2: 680000000 ウェディングタウン, 680000210 ウェディング(大聖堂) | `Reference/Cosmic/scripts/npc/9201002.js` |
| 9201005 | ニコル | 2: 680000000 ウェディングタウン, 680000200 ウェディングホール待機室(大聖堂) | `Reference/Cosmic/scripts/npc/9201005.js` |
| 9201006 | デビー | 2: 680000200 ウェディングホール待機室(大聖堂), 680000210 ウェディング(大聖堂) | `Reference/Cosmic/scripts/npc/9201006.js` |
| 9201008 | ボニー | 2: 680000000 ウェディングタウン, 889300100 ウェディングホール待機室(ハウスウェディング) | `Reference/Cosmic/scripts/npc/9201008.js` |
| 9201009 | ジャッキー | 2: 889300100 ウェディングホール待機室(ハウスウェディング), 889300101 ウェディング(ハウスウェディング) | `Reference/Cosmic/scripts/npc/9201009.js` |
| 9201023 | ナナ | 2: 100000200 広場, 101000000 エリニア | `Reference/Cosmic/scripts/npc/9201023.js` |
| 9201107 | マスターウォリアー | 2: 803001100 統一の試練, 803011100 統一の試練 | `Reference/Cosmic/scripts/npc/9201107.js` |
| 9201109 | マスターメイジ | 2: 803001100 統一の試練, 803011100 統一の試練 | `Reference/Cosmic/scripts/npc/9201109.js` |
| 9201110 | マスターシーフ | 2: 803001100 統一の試練, 803011100 統一の試練 | `Reference/Cosmic/scripts/npc/9201110.js` |
| 9201111 | マスターパイレーツ | 2: 803001100 統一の試練, 803011100 統一の試練 | `Reference/Cosmic/scripts/npc/9201111.js` |
| 9201112 | ジャック | 2: 803000502 クリムゾン砦3, 803000505 永遠の警戒地 | `Reference/Cosmic/scripts/npc/9201112.js` |
| 2003 | ロビン | 1: 50000 危険な森 | `Reference/Cosmic/scripts/npc/2003.js` |
| 2007 | イベントガイド | 1: 10000 キノコの丘 | `Reference/Cosmic/scripts/npc/2007.js` |
| 2100 | セーラ | 1: 10000 キノコの丘 | `Reference/Cosmic/scripts/npc/2100.js` |
| 2101 | ヒナ | 1: 10000 キノコの丘 | `Reference/Cosmic/scripts/npc/2101.js` |
| 11000 | シード | 1: 1000001 アムホストの武器屋 | `Reference/Cosmic/scripts/npc/11000.js` |
| 12101 | レイン | 1: 1000000 アムホスト | `Reference/Cosmic/scripts/npc/12101.js` |
| 1002006 | チェフ | 1: 104000000 港口 | `Reference/Cosmic/scripts/npc/1002006.js` |
| 1002100 | ジェーン | 1: 104000000 港口 | `Reference/Cosmic/scripts/npc/1002100.js` |
| 1012006 | バルトス | 1: 100000202 ペットの散歩路 | `Reference/Cosmic/scripts/npc/1012006.js` |
| 1012007 | プロド | 1: 100000202 ペットの散歩路 | `Reference/Cosmic/scripts/npc/1012007.js` |
| 1012116 | ヘネシス草むら | 1: 100020000 東の草むら | `Reference/Cosmic/scripts/npc/1012116.js` |
| 1012119 | ヘンケル | 1: 100010000 東の丘 | `Reference/Cosmic/scripts/npc/1012119.js` |
| 1013001 | ドラゴン | 1: 900010200 夢見る森 | `Reference/Cosmic/scripts/npc/1013001.js` |
| 1013002 | ドラゴンの巣 | 1: 900020220 忘れてしまった森 | `Reference/Cosmic/scripts/npc/1013002.js` |
| 1013104 | 卵の箱 | 1: 100030102 前庭 | `Reference/Cosmic/scripts/npc/1013104.js` |
| 1032002 | エトラン | 1: 101000000 エリニア | `Reference/Cosmic/scripts/npc/1032002.js` |
| 1032003 | シェイン | 1: 101000000 エリニア | `Reference/Cosmic/scripts/npc/1032003.js` |
| 1032100 | 妖精アルウェン | 1: 101000000 エリニア | `Reference/Cosmic/scripts/npc/1032100.js` |
| 1032102 | 妖精マル | 1: 101000200 マルの森 | `Reference/Cosmic/scripts/npc/1032102.js` |
| 1032109 | 魔法図書館隈 | 1: 910110000 魔法図書館 | `Reference/Cosmic/scripts/npc/1032109.js` |
| 1032110 | 魔法図書館隈 | 1: 910110000 魔法図書館 | `Reference/Cosmic/scripts/npc/1032110.js` |
| 1032111 | 小さい切り株 | 1: 101010103 渓流&lt;バンジージャンプ台&gt; | `Reference/Cosmic/scripts/npc/1032111.js` |
| 1043000 | 花の摘み | 1: 101000101 忍耐の森-2段階 | `Reference/Cosmic/scripts/npc/1043000.js` |
| 1043001 | 薬草の藪 | 1: 101000104 忍耐の森-5段階 | `Reference/Cosmic/scripts/npc/1043001.js` |
| 1052002 | 裏通りのゼイエム | 1: 103000000 カニングシティー | `Reference/Cosmic/scripts/npc/1052002.js` |
| 1052013 | パソコン | 1: 193000000 ネットカフェ | `Reference/Cosmic/scripts/npc/1052013.js` |
| 1052014 | 自販機 | 1: 193000000 ネットカフェ | `Reference/Cosmic/scripts/npc/1052014.js` |
| 1052109 | 地下鉄のゴミ箱 | 1: 103000101 1号線-1区 | `Reference/Cosmic/scripts/npc/1052109.js` |
| 1052111 | 地下鉄のゴミ箱 | 1: 103000101 1号線-1区 | `Reference/Cosmic/scripts/npc/1052111.js` |
| 1052112 | 地下鉄のゴミ箱 | 1: 103000101 1号線-1区 | `Reference/Cosmic/scripts/npc/1052112.js` |
| 1061000 | クリシュラマ | 1: 105040300 スリーピーウッド | `Reference/Cosmic/scripts/npc/1061000.js` |
| 1061006 | 変な形の石像 | 1: 105040300 スリーピーウッド | `Reference/Cosmic/scripts/npc/1061006.js` |
| 1061009 | 次元の扉 | 1: 105070001 アリの巣-広場 | `Reference/Cosmic/scripts/npc/1061009.js` |
| 1061012 | 亡霊 | 1: 105090200 別世界への扉 | `Reference/Cosmic/scripts/npc/1061012.js` |
| 1061014 | ムヨン | 1: 105100100 神殿の底 | `Reference/Cosmic/scripts/npc/1061014.js` |
| 1061016 | 怪しい男 | 1: 105100000 地下への下り道 | `Reference/Cosmic/scripts/npc/1061016.js` |
| 1061100 | ホテルガイド | 1: 105040400 スリーピーホテルロビー | `Reference/Cosmic/scripts/npc/1061100.js` |
| 1063000 | 桃色の花山 | 1: 105040311 忍耐の森「2段階」 | `Reference/Cosmic/scripts/npc/1063000.js` |
| 1063001 | 青色の花山 | 1: 105040313 忍耐の森「4段階」 | `Reference/Cosmic/scripts/npc/1063001.js` |
| 1063002 | 白色の花山 | 1: 105040316 忍耐の森「7段階」 | `Reference/Cosmic/scripts/npc/1063002.js` |
| 1063013 | 霊験な石2 | 1: 105090000 光を失った洞窟1 | `Reference/Cosmic/scripts/npc/1063013.js` |
| 1063016 | 不思議な石像 | 1: 910510100 人形使いの秘密通路 | `Reference/Cosmic/scripts/npc/1063016.js` |
| 1063017 | 不思議な石像 | 1: 910510100 人形使いの秘密通路 | `Reference/Cosmic/scripts/npc/1063017.js` |
| 1092000 | タンユン | 1: 120000103 食堂 | `Reference/Cosmic/scripts/npc/1092000.js` |
| 1092007 | ムラト | 1: 120000100 上階廊下 | `Reference/Cosmic/scripts/npc/1092007.js` |
| 1092008 | シュリンツ | 1: 120000104 訓練場 | `Reference/Cosmic/scripts/npc/1092008.js` |
| 1092010 | ジャック | 1: 120000100 上階廊下 | `Reference/Cosmic/scripts/npc/1092010.js` |
| 1092015 | 浄水器 | 1: 120000202 寝室 | `Reference/Cosmic/scripts/npc/1092015.js` |
| 1092016 | 輝く石 | 1: 120000301 動力室 | `Reference/Cosmic/scripts/npc/1092016.js` |
| 1092018 | ゴミ箱 | 1: 120000100 上階廊下 | `Reference/Cosmic/scripts/npc/1092018.js` |
| 1092019 | ジョナサン | 1: 120000102 ジョナサンの部屋 | `Reference/Cosmic/scripts/npc/1092019.js` |
| 1092090 | 母牛 | 1: 912000100 ノーチラスの牛小屋 | `Reference/Cosmic/scripts/npc/1092090.js` |
| 1092091 | 母牛 | 1: 912000100 ノーチラスの牛小屋 | `Reference/Cosmic/scripts/npc/1092091.js` |
| 1092094 | 子牛 | 1: 912000100 ノーチラスの牛小屋 | `Reference/Cosmic/scripts/npc/1092094.js` |
| 1092095 | 子牛 | 1: 912000100 ノーチラスの牛小屋 | `Reference/Cosmic/scripts/npc/1092095.js` |
| 1094000 | バト | 1: 120000000 ノーチラス | `Reference/Cosmic/scripts/npc/1094000.js` |
| 1094002 | 草原 | 1: 120000000 ノーチラス | `Reference/Cosmic/scripts/npc/1094002.js` |
| 1094003 | 草原 | 1: 120000000 ノーチラス | `Reference/Cosmic/scripts/npc/1094003.js` |
| 1094004 | 草原 | 1: 120000000 ノーチラス | `Reference/Cosmic/scripts/npc/1094004.js` |
| 1094005 | 草原 | 1: 120000000 ノーチラス | `Reference/Cosmic/scripts/npc/1094005.js` |
| 1094006 | 草原 | 1: 120000000 ノーチラス | `Reference/Cosmic/scripts/npc/1094006.js` |
| 1101001 | 神獣 | 1: 130000000 エレヴ | `Reference/Cosmic/scripts/npc/1101001.js` |
| 1102002 | キリド | 1: 130010220 孵化場 | `Reference/Cosmic/scripts/npc/1102002.js` |
| 1103000 | デュナミス | 1: 924010200 暗黒の魔女の洞窟 | `Reference/Cosmic/scripts/npc/1103000.js` |
| 1103005 | ナインハート | 1: 913040006 シグナス騎士団 | `Reference/Cosmic/scripts/npc/1103005.js` |
| 1104000 | フランシス | 1: 910510001 人形使いの本拠地 | `Reference/Cosmic/scripts/npc/1104000.js` |
| 1104200 | 倒れた騎士 | 1: 924010100 暗黒の魔女の洞窟 | `Reference/Cosmic/scripts/npc/1104200.js` |
| 1104201 | シグナス | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104201.js` |
| 1104202 | ナインハート | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104202.js` |
| 1104203 | ミハエル | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104203.js` |
| 1104204 | オズ | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104204.js` |
| 1104205 | イリーナ | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104205.js` |
| 1104206 | イカルト | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104206.js` |
| 1104207 | ホークアイ | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/npc/1104207.js` |
| 1204001 | フランシス | 1: 910510200 人形使いの洞窟 | `Reference/Cosmic/scripts/npc/1204001.js` |
| 1209000 | ヘレナ | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/npc/1209000.js` |
| 1209001 | 避難民1 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/npc/1209001.js` |
| 1209002 | 避難民2 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/npc/1209002.js` |
| 1209003 | 避難民3・4・5 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/npc/1209003.js` |
| 1209004 | 避難民6 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/npc/1209004.js` |
| 1209005 | 避難民7 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/npc/1209005.js` |
| 1300001 | ペペキング | 1: 106021600 結婚式場 | `Reference/Cosmic/scripts/npc/1300001.js` |
| 1300006 | ズペ王子 | 1: 106021600 結婚式場 | `Reference/Cosmic/scripts/npc/1300006.js` |
| 1300012 | 東石塔門 | 1: 106021400 東の塔 | `Reference/Cosmic/scripts/npc/1300012.js` |
| 1300013 | マックフィンイブグ | 1: 106021402 最後の城塔 | `Reference/Cosmic/scripts/npc/1300013.js` |
| 2010000 | チャーリー軍曹 | 1: 200000000 オルビス | `Reference/Cosmic/scripts/npc/2010000.js` |
| 2010003 | ネーブ | 1: 200000200 オルビス公園 | `Reference/Cosmic/scripts/npc/2010003.js` |
| 2012012 | リーサ | 1: 200000000 オルビス | `Reference/Cosmic/scripts/npc/2012012.js` |
| 2012023 | 紅葉玉 | 1: 200000300 出会いの丘 | `Reference/Cosmic/scripts/npc/2012023.js` |
| 2012027 | ヒューズ | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012027.js` |
| 2012028 | ハープ弦&lt;レ&gt; | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012028.js` |
| 2012029 | ハープ弦&lt;ミ&gt; | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012029.js` |
| 2012030 | ハープ弦&lt;ファ&gt; | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012030.js` |
| 2012031 | ハープ弦&lt;ソ&gt; | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012031.js` |
| 2012032 | ハープ弦&lt;ラ&gt; | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012032.js` |
| 2012033 | ハープ弦&lt;シ&gt; | 1: 920020000 エリジャーの庭園 | `Reference/Cosmic/scripts/npc/2012033.js` |
| 2013002 | 女神ミネルバ | 1: 920011300 女神の祝福 | `Reference/Cosmic/scripts/npc/2013002.js` |
| 2020005 | アルケスタ | 1: 211000100 市場 | `Reference/Cosmic/scripts/npc/2020005.js` |
| 2022004 | タイラス | 1: 921100301 タイラス護衛完遂 | `Reference/Cosmic/scripts/npc/2022004.js` |
| 2030008 | アドビス | 1: 211042300 ジャクムへの門 | `Reference/Cosmic/scripts/npc/2030008.js` |
| 2030014 | 古代氷石 | 1: 921100100 氷の谷 | `Reference/Cosmic/scripts/npc/2030014.js` |
| 2032001 | スピルナ | 1: 200050001 老婆の家 | `Reference/Cosmic/scripts/npc/2032001.js` |
| 2040002 | レフトくん | 1: 221024400 エオス塔100階 | `Reference/Cosmic/scripts/npc/2040002.js` |
| 2040020 | ジロクン | 1: 220000303 ジロクンとペイの家 | `Reference/Cosmic/scripts/npc/2040020.js` |
| 2040021 | ペイ | 1: 220000303 ジロクンとペイの家 | `Reference/Cosmic/scripts/npc/2040021.js` |
| 2040024 | 一番目のエオス石 | 1: 221024400 エオス塔100階 | `Reference/Cosmic/scripts/npc/2040024.js` |
| 2040026 | 三番目のエオス石 | 1: 221021700 エオス塔41階 | `Reference/Cosmic/scripts/npc/2040026.js` |
| 2040030 | ウィスブ | 1: 220000400 エオス塔入口 | `Reference/Cosmic/scripts/npc/2040030.js` |
| 2040031 | 文書束 | 1: 220000304 クロイの家 | `Reference/Cosmic/scripts/npc/2040031.js` |
| 2040032 | ウィーバー | 1: 220000006 ルディブリアムの散歩路 | `Reference/Cosmic/scripts/npc/2040032.js` |
| 2040033 | ネル | 1: 220000006 ルディブリアムの散歩路 | `Reference/Cosmic/scripts/npc/2040033.js` |
| 2040052 | 司書ウィズ | 1: 222020000 ヘリオス塔の図書館 | `Reference/Cosmic/scripts/npc/2040052.js` |
| 2041023 | プロ | 1: 220050300 時間の通路 | `Reference/Cosmic/scripts/npc/2041023.js` |
| 2041024 | 造形物 | 1: 220080000 時計塔の奥 | `Reference/Cosmic/scripts/npc/2041024.js` |
| 2041029 | カレン | 1: 222020400 時間制御室 | `Reference/Cosmic/scripts/npc/2041029.js` |
| 2042005 | シュピゲルマン | 1: 980030000 シュピゲルマンの事務室 | `Reference/Cosmic/scripts/npc/2042005.js` |
| 2043000 | ビシャスプラント | 1: 922020300 時計塔の深層部 | `Reference/Cosmic/scripts/npc/2043000.js` |
| 2060100 | 海の魔女カルタ | 1: 230040001 カルタの洞窟 | `Reference/Cosmic/scripts/npc/2060100.js` |
| 2071012 | 見覚えがある少女(キツネ) | 1: 922220000 冷たく寒い森 | `Reference/Cosmic/scripts/npc/2071012.js` |
| 2080000 | モス | 1: 240000000 リプレ | `Reference/Cosmic/scripts/npc/2080000.js` |
| 2081000 | 村長タタモ | 1: 240000000 リプレ | `Reference/Cosmic/scripts/npc/2081000.js` |
| 2082003 | コルバ | 1: 240000110 ステーション&lt;オルビス行き&gt; | `Reference/Cosmic/scripts/npc/2082003.js` |
| 2083005 | 生命の泉 | 1: 240050400 ホーンテイルの洞窟入口 | `Reference/Cosmic/scripts/npc/2083005.js` |
| 2090004 | チエル | 1: 250000000 武陵 | `Reference/Cosmic/scripts/npc/2090004.js` |
| 2094000 | キキョウコライ | 1: 251010404 海賊船の向こう | `Reference/Cosmic/scripts/npc/2094000.js` |
| 2094001 | キキョウニジン | 1: 925100600 キキョウニジンの感謝 | `Reference/Cosmic/scripts/npc/2094001.js` |
| 2095000 | デリー | 1: 925010400 ノーチラス避難所 | `Reference/Cosmic/scripts/npc/2095000.js` |
| 2100001 | ムハマード | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2100001.js` |
| 2100002 | ザーイド | 1: 260000000 アリアント | `Reference/Cosmic/scripts/npc/2100002.js` |
| 2100003 | ヤスミン | 1: 260000000 アリアント | `Reference/Cosmic/scripts/npc/2100003.js` |
| 2101000 | シリン | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2101000.js` |
| 2101001 | ジユル | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2101001.js` |
| 2101002 | エレスカ | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2101002.js` |
| 2101003 | アディン | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2101003.js` |
| 2101004 | ティガン | 1: 260000300 アリアント宮殿 | `Reference/Cosmic/scripts/npc/2101004.js` |
| 2101005 | バイラン | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2101005.js` |
| 2101007 | アレダ | 1: 260000303 アリアント宮殿&lt;王室&gt; | `Reference/Cosmic/scripts/npc/2101007.js` |
| 2101008 | シェヘラザード | 1: 260000303 アリアント宮殿&lt;王室&gt; | `Reference/Cosmic/scripts/npc/2101008.js` |
| 2101009 | アプドラ８世 | 1: 260000303 アリアント宮殿&lt;王室&gt; | `Reference/Cosmic/scripts/npc/2101009.js` |
| 2101010 | ジャノ | 1: 260000201 古い空き家 | `Reference/Cosmic/scripts/npc/2101010.js` |
| 2101011 | セザン | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2101011.js` |
| 2101014 | セザール | 1: 980010000 闘技場の控え室 | `Reference/Cosmic/scripts/npc/2101014.js` |
| 2101015 | アブドラ８世 | 1: 980010010 王の部屋 | `Reference/Cosmic/scripts/npc/2101015.js` |
| 2101016 | アレダ | 1: 980010010 王の部屋 | `Reference/Cosmic/scripts/npc/2101016.js` |
| 2103000 | 王宮オアシス | 1: 260000300 アリアント宮殿 | `Reference/Cosmic/scripts/npc/2103000.js` |
| 2103001 | 秘密の壁 | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/npc/2103001.js` |
| 2103002 | 王妃の飾り棚 | 1: 260000303 アリアント宮殿&lt;王室&gt; | `Reference/Cosmic/scripts/npc/2103002.js` |
| 2103003 | アリアント民家1 | 1: 260000202 民家1 | `Reference/Cosmic/scripts/npc/2103003.js` |
| 2103004 | アリアント民家2 | 1: 260000203 民家2 | `Reference/Cosmic/scripts/npc/2103004.js` |
| 2103005 | アリアント民家4 | 1: 260000205 民家4 | `Reference/Cosmic/scripts/npc/2103005.js` |
| 2103006 | アリアント民家6 | 1: 260000207 民家6 | `Reference/Cosmic/scripts/npc/2103006.js` |
| 2103009 | 民家1収納場所(透明) | 1: 260000202 民家1 | `Reference/Cosmic/scripts/npc/2103009.js` |
| 2103010 | 民家2収納場所(透明) | 1: 260000203 民家2 | `Reference/Cosmic/scripts/npc/2103010.js` |
| 2103011 | 民家4収納場所(透明) | 1: 260000205 民家4 | `Reference/Cosmic/scripts/npc/2103011.js` |
| 2103012 | 民家7収納場所(透明) | 1: 260000207 民家6 | `Reference/Cosmic/scripts/npc/2103012.js` |
| 2110002 | キオル | 1: 261000002 マレンの作業室 | `Reference/Cosmic/scripts/npc/2110002.js` |
| 2111000 | カソン | 1: 261000010 ジェニミスト協会 | `Reference/Cosmic/scripts/npc/2111000.js` |
| 2111001 | マッド | 1: 261000020 アルカドノ協会 | `Reference/Cosmic/scripts/npc/2111001.js` |
| 2111003 | ヒュモノイドA | 1: 261000000 マガティア | `Reference/Cosmic/scripts/npc/2111003.js` |
| 2111004 | フィリア | 1: 261000000 マガティア | `Reference/Cosmic/scripts/npc/2111004.js` |
| 2111005 | キニ | 1: 261000000 マガティア | `Reference/Cosmic/scripts/npc/2111005.js` |
| 2111007 | ブローカーハン | 1: 261000000 マガティア | `Reference/Cosmic/scripts/npc/2111007.js` |
| 2111008 | ベディン | 1: 261010000 研究所1階廊下 | `Reference/Cosmic/scripts/npc/2111008.js` |
| 2111009 | ラセルロン | 1: 261020000 研究所中央ゲート | `Reference/Cosmic/scripts/npc/2111009.js` |
| 2111010 | アルカドノの本棚 | 1: 926120000 光が消えた研究室 | `Reference/Cosmic/scripts/npc/2111010.js` |
| 2111011 | 失踪した錬金術師の家の壁(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111011.js` |
| 2111012 | 失踪した錬金術師の家の本棚(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111012.js` |
| 2111013 | 失踪した錬金術師の家の額縁(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111013.js` |
| 2111014 | 失踪した錬金術師の家の机(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111014.js` |
| 2111016 | ドランの秘密本 | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111016.js` |
| 2111017 | 一番目のパイプ取っ手(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111017.js` |
| 2111018 | 二番目のパイプ取っ手(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111018.js` |
| 2111019 | 三番目のパイプ取っ手(透明) | 1: 261000001 失踪した錬金術師の家 | `Reference/Cosmic/scripts/npc/2111019.js` |
| 2111020 | 一番目の魔法陣(透明) | 1: 261040000 暗黒の魔法使いの研究室 | `Reference/Cosmic/scripts/npc/2111020.js` |
| 2111021 | 二番目の魔法陣(透明) | 1: 261040000 暗黒の魔法使いの研究室 | `Reference/Cosmic/scripts/npc/2111021.js` |
| 2111022 | 三番目の魔法陣(透明) | 1: 261040000 暗黒の魔法使いの研究室 | `Reference/Cosmic/scripts/npc/2111022.js` |
| 2111023 | 魔法陣中央(透明) | 1: 261040000 暗黒の魔法使いの研究室 | `Reference/Cosmic/scripts/npc/2111023.js` |
| 2112016 | 隠された文書 | 1: 926130102 ユレテの実験室2 | `Reference/Cosmic/scripts/npc/2112016.js` |
| 2120003 | メイド | 1: 229000000 中央ホール | `Reference/Cosmic/scripts/npc/2120003.js` |
| 2131000 | ヘレナ | 1: 300000010 キャンプ会議場 | `Reference/Cosmic/scripts/npc/2131000.js` |
| 2131001 | ペルゼン | 1: 300000000 森のキャンプ | `Reference/Cosmic/scripts/npc/2131001.js` |
| 2131002 | ユリス | 1: 300000000 森のキャンプ | `Reference/Cosmic/scripts/npc/2131002.js` |
| 2131003 | ロハ | 1: 300000000 森のキャンプ | `Reference/Cosmic/scripts/npc/2131003.js` |
| 2131004 | 寝ている赤ちゃん | 1: 300000002 テント2 | `Reference/Cosmic/scripts/npc/2131004.js` |
| 2131005 | シオン | 1: 300000001 テント1 | `Reference/Cosmic/scripts/npc/2131005.js` |
| 2131006 | ドル | 1: 300000000 森のキャンプ | `Reference/Cosmic/scripts/npc/2131006.js` |
| 2131007 | テス | 1: 300000000 森のキャンプ | `Reference/Cosmic/scripts/npc/2131007.js` |
| 2133000 | エリン | 1: 300030100 深い妖精の森 | `Reference/Cosmic/scripts/npc/2133000.js` |
| 2133002 | エリン森道しるべ | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/npc/2133002.js` |
| 2133004 | スプライト | 1: 930000500 森の広場 | `Reference/Cosmic/scripts/npc/2133004.js` |
| 2141001 | 忘れられた神殿管理人 | 1: 270050000 忘れられた黄昏 | `Reference/Cosmic/scripts/npc/2141001.js` |
| 2141002 | 忘れられた神殿管理人 | 1: 270050200 失われた黄昏 | `Reference/Cosmic/scripts/npc/2141002.js` |
| 9000000 | ポル | 1: 261000000 マガティア | `Reference/Cosmic/scripts/npc/9000000.js` |
| 9000001 | ジャング | 1: 104000000 港口 | `Reference/Cosmic/scripts/npc/9000001.js` |
| 9000004 | バイコン | 1: 109010100 東フィールド | `Reference/Cosmic/scripts/npc/9000004.js` |
| 9000008 | 開け屋 | 1: 103000000 カニングシティー | `Reference/Cosmic/scripts/npc/9000008.js` |
| 9000009 | バイキン | 1: 104000000 港口 | `Reference/Cosmic/scripts/npc/9000009.js` |
| 9000010 | ピエトラ | 1: 109050001 イベント出口 | `Reference/Cosmic/scripts/npc/9000010.js` |
| 9000011 | マティン | 1: 200000000 オルビス | `Reference/Cosmic/scripts/npc/9000011.js` |
| 9000013 | トニ | 1: 220000000 ルディブリアム | `Reference/Cosmic/scripts/npc/9000013.js` |
| 9040000 | モニカ | 1: 101030104 遺跡発掘ベースキャンプ | `Reference/Cosmic/scripts/npc/9040000.js` |
| 9040001 | ヌリス | 1: 990001100 帰り道 | `Reference/Cosmic/scripts/npc/9040001.js` |
| 9040002 | シャン | 1: 101030104 遺跡発掘ベースキャンプ | `Reference/Cosmic/scripts/npc/9040002.js` |
| 9040007 | シャレン3世の遺言書 | 1: 990000600 地下水路 | `Reference/Cosmic/scripts/npc/9040007.js` |
| 9040008 | ギルドランキング掲示板 | 1: 101030104 遺跡発掘ベースキャンプ | `Reference/Cosmic/scripts/npc/9040008.js` |
| 9040009 | ライオン像 | 1: 990000300 シャレニアン城門 | `Reference/Cosmic/scripts/npc/9040009.js` |
| 9040010 | キメラ像 | 1: 990000900 エレゴスの王子 | `Reference/Cosmic/scripts/npc/9040010.js` |
| 9040012 | 騎士鎧 | 1: 990000400 騎士のホール | `Reference/Cosmic/scripts/npc/9040012.js` |
| 9102100 | ? | 1: 100000202 ペットの散歩路 | `Reference/Cosmic/scripts/npc/9102100.js` |
| 9102101 | ? | 1: 100000202 ペットの散歩路 | `Reference/Cosmic/scripts/npc/9102101.js` |
| 9103000 | ピエトル | 1: 809050015 迷路 | `Reference/Cosmic/scripts/npc/9103000.js` |
| 9103001 | ガイドモモ | 1: 220000000 ルディブリアム | `Reference/Cosmic/scripts/npc/9103001.js` |
| 9103002 | ガイドララ | 1: 809050016 商品交換所 | `Reference/Cosmic/scripts/npc/9103002.js` |
| 9103003 | ガイドルル | 1: 809050017 イベント出口 | `Reference/Cosmic/scripts/npc/9103003.js` |
| 9110002 | 木野子のこ | 1: 800000000 キノコ神社 | `Reference/Cosmic/scripts/npc/9110002.js` |
| 9120003 | ヒカリ | 1: 801000000 ショーワ町 | `Reference/Cosmic/scripts/npc/9120003.js` |
| 9120010 | ファイト | 1: 801000300 ショーワ町通り | `Reference/Cosmic/scripts/npc/9120010.js` |
| 9120013 | ボス猫 | 1: 801000000 ショーワ町 | `Reference/Cosmic/scripts/npc/9120013.js` |
| 9120015 | コンペイ | 1: 801000000 ショーワ町 | `Reference/Cosmic/scripts/npc/9120015.js` |
| 9120023 | ヨコヨコ | 1: 801000300 ショーワ町通り | `Reference/Cosmic/scripts/npc/9120023.js` |
| 9120101 | 助手みどり | 1: 801000001 美容院 | `Reference/Cosmic/scripts/npc/9120101.js` |
| 9120102 | ヒゲクロ先生 | 1: 801000002 整形外科 | `Reference/Cosmic/scripts/npc/9120102.js` |
| 9120103 | 助手サエコ | 1: 801000002 整形外科 | `Reference/Cosmic/scripts/npc/9120103.js` |
| 9120200 | コンペイ | 1: 801040000 アジト前 | `Reference/Cosmic/scripts/npc/9120200.js` |
| 9120203 | コンペイ | 1: 801040101 アジト前(天晴れ) | `Reference/Cosmic/scripts/npc/9120203.js` |
| 9201000 | ムーニ | 1: 680000000 ウェディングタウン | `Reference/Cosmic/scripts/npc/9201000.js` |
| 9201004 | 聖賢エームズ | 1: 680000000 ウェディングタウン | `Reference/Cosmic/scripts/npc/9201004.js` |
| 9201007 | ナンシー | 1: 680000210 ウェディング(大聖堂) | `Reference/Cosmic/scripts/npc/9201007.js` |
| 9201010 | トラヴィス | 1: 889300101 ウェディング(ハウスウェディング) | `Reference/Cosmic/scripts/npc/9201010.js` |
| 9201011 | ビバップ | 1: 889300101 ウェディング(ハウスウェディング) | `Reference/Cosmic/scripts/npc/9201011.js` |
| 9201012 | ウェイン | 1: 680000000 ウェディングタウン | `Reference/Cosmic/scripts/npc/9201012.js` |
| 9201013 | ビクトリア | 1: 680000000 ウェディングタウン | `Reference/Cosmic/scripts/npc/9201013.js` |
| 9201014 | ピーラ | 1: 680000000 ウェディングタウン | `Reference/Cosmic/scripts/npc/9201014.js` |
| 9201015 | ジュリアス | 1: 680000002 美容院 | `Reference/Cosmic/scripts/npc/9201015.js` |
| 9201016 | シェイマス | 1: 680000002 美容院 | `Reference/Cosmic/scripts/npc/9201016.js` |
| 9201018 | アルバーツ | 1: 680000003 整形外科 | `Reference/Cosmic/scripts/npc/9201018.js` |
| 9201019 | シャキ | 1: 680000003 整形外科 | `Reference/Cosmic/scripts/npc/9201019.js` |
| 9201051 | ジョン・バリケード | 1: 104000000 港口 | `Reference/Cosmic/scripts/npc/9201051.js` |
| 9201052 | フォックスウィット | 1: 104000000 港口 | `Reference/Cosmic/scripts/npc/9201052.js` |
| 9201082 | ペティト | 1: 801000000 ショーワ町 | `Reference/Cosmic/scripts/npc/9201082.js` |
| 9201083 | グリマー・マン | 1: 221000000 地球防衛本部 | `Reference/Cosmic/scripts/npc/9201083.js` |
| 9201094 | コリーン | 1: 240000000 リプレ | `Reference/Cosmic/scripts/npc/9201094.js` |
| 9201098 | ルーカン
 | 1: 803000101 畏れの森 | `Reference/Cosmic/scripts/npc/9201098.js` |
| 9201099 | フォウ
 | 1: 803000205 侵食の沼 | `Reference/Cosmic/scripts/npc/9201099.js` |
| 9201113 | ジャック | 1: 803000510 遠征隊(入場) ‐修練の道- | `Reference/Cosmic/scripts/npc/9201113.js` |
| 9201114 | ジャック | 1: 803000520 遠征隊(入場) ‐挑戦者の道- | `Reference/Cosmic/scripts/npc/9201114.js` |
| 9201115 | 戦女神の像 | 1: 803100000 支配者の秘密の間-孤高の戦場- | `Reference/Cosmic/scripts/npc/9201115.js` |
| 9250045 | ぺリオン電光板 | 1: 102000000 ぺリオン | `Reference/Cosmic/scripts/npc/9250045.js` |
| 9900000 | KIN | 1: 180000000 撮影現場 | `Reference/Cosmic/scripts/npc/9900000.js` |
| 9900001 | NimaKIN | 1: 180000000 撮影現場 | `Reference/Cosmic/scripts/npc/9900001.js` |
| 1012114 | タイクン | 0:  | `Reference/Cosmic/scripts/npc/1012114.js` |
| 1012115 | ヘネシス草むら | 0:  | `Reference/Cosmic/scripts/npc/1012115.js` |
| 1012118 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1012118.js` |
| 1022101 | ルニ | 0:  | `Reference/Cosmic/scripts/npc/1022101.js` |
| 1022104 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1022104.js` |
| 1022105 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1022105.js` |
| 1032113 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1032113.js` |
| 1032114 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1032114.js` |
| 1040000 | ルーク | 0:  | `Reference/Cosmic/scripts/npc/1040000.js` |
| 1052008 | 宝箱 | 0:  | `Reference/Cosmic/scripts/npc/1052008.js` |
| 1052009 | 宝箱 | 0:  | `Reference/Cosmic/scripts/npc/1052009.js` |
| 1052010 | 宝箱 | 0:  | `Reference/Cosmic/scripts/npc/1052010.js` |
| 1052107 | 小さな街灯 | 0:  | `Reference/Cosmic/scripts/npc/1052107.js` |
| 1052110 | 地下鉄のゴミ箱 | 0:  | `Reference/Cosmic/scripts/npc/1052110.js` |
| 1052113 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1052113.js` |
| 1052114 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1052114.js` |
| 1052125 | ガードマン | 0:  | `Reference/Cosmic/scripts/npc/1052125.js` |
| 1061010 | 光る水晶 | 0:  | `Reference/Cosmic/scripts/npc/1061010.js` |
| 1063012 | 霊験な石1 | 0:  | `Reference/Cosmic/scripts/npc/1063012.js` |
| 1072004 | 戦士転職教官 | 0:  | `Reference/Cosmic/scripts/npc/1072004.js` |
| 1072005 | 魔法使い転職教官 | 0:  | `Reference/Cosmic/scripts/npc/1072005.js` |
| 1072006 | 弓使い転職教官 | 0:  | `Reference/Cosmic/scripts/npc/1072006.js` |
| 1072007 | 盗賊転職教官 | 0:  | `Reference/Cosmic/scripts/npc/1072007.js` |
| 1095000 | シュリンツ | 0:  | `Reference/Cosmic/scripts/npc/1095000.js` |
| 1095001 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1095001.js` |
| 1095002 | ヘンケル | 0:  | `Reference/Cosmic/scripts/npc/1095002.js` |
| 1101008 | シグネット | 0:  | `Reference/Cosmic/scripts/npc/1101008.js` |
| 1102001 | キリコ | 0:  | `Reference/Cosmic/scripts/npc/1102001.js` |
| 1104002 | エレオノール | 0:  | `Reference/Cosmic/scripts/npc/1104002.js` |
| 1104100 | ミハエル(偽者) | 0:  | `Reference/Cosmic/scripts/npc/1104100.js` |
| 1104101 | オズ(偽者) | 0:  | `Reference/Cosmic/scripts/npc/1104101.js` |
| 1104102 | イリーナ(偽者) | 0:  | `Reference/Cosmic/scripts/npc/1104102.js` |
| 1104103 | イカルト(偽者) | 0:  | `Reference/Cosmic/scripts/npc/1104103.js` |
| 1104104 | ホークアイ(偽者) | 0:  | `Reference/Cosmic/scripts/npc/1104104.js` |
| 1202009 | 番人ヴォルフ | 0:  | `Reference/Cosmic/scripts/npc/1202009.js` |
| 2001000 | クリッフ | 0:  | `Reference/Cosmic/scripts/npc/2001000.js` |
| 2001001 | 木の手雪だるま | 0:  | `Reference/Cosmic/scripts/npc/2001001.js` |
| 2001002 | バケツ雪だるま | 0:  | `Reference/Cosmic/scripts/npc/2001002.js` |
| 2001003 | 麦わら帽子雪だるま | 0:  | `Reference/Cosmic/scripts/npc/2001003.js` |
| 2001004 | マフラー雪だるま | 0:  | `Reference/Cosmic/scripts/npc/2001004.js` |
| 2002000 | ルピ | 0:  | `Reference/Cosmic/scripts/npc/2002000.js` |
| 2040003 | ハカセくん | 0:  | `Reference/Cosmic/scripts/npc/2040003.js` |
| 2040022 | ライドル | 0:  | `Reference/Cosmic/scripts/npc/2040022.js` |
| 2040025 | 二番目のエオス石 | 0:  | `Reference/Cosmic/scripts/npc/2040025.js` |
| 2040027 | 四番目のエオス石 | 0:  | `Reference/Cosmic/scripts/npc/2040027.js` |
| 2040028 | マークくん | 0:  | `Reference/Cosmic/scripts/npc/2040028.js` |
| 2041017 | 製菓師クッカース | 0:  | `Reference/Cosmic/scripts/npc/2041017.js` |
| 2041026 | ゴーストハンターボブ | 0:  | `Reference/Cosmic/scripts/npc/2041026.js` |
| 2042009 | 助手ブルー | 0:  | `Reference/Cosmic/scripts/npc/2042009.js` |
| 2050014 | 隕石1 | 0:  | `Reference/Cosmic/scripts/npc/2050014.js` |
| 2050015 | 隕石2 | 0:  | `Reference/Cosmic/scripts/npc/2050015.js` |
| 2050016 | 隕石3 | 0:  | `Reference/Cosmic/scripts/npc/2050016.js` |
| 2050017 | 隕石4 | 0:  | `Reference/Cosmic/scripts/npc/2050017.js` |
| 2050018 | 隕石5 | 0:  | `Reference/Cosmic/scripts/npc/2050018.js` |
| 2050019 | 隕石6 | 0:  | `Reference/Cosmic/scripts/npc/2050019.js` |
| 2060005 | ケンタ | 0:  | `Reference/Cosmic/scripts/npc/2060005.js` |
| 2081009 | ムス | 0:  | `Reference/Cosmic/scripts/npc/2081009.js` |
| 2082004 | 앤디 | 0:  | `Reference/Cosmic/scripts/npc/2082004.js` |
| 2082014 | 아아시아 | 0:  | `Reference/Cosmic/scripts/npc/2082014.js` |
| 2083006 | 타임 게이트 | 0:  | `Reference/Cosmic/scripts/npc/2083006.js` |
| 2091009 | 封印された社員入口 | 0:  | `Reference/Cosmic/scripts/npc/2091009.js` |
| 2096000 | 練習記録帳 | 0:  | `Reference/Cosmic/scripts/npc/2096000.js` |
| 2101006 | プリンス | 0:  | `Reference/Cosmic/scripts/npc/2101006.js` |
| 2101013 | カルタサ | 0:  | `Reference/Cosmic/scripts/npc/2101013.js` |
| 2110005 | ラクダ | 0:  | `Reference/Cosmic/scripts/npc/2110005.js` |
| 2111006 | ファウェン | 0:  | `Reference/Cosmic/scripts/npc/2111006.js` |
| 2111015 | ラセルロンの机(透明) | 0:  | `Reference/Cosmic/scripts/npc/2111015.js` |
| 2111025 | 制御装置 | 0:  | `Reference/Cosmic/scripts/npc/2111025.js` |
| 2111026 | 未完成魔法陣 | 0:  | `Reference/Cosmic/scripts/npc/2111026.js` |
| 2112000 | ユレテ | 0:  | `Reference/Cosmic/scripts/npc/2112000.js` |
| 2112001 | ユレテ | 0:  | `Reference/Cosmic/scripts/npc/2112001.js` |
| 2112005 | ジュリエット(進行) | 0:  | `Reference/Cosmic/scripts/npc/2112005.js` |
| 2112006 | ロミオ(進行) | 0:  | `Reference/Cosmic/scripts/npc/2112006.js` |
| 2112010 | ユレテ | 0:  | `Reference/Cosmic/scripts/npc/2112010.js` |
| 2112011 | ユレテ | 0:  | `Reference/Cosmic/scripts/npc/2112011.js` |
| 2112018 | ロミオとジュリエット | 0:  | `Reference/Cosmic/scripts/npc/2112018.js` |
| 2132000 | カンデルン | 0:  | `Reference/Cosmic/scripts/npc/2132000.js` |
| 2132001 | ロード | 0:  | `Reference/Cosmic/scripts/npc/2132001.js` |
| 2132002 | リオス | 0:  | `Reference/Cosmic/scripts/npc/2132002.js` |
| 2132003 | シャドリオン | 0:  | `Reference/Cosmic/scripts/npc/2132003.js` |
| 2141000 | キルストン | 0:  | `Reference/Cosmic/scripts/npc/2141000.js` |
| 9000007 | 天地 | 0:  | `Reference/Cosmic/scripts/npc/9000007.js` |
| 9000017 | ココ | 0:  | `Reference/Cosmic/scripts/npc/9000017.js` |
| 9000036 | 要員E | 0:  | `Reference/Cosmic/scripts/npc/9000036.js` |
| 9000037 | 要員ニャニャ | 0:  | `Reference/Cosmic/scripts/npc/9000037.js` |
| 9000038 | 要員ニャ | 0:  | `Reference/Cosmic/scripts/npc/9000038.js` |
| 9010001 | ティア | 0:  | `Reference/Cosmic/scripts/npc/9010001.js` |
| 9010002 | ピア | 0:  | `Reference/Cosmic/scripts/npc/9010002.js` |
| 9010003 | リア | 0:  | `Reference/Cosmic/scripts/npc/9010003.js` |
| 9010004 | ミア | 0:  | `Reference/Cosmic/scripts/npc/9010004.js` |
| 9010021 | オオカミの精霊のヴォルフ | 0:  | `Reference/Cosmic/scripts/npc/9010021.js` |
| 9040003 | シャレン3世の霊魂 | 0:  | `Reference/Cosmic/scripts/npc/9040003.js` |
| 9040006 | 正邪の彫刻 | 0:  | `Reference/Cosmic/scripts/npc/9040006.js` |
| 9060000 | ケンタ | 0:  | `Reference/Cosmic/scripts/npc/9060000.js` |
| 9101001 | ピーター | 0:  | `Reference/Cosmic/scripts/npc/9101001.js` |
| 9120201 | コンペイ | 0:  | `Reference/Cosmic/scripts/npc/9120201.js` |
| 9120202 | コンペイ | 0:  | `Reference/Cosmic/scripts/npc/9120202.js` |
| 9201001 | ナナ | 0:  | `Reference/Cosmic/scripts/npc/9201001.js` |
| 9201003 | パパ&amp;ママ | 0:  | `Reference/Cosmic/scripts/npc/9201003.js` |
| 9201017 | ロバーツ | 0:  | `Reference/Cosmic/scripts/npc/9201017.js` |
| 9201024 | リリ | 0:  | `Reference/Cosmic/scripts/npc/9201024.js` |
| 9201025 | ナナ(O) | 0:  | `Reference/Cosmic/scripts/npc/9201025.js` |
| 9201026 | ミミ | 0:  | `Reference/Cosmic/scripts/npc/9201026.js` |
| 9201027 | ナナ(P) | 0:  | `Reference/Cosmic/scripts/npc/9201027.js` |
| 9201095 | フィオナ | 0:  | `Reference/Cosmic/scripts/npc/9201095.js` |
| 9201096 | ジャック | 0:  | `Reference/Cosmic/scripts/npc/9201096.js` |
| 9201097 | ジョコ | 0:  | `Reference/Cosmic/scripts/npc/9201097.js` |
| 9201100 | タッガーリン
 | 0:  | `Reference/Cosmic/scripts/npc/9201100.js` |
| 9201103 | リドリー | 0:  | `Reference/Cosmic/scripts/npc/9201103.js` |
| 9201104 | 賢者 | 0:  | `Reference/Cosmic/scripts/npc/9201104.js` |
| 9201105 | 賢者 | 0:  | `Reference/Cosmic/scripts/npc/9201105.js` |
| 9201106 | Adonis | 0:  | `Reference/Cosmic/scripts/npc/9201106.js` |
| 9220004 | ジョイス | 0:  | `Reference/Cosmic/scripts/npc/9220004.js` |
| 9220005 | ルドルフ | 0:  | `Reference/Cosmic/scripts/npc/9220005.js` |

## Quest scripts Cosmic has for quests that exist in JMS

| quest | Cosmic file |
|---|---|
| 1021 | `Reference/Cosmic/scripts/quest/1021.js` |
| 2001 | `Reference/Cosmic/scripts/quest/2001.js` |
| 2034 | `Reference/Cosmic/scripts/quest/2034.js` |
| 2124 | `Reference/Cosmic/scripts/quest/2124.js` |
| 2126 | `Reference/Cosmic/scripts/quest/2126.js` |
| 2127 | `Reference/Cosmic/scripts/quest/2127.js` |
| 2148 | `Reference/Cosmic/scripts/quest/2148.js` |
| 2149 | `Reference/Cosmic/scripts/quest/2149.js` |
| 2150 | `Reference/Cosmic/scripts/quest/2150.js` |
| 2151 | `Reference/Cosmic/scripts/quest/2151.js` |
| 2152 | `Reference/Cosmic/scripts/quest/2152.js` |
| 2186 | `Reference/Cosmic/scripts/quest/2186.js` |
| 2197 | `Reference/Cosmic/scripts/quest/2197.js` |
| 2214 | `Reference/Cosmic/scripts/quest/2214.js` |
| 2215 | `Reference/Cosmic/scripts/quest/2215.js` |
| 2216 | `Reference/Cosmic/scripts/quest/2216.js` |
| 2217 | `Reference/Cosmic/scripts/quest/2217.js` |
| 2218 | `Reference/Cosmic/scripts/quest/2218.js` |
| 2219 | `Reference/Cosmic/scripts/quest/2219.js` |
| 2228 | `Reference/Cosmic/scripts/quest/2228.js` |
| 2230 | `Reference/Cosmic/scripts/quest/2230.js` |
| 2232 | `Reference/Cosmic/scripts/quest/2232.js` |
| 2238 | `Reference/Cosmic/scripts/quest/2238.js` |
| 2245 | `Reference/Cosmic/scripts/quest/2245.js` |
| 2251 | `Reference/Cosmic/scripts/quest/2251.js` |
| 2257 | `Reference/Cosmic/scripts/quest/2257.js` |
| 2258 | `Reference/Cosmic/scripts/quest/2258.js` |
| 2259 | `Reference/Cosmic/scripts/quest/2259.js` |
| 2260 | `Reference/Cosmic/scripts/quest/2260.js` |
| 2300 | `Reference/Cosmic/scripts/quest/2300.js` |
| 2301 | `Reference/Cosmic/scripts/quest/2301.js` |
| 2302 | `Reference/Cosmic/scripts/quest/2302.js` |
| 2303 | `Reference/Cosmic/scripts/quest/2303.js` |
| 2304 | `Reference/Cosmic/scripts/quest/2304.js` |
| 2305 | `Reference/Cosmic/scripts/quest/2305.js` |
| 2306 | `Reference/Cosmic/scripts/quest/2306.js` |
| 2307 | `Reference/Cosmic/scripts/quest/2307.js` |
| 2308 | `Reference/Cosmic/scripts/quest/2308.js` |
| 2309 | `Reference/Cosmic/scripts/quest/2309.js` |
| 2310 | `Reference/Cosmic/scripts/quest/2310.js` |
| 2312 | `Reference/Cosmic/scripts/quest/2312.js` |
| 2313 | `Reference/Cosmic/scripts/quest/2313.js` |
| 2314 | `Reference/Cosmic/scripts/quest/2314.js` |
| 2315 | `Reference/Cosmic/scripts/quest/2315.js` |
| 2316 | `Reference/Cosmic/scripts/quest/2316.js` |
| 2317 | `Reference/Cosmic/scripts/quest/2317.js` |
| 2318 | `Reference/Cosmic/scripts/quest/2318.js` |
| 2319 | `Reference/Cosmic/scripts/quest/2319.js` |
| 2320 | `Reference/Cosmic/scripts/quest/2320.js` |
| 2321 | `Reference/Cosmic/scripts/quest/2321.js` |
| 2322 | `Reference/Cosmic/scripts/quest/2322.js` |
| 2325 | `Reference/Cosmic/scripts/quest/2325.js` |
| 2327 | `Reference/Cosmic/scripts/quest/2327.js` |
| 2332 | `Reference/Cosmic/scripts/quest/2332.js` |
| 2333 | `Reference/Cosmic/scripts/quest/2333.js` |
| 2334 | `Reference/Cosmic/scripts/quest/2334.js` |
| 2335 | `Reference/Cosmic/scripts/quest/2335.js` |
| 2338 | `Reference/Cosmic/scripts/quest/2338.js` |
| 2342 | `Reference/Cosmic/scripts/quest/2342.js` |
| 3108 | `Reference/Cosmic/scripts/quest/3108.js` |
| 3239 | `Reference/Cosmic/scripts/quest/3239.js` |
| 3301 | `Reference/Cosmic/scripts/quest/3301.js` |
| 3303 | `Reference/Cosmic/scripts/quest/3303.js` |
| 3305 | `Reference/Cosmic/scripts/quest/3305.js` |
| 3306 | `Reference/Cosmic/scripts/quest/3306.js` |
| 3314 | `Reference/Cosmic/scripts/quest/3314.js` |
| 3320 | `Reference/Cosmic/scripts/quest/3320.js` |
| 3321 | `Reference/Cosmic/scripts/quest/3321.js` |
| 3353 | `Reference/Cosmic/scripts/quest/3353.js` |
| 3354 | `Reference/Cosmic/scripts/quest/3354.js` |
| 3360 | `Reference/Cosmic/scripts/quest/3360.js` |
| 3382 | `Reference/Cosmic/scripts/quest/3382.js` |
| 3414 | `Reference/Cosmic/scripts/quest/3414.js` |
| 3437 | `Reference/Cosmic/scripts/quest/3437.js` |
| 3452 | `Reference/Cosmic/scripts/quest/3452.js` |
| 3454 | `Reference/Cosmic/scripts/quest/3454.js` |
| 3507 | `Reference/Cosmic/scripts/quest/3507.js` |
| 3514 | `Reference/Cosmic/scripts/quest/3514.js` |
| 3523 | `Reference/Cosmic/scripts/quest/3523.js` |
| 3524 | `Reference/Cosmic/scripts/quest/3524.js` |
| 3525 | `Reference/Cosmic/scripts/quest/3525.js` |
| 3526 | `Reference/Cosmic/scripts/quest/3526.js` |
| 3527 | `Reference/Cosmic/scripts/quest/3527.js` |
| 3529 | `Reference/Cosmic/scripts/quest/3529.js` |
| 3539 | `Reference/Cosmic/scripts/quest/3539.js` |
| 3714 | `Reference/Cosmic/scripts/quest/3714.js` |
| 3833 | `Reference/Cosmic/scripts/quest/3833.js` |
| 3933 | `Reference/Cosmic/scripts/quest/3933.js` |
| 3941 | `Reference/Cosmic/scripts/quest/3941.js` |
| 3953 | `Reference/Cosmic/scripts/quest/3953.js` |
| 4647 | `Reference/Cosmic/scripts/quest/4647.js` |
| 4659 | `Reference/Cosmic/scripts/quest/4659.js` |
| 6030 | `Reference/Cosmic/scripts/quest/6030.js` |
| 6031 | `Reference/Cosmic/scripts/quest/6031.js` |
| 6032 | `Reference/Cosmic/scripts/quest/6032.js` |
| 6033 | `Reference/Cosmic/scripts/quest/6033.js` |
| 6036 | `Reference/Cosmic/scripts/quest/6036.js` |
| 7103 | `Reference/Cosmic/scripts/quest/7103.js` |
| 8185 | `Reference/Cosmic/scripts/quest/8185.js` |
| 8189 | `Reference/Cosmic/scripts/quest/8189.js` |
| 20000 | `Reference/Cosmic/scripts/quest/20000.js` |
| 20001 | `Reference/Cosmic/scripts/quest/20001.js` |
| 20002 | `Reference/Cosmic/scripts/quest/20002.js` |
| 20008 | `Reference/Cosmic/scripts/quest/20008.js` |
| 20010 | `Reference/Cosmic/scripts/quest/20010.js` |
| 20011 | `Reference/Cosmic/scripts/quest/20011.js` |
| 20012 | `Reference/Cosmic/scripts/quest/20012.js` |
| 20013 | `Reference/Cosmic/scripts/quest/20013.js` |
| 20016 | `Reference/Cosmic/scripts/quest/20016.js` |
| 20017 | `Reference/Cosmic/scripts/quest/20017.js` |
| 20020 | `Reference/Cosmic/scripts/quest/20020.js` |
| 20100 | `Reference/Cosmic/scripts/quest/20100.js` |
| 20101 | `Reference/Cosmic/scripts/quest/20101.js` |
| 20102 | `Reference/Cosmic/scripts/quest/20102.js` |
| 20103 | `Reference/Cosmic/scripts/quest/20103.js` |
| 20104 | `Reference/Cosmic/scripts/quest/20104.js` |
| 20105 | `Reference/Cosmic/scripts/quest/20105.js` |
| 20200 | `Reference/Cosmic/scripts/quest/20200.js` |
| 20201 | `Reference/Cosmic/scripts/quest/20201.js` |
| 20202 | `Reference/Cosmic/scripts/quest/20202.js` |
| 20203 | `Reference/Cosmic/scripts/quest/20203.js` |
| 20204 | `Reference/Cosmic/scripts/quest/20204.js` |
| 20205 | `Reference/Cosmic/scripts/quest/20205.js` |
| 20311 | `Reference/Cosmic/scripts/quest/20311.js` |
| 20312 | `Reference/Cosmic/scripts/quest/20312.js` |
| 20313 | `Reference/Cosmic/scripts/quest/20313.js` |
| 20314 | `Reference/Cosmic/scripts/quest/20314.js` |
| 20315 | `Reference/Cosmic/scripts/quest/20315.js` |
| 20400 | `Reference/Cosmic/scripts/quest/20400.js` |
| 20401 | `Reference/Cosmic/scripts/quest/20401.js` |
| 20405 | `Reference/Cosmic/scripts/quest/20405.js` |
| 20406 | `Reference/Cosmic/scripts/quest/20406.js` |
| 20408 | `Reference/Cosmic/scripts/quest/20408.js` |
| 20520 | `Reference/Cosmic/scripts/quest/20520.js` |
| 20522 | `Reference/Cosmic/scripts/quest/20522.js` |
| 20526 | `Reference/Cosmic/scripts/quest/20526.js` |
| 20527 | `Reference/Cosmic/scripts/quest/20527.js` |
| 20600 | `Reference/Cosmic/scripts/quest/20600.js` |
| 20610 | `Reference/Cosmic/scripts/quest/20610.js` |
| 20700 | `Reference/Cosmic/scripts/quest/20700.js` |
| 20710 | `Reference/Cosmic/scripts/quest/20710.js` |
| 20720 | `Reference/Cosmic/scripts/quest/20720.js` |
| 21000 | `Reference/Cosmic/scripts/quest/21000.js` |
| 21001 | `Reference/Cosmic/scripts/quest/21001.js` |
| 21010 | `Reference/Cosmic/scripts/quest/21010.js` |
| 21011 | `Reference/Cosmic/scripts/quest/21011.js` |
| 21012 | `Reference/Cosmic/scripts/quest/21012.js` |
| 21013 | `Reference/Cosmic/scripts/quest/21013.js` |
| 21015 | `Reference/Cosmic/scripts/quest/21015.js` |
| 21016 | `Reference/Cosmic/scripts/quest/21016.js` |
| 21017 | `Reference/Cosmic/scripts/quest/21017.js` |
| 21018 | `Reference/Cosmic/scripts/quest/21018.js` |
| 21100 | `Reference/Cosmic/scripts/quest/21100.js` |
| 21101 | `Reference/Cosmic/scripts/quest/21101.js` |
| 21200 | `Reference/Cosmic/scripts/quest/21200.js` |
| 21201 | `Reference/Cosmic/scripts/quest/21201.js` |
| 21202 | `Reference/Cosmic/scripts/quest/21202.js` |
| 21300 | `Reference/Cosmic/scripts/quest/21300.js` |
| 21301 | `Reference/Cosmic/scripts/quest/21301.js` |
| 21302 | `Reference/Cosmic/scripts/quest/21302.js` |
| 21303 | `Reference/Cosmic/scripts/quest/21303.js` |
| 21400 | `Reference/Cosmic/scripts/quest/21400.js` |
| 21401 | `Reference/Cosmic/scripts/quest/21401.js` |
| 21600 | `Reference/Cosmic/scripts/quest/21600.js` |
| 21604 | `Reference/Cosmic/scripts/quest/21604.js` |
| 21613 | `Reference/Cosmic/scripts/quest/21613.js` |
| 21618 | `Reference/Cosmic/scripts/quest/21618.js` |
| 21700 | `Reference/Cosmic/scripts/quest/21700.js` |
| 21703 | `Reference/Cosmic/scripts/quest/21703.js` |
| 21704 | `Reference/Cosmic/scripts/quest/21704.js` |
| 21712 | `Reference/Cosmic/scripts/quest/21712.js` |
| 21716 | `Reference/Cosmic/scripts/quest/21716.js` |
| 21719 | `Reference/Cosmic/scripts/quest/21719.js` |
| 21720 | `Reference/Cosmic/scripts/quest/21720.js` |
| 21729 | `Reference/Cosmic/scripts/quest/21729.js` |
| 21733 | `Reference/Cosmic/scripts/quest/21733.js` |
| 21734 | `Reference/Cosmic/scripts/quest/21734.js` |
| 21735 | `Reference/Cosmic/scripts/quest/21735.js` |
| 21736 | `Reference/Cosmic/scripts/quest/21736.js` |
| 21738 | `Reference/Cosmic/scripts/quest/21738.js` |
| 21739 | `Reference/Cosmic/scripts/quest/21739.js` |
| 21740 | `Reference/Cosmic/scripts/quest/21740.js` |
| 21741 | `Reference/Cosmic/scripts/quest/21741.js` |
| 21742 | `Reference/Cosmic/scripts/quest/21742.js` |
| 21746 | `Reference/Cosmic/scripts/quest/21746.js` |
| 21747 | `Reference/Cosmic/scripts/quest/21747.js` |
| 21748 | `Reference/Cosmic/scripts/quest/21748.js` |
| 21749 | `Reference/Cosmic/scripts/quest/21749.js` |
| 21750 | `Reference/Cosmic/scripts/quest/21750.js` |
| 21753 | `Reference/Cosmic/scripts/quest/21753.js` |
| 21754 | `Reference/Cosmic/scripts/quest/21754.js` |
| 21757 | `Reference/Cosmic/scripts/quest/21757.js` |
| 21766 | `Reference/Cosmic/scripts/quest/21766.js` |
| 21767 | `Reference/Cosmic/scripts/quest/21767.js` |
| 22000 | `Reference/Cosmic/scripts/quest/22000.js` |
| 22001 | `Reference/Cosmic/scripts/quest/22001.js` |
| 22002 | `Reference/Cosmic/scripts/quest/22002.js` |
| 22003 | `Reference/Cosmic/scripts/quest/22003.js` |
| 22004 | `Reference/Cosmic/scripts/quest/22004.js` |
| 22007 | `Reference/Cosmic/scripts/quest/22007.js` |
| 22008 | `Reference/Cosmic/scripts/quest/22008.js` |
| 22500 | `Reference/Cosmic/scripts/quest/22500.js` |
| 22501 | `Reference/Cosmic/scripts/quest/22501.js` |
| 22502 | `Reference/Cosmic/scripts/quest/22502.js` |
| 22503 | `Reference/Cosmic/scripts/quest/22503.js` |
| 22504 | `Reference/Cosmic/scripts/quest/22504.js` |
| 22507 | `Reference/Cosmic/scripts/quest/22507.js` |
| 29900 | `Reference/Cosmic/scripts/quest/29900.js` |
| 29901 | `Reference/Cosmic/scripts/quest/29901.js` |
| 29902 | `Reference/Cosmic/scripts/quest/29902.js` |
| 29903 | `Reference/Cosmic/scripts/quest/29903.js` |
| 29906 | `Reference/Cosmic/scripts/quest/29906.js` |
| 29907 | `Reference/Cosmic/scripts/quest/29907.js` |
| 29908 | `Reference/Cosmic/scripts/quest/29908.js` |
| 29909 | `Reference/Cosmic/scripts/quest/29909.js` |
| 29924 | `Reference/Cosmic/scripts/quest/29924.js` |
| 29925 | `Reference/Cosmic/scripts/quest/29925.js` |
| 29926 | `Reference/Cosmic/scripts/quest/29926.js` |
| 29927 | `Reference/Cosmic/scripts/quest/29927.js` |
| 29928 | `Reference/Cosmic/scripts/quest/29928.js` |

## Portal scripts JMS maps use, that Cosmic has and Cronus lacks

| script | JMS maps | Cosmic file |
|---|---|---|
| timeQuest | 16: 270010100 思い出の道1, 270010200 思い出の道2, 270010300 思い出の道3 … | `Reference/Cosmic/scripts/portal/timeQuest.js` |
| rankRoom | 8: 100000201 弓使い学院, 101000003 魔法図書館, 102000003 戦士の聖殿 … | `Reference/Cosmic/scripts/portal/rankRoom.js` |
| NextMap | 7: 980041000 魔女の塔1階, 980041100 魔女の塔2階, 980042000 魔女の塔1階 … | `Reference/Cosmic/scripts/portal/NextMap.js` |
| party3_roomout | 7: 920010200 散歩路, 920010300 倉庫, 920010400 休憩室 … | `Reference/Cosmic/scripts/portal/party3_roomout.js` |
| highposition | 5: 101010103 渓流&lt;バンジージャンプ台&gt;, 102000000 ぺリオン, 103000000 カニングシティー … | `Reference/Cosmic/scripts/portal/highposition.js` |
| hontale_Bopen | 5: 240050101 一番目の迷路部屋, 240050102 二番目の迷路部屋, 240050103 三番目の迷路部屋 … | `Reference/Cosmic/scripts/portal/hontale_Bopen.js` |
| tutorialNPC | 5: 100000201 弓使い学院, 101000003 魔法図書館, 102000003 戦士の聖殿 … | `Reference/Cosmic/scripts/portal/tutorialNPC.js` |
| jnr5_rp | 4: 926110301 実験室の通路1, 926110302 実験室の通路2, 926110303 実験室の通路3 … | `Reference/Cosmic/scripts/portal/jnr5_rp.js` |
| party6_stage | 4: 930000000 森の前, 930000010 森入口, 930000100 森の端 … | `Reference/Cosmic/scripts/portal/party6_stage.js` |
| rnj5_rp | 4: 926100301 実験室の通路1, 926100302 実験室の通路2, 926100303 実験室の通路3 … | `Reference/Cosmic/scripts/portal/rnj5_rp.js` |
| tutorquest | 4: 130030001 開始の森2, 130030002 開始の森3, 130030003 開始の森4 … | `Reference/Cosmic/scripts/portal/tutorquest.js` |
| ariant_queens | 3: 260000301 アリアント宮殿&lt;庭園&gt;, 260000302 アリアント宮殿&lt;廊下&gt;, 260000303 アリアント宮殿&lt;王室&gt; | `Reference/Cosmic/scripts/portal/ariant_queens.js` |
| party3_jailin | 3: 920010910 監獄１, 920010920 監獄2, 920010930 監獄3 | `Reference/Cosmic/scripts/portal/party3_jailin.js` |
| Depart_goBack01 | 2: 103040420 7階 8階 C区域, 103040430 7階 8階 D区域 | `Reference/Cosmic/scripts/portal/Depart_goBack01.js` |
| Depart_goFoward1 | 2: 103040410 7階 8階 B区域, 103040420 7階 8階 C区域 | `Reference/Cosmic/scripts/portal/Depart_goFoward1.js` |
| MD_drakeroom | 2: 105090311 冷たい揺りかご, 105090320 ドレイクの青い洞窟 | `Reference/Cosmic/scripts/portal/MD_drakeroom.js` |
| MD_error | 2: 261020300 研究所C-1区域, 261020301 禁断の実験室 | `Reference/Cosmic/scripts/portal/MD_error.js` |
| MD_golem | 2: 105040304 スリーピーダンジョン4, 105040320 崩れたゴーレムの城跡 | `Reference/Cosmic/scripts/portal/MD_golem.js` |
| MD_mushroom | 2: 105050100 アリの巣2, 105050101 キノコ栽培洞窟 | `Reference/Cosmic/scripts/portal/MD_mushroom.js` |
| MD_pig | 2: 100020000 東の草むら, 100020100 豚農場 | `Reference/Cosmic/scripts/portal/MD_pig.js` |
| MD_protect | 2: 240040520 壊れた龍の巣, 240040900 ニュート保護区域 | `Reference/Cosmic/scripts/portal/MD_protect.js` |
| MD_rabbit | 2: 221023400 エオス塔76階-90階, 221023401 ビートラビットの隠れ家 | `Reference/Cosmic/scripts/portal/MD_rabbit.js` |
| MD_remember | 2: 240040511 残された龍の巣, 240040800 復活する記憶 | `Reference/Cosmic/scripts/portal/MD_remember.js` |
| MD_roundTable | 2: 240020500 火と水の戦場, 240020501 ケンタウロスの円卓 | `Reference/Cosmic/scripts/portal/MD_roundTable.js` |
| MD_sand | 2: 260020600 サヘル地帯2, 260020630 砂塵の吹く丘 | `Reference/Cosmic/scripts/portal/MD_sand.js` |
| MD_treasure | 2: 251010402 赤鼻海賊団の盗掘2, 251010410 宝島の掠奪 | `Reference/Cosmic/scripts/portal/MD_treasure.js` |
| balog_end | 2: 105100301 バルログが消えた場所, 105100401 イージーモード_バルログが消えた場所 | `Reference/Cosmic/scripts/portal/balog_end.js` |
| elevator | 2: 222020100 ヘリオス塔2階, 222020200 ヘリオス塔99階 | `Reference/Cosmic/scripts/portal/elevator.js` |
| hontale_BR | 2: 240060000 試験の洞窟1, 240060100 試験の洞窟2 | `Reference/Cosmic/scripts/portal/hontale_BR.js` |
| inNix1 | 2: 240020101 グリフォンの森, 240020102 グリフォンの森2 | `Reference/Cosmic/scripts/portal/inNix1.js` |
| inNix2 | 2: 240020401 ドラゴンの森, 240020402 レッドドラゴンの森 | `Reference/Cosmic/scripts/portal/inNix2.js` |
| mc_out | 2: 980000000 モンスターカーニバル入口, 980030000 シュピゲルマンの事務室 | `Reference/Cosmic/scripts/portal/mc_out.js` |
| minar_elli | 2: 101010000 エリニアの北側フィールド, 240010100 ミナル西側の境界 | `Reference/Cosmic/scripts/portal/minar_elli.js` |
| rnj_exit | 2: 926100700 出口, 926110700 出口 | `Reference/Cosmic/scripts/portal/rnj_exit.js` |
| secretDoor | 2: 261010000 研究所1階廊下, 261020200 研究所B-1区域 | `Reference/Cosmic/scripts/portal/secretDoor.js` |
| undodraco | 2: 200090500 時間の神殿への道, 200090510 ミナルへの道 | `Reference/Cosmic/scripts/portal/undodraco.js` |
| Depart_ToKerning | 1: 103000310 カニングスクエア駅 | `Reference/Cosmic/scripts/portal/Depart_ToKerning.js` |
| Depart_goBack00 | 1: 103040420 7階 8階 C区域 | `Reference/Cosmic/scripts/portal/Depart_goBack00.js` |
| Depart_goFoward0 | 1: 103040410 7階 8階 B区域 | `Reference/Cosmic/scripts/portal/Depart_goFoward0.js` |
| Depart_topOut | 1: 103040400 7階 8階 A区域 | `Reference/Cosmic/scripts/portal/Depart_topOut.js` |
| DragonEggNotice | 1: 900020110 鬱蒼とした森 | `Reference/Cosmic/scripts/portal/DragonEggNotice.js` |
| MC2revive | 1: 980031200 カーニバルフィールド1&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MC2revive.js` |
| MCrevive1 | 1: 980000102 カーニバルフィールド1&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MCrevive1.js` |
| MCrevive2 | 1: 980000202 カーニバルフィールド2&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MCrevive2.js` |
| MCrevive3 | 1: 980000302 カーニバルフィールド3&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MCrevive3.js` |
| MCrevive4 | 1: 980000402 カーニバルフィールド4&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MCrevive4.js` |
| MCrevive5 | 1: 980000502 カーニバルフィールド5&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MCrevive5.js` |
| MCrevive6 | 1: 980000602 カーニバルフィールド6&lt;再生の間&gt; | `Reference/Cosmic/scripts/portal/MCrevive6.js` |
| PPinkOut | 1: 270050300 黄昏の黎明の間 | `Reference/Cosmic/scripts/portal/PPinkOut.js` |
| Pianus | 1: 230040410 危険な洞窟 | `Reference/Cosmic/scripts/portal/Pianus.js` |
| Pinkin | 1: 270050200 失われた黄昏 | `Reference/Cosmic/scripts/portal/Pinkin.js` |
| Populatus00 | 1: 220080000 時計塔の奥 | `Reference/Cosmic/scripts/portal/Populatus00.js` |
| Spacegaga_out0 | 1: 922240000 ガガを救出せよ！ | `Reference/Cosmic/scripts/portal/Spacegaga_out0.js` |
| Spacegaga_out1 | 1: 922240000 ガガを救出せよ！ | `Reference/Cosmic/scripts/portal/Spacegaga_out1.js` |
| Spacegaga_out2 | 1: 922240000 ガガを救出せよ！ | `Reference/Cosmic/scripts/portal/Spacegaga_out2.js` |
| Spacegaga_out3 | 1: 922240000 ガガを救出せよ！ | `Reference/Cosmic/scripts/portal/Spacegaga_out3.js` |
| TD_MC_Egate | 1: 106021400 東の塔 | `Reference/Cosmic/scripts/portal/TD_MC_Egate.js` |
| TD_MC_enterboss1 | 1: 106021400 東の塔 | `Reference/Cosmic/scripts/portal/TD_MC_enterboss1.js` |
| TD_MC_enterboss2 | 1: 106021402 最後の城塔 | `Reference/Cosmic/scripts/portal/TD_MC_enterboss2.js` |
| TD_MC_first | 1: 100000002 空き家 | `Reference/Cosmic/scripts/portal/TD_MC_first.js` |
| TD_MC_jump | 1: 106020403 絶壁の影 | `Reference/Cosmic/scripts/portal/TD_MC_jump.js` |
| Zakum03 | 1: 280010000 知られざる閉鉱 | `Reference/Cosmic/scripts/portal/Zakum03.js` |
| Zakum05 | 1: 211042300 ジャクムへの門 | `Reference/Cosmic/scripts/portal/Zakum05.js` |
| aMatchMove2 | 1: 980010000 闘技場の控え室 | `Reference/Cosmic/scripts/portal/aMatchMove2.js` |
| aranTutorAloneX | 1: 914000000 負傷兵の幕舍 | `Reference/Cosmic/scripts/portal/aranTutorAloneX.js` |
| aranTutorArrow0 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/portal/aranTutorArrow0.js` |
| aranTutorArrow1 | 1: 914000200 燃える森1 | `Reference/Cosmic/scripts/portal/aranTutorArrow1.js` |
| aranTutorArrow2 | 1: 914000210 燃える森2 | `Reference/Cosmic/scripts/portal/aranTutorArrow2.js` |
| aranTutorArrow3 | 1: 914000220 燃える森3 | `Reference/Cosmic/scripts/portal/aranTutorArrow3.js` |
| aranTutorGuide0 | 1: 914000200 燃える森1 | `Reference/Cosmic/scripts/portal/aranTutorGuide0.js` |
| aranTutorGuide1 | 1: 914000210 燃える森2 | `Reference/Cosmic/scripts/portal/aranTutorGuide1.js` |
| aranTutorGuide2 | 1: 914000220 燃える森3 | `Reference/Cosmic/scripts/portal/aranTutorGuide2.js` |
| aranTutorLost | 1: 914000300 森の最奥 | `Reference/Cosmic/scripts/portal/aranTutorLost.js` |
| aranTutorMono0 | 1: 914000000 負傷兵の幕舍 | `Reference/Cosmic/scripts/portal/aranTutorMono0.js` |
| aranTutorMono1 | 1: 914000000 負傷兵の幕舍 | `Reference/Cosmic/scripts/portal/aranTutorMono1.js` |
| aranTutorMono2 | 1: 914000000 負傷兵の幕舍 | `Reference/Cosmic/scripts/portal/aranTutorMono2.js` |
| aranTutorMono3 | 1: 914000200 燃える森1 | `Reference/Cosmic/scripts/portal/aranTutorMono3.js` |
| aranTutorOut1 | 1: 914000100 避難準備中 | `Reference/Cosmic/scripts/portal/aranTutorOut1.js` |
| aranTutorOut2 | 1: 914000200 燃える森1 | `Reference/Cosmic/scripts/portal/aranTutorOut2.js` |
| aranTutorOut3 | 1: 914000210 燃える森2 | `Reference/Cosmic/scripts/portal/aranTutorOut3.js` |
| ariantMout | 1: 980010010 王の部屋 | `Reference/Cosmic/scripts/portal/ariantMout.js` |
| ariantMout2 | 1: 980010020 闘技場出口 | `Reference/Cosmic/scripts/portal/ariantMout2.js` |
| ariant_Agit | 1: 260000200 アリアント集落地 | `Reference/Cosmic/scripts/portal/ariant_Agit.js` |
| ariant_castle | 1: 260000300 アリアント宮殿 | `Reference/Cosmic/scripts/portal/ariant_castle.js` |
| babyPigOut | 1: 900020110 鬱蒼とした森 | `Reference/Cosmic/scripts/portal/babyPigOut.js` |
| balogTemple | 1: 105090200 別世界への扉 | `Reference/Cosmic/scripts/portal/balogTemple.js` |
| catPriest_map | 1: 250010504 妖怪の森2 | `Reference/Cosmic/scripts/portal/catPriest_map.js` |
| contactDragon | 1: 900010100 夢見る一本道 | `Reference/Cosmic/scripts/portal/contactDragon.js` |
| curseforest | 1: 100040105 邪気の森1 | `Reference/Cosmic/scripts/portal/curseforest.js` |
| davy2_hd1 | 1: 925100200 甲板突破1 | `Reference/Cosmic/scripts/portal/davy2_hd1.js` |
| davy3_hd1 | 1: 925100300 甲板突破2 | `Reference/Cosmic/scripts/portal/davy3_hd1.js` |
| davy_next0 | 1: 925100000 海賊船への道 | `Reference/Cosmic/scripts/portal/davy_next0.js` |
| davy_next1 | 1: 925100100 船首突破 | `Reference/Cosmic/scripts/portal/davy_next1.js` |
| davy_next2 | 1: 925100200 甲板突破1 | `Reference/Cosmic/scripts/portal/davy_next2.js` |
| davy_next3 | 1: 925100300 甲板突破2 | `Reference/Cosmic/scripts/portal/davy_next3.js` |
| davy_next4 | 1: 925100400 海賊退治! | `Reference/Cosmic/scripts/portal/davy_next4.js` |
| dracoout | 1: 240000110 ステーション&lt;オルビス行き&gt; | `Reference/Cosmic/scripts/portal/dracoout.js` |
| dragonNest | 1: 240040610 危険な巣の下 | `Reference/Cosmic/scripts/portal/dragonNest.js` |
| dragoneyes | 1: 900010200 夢見る森 | `Reference/Cosmic/scripts/portal/dragoneyes.js` |
| eliza_Garden | 1: 200010300 空の階段2 | `Reference/Cosmic/scripts/portal/eliza_Garden.js` |
| end_cow | 1: 912000100 ノーチラスの牛小屋 | `Reference/Cosmic/scripts/portal/end_cow.js` |
| enterAchter | 1: 100000200 広場 | `Reference/Cosmic/scripts/portal/enterAchter.js` |
| enterDisguise0 | 1: 130000200 エレヴの分かれ道 | `Reference/Cosmic/scripts/portal/enterDisguise0.js` |
| enterDisguise1 | 1: 130010000 修行の森1 | `Reference/Cosmic/scripts/portal/enterDisguise1.js` |
| enterDisguise2 | 1: 130010000 修行の森1 | `Reference/Cosmic/scripts/portal/enterDisguise2.js` |
| enterDisguise3 | 1: 130010100 修行の森2 | `Reference/Cosmic/scripts/portal/enterDisguise3.js` |
| enterDisguise4 | 1: 130010100 修行の森2 | `Reference/Cosmic/scripts/portal/enterDisguise4.js` |
| enterDisguise5 | 1: 130010200 修行の森3 | `Reference/Cosmic/scripts/portal/enterDisguise5.js` |
| enterDollWay | 1: 105040201 人形使いの隠れ家 | `Reference/Cosmic/scripts/portal/enterDollWay.js` |
| enterDollcave | 1: 105070300 エビルアイの巣３ | `Reference/Cosmic/scripts/portal/enterDollcave.js` |
| enterEvanRoom | 1: 100030101 一軒家 | `Reference/Cosmic/scripts/portal/enterEvanRoom.js` |
| enterFirstDH | 1: 130020000 訓練場の入口 | `Reference/Cosmic/scripts/portal/enterFirstDH.js` |
| enterGym | 1: 140010100 リエン修行場入口 | `Reference/Cosmic/scripts/portal/enterGym.js` |
| enterInfo | 1: 104000000 港口 | `Reference/Cosmic/scripts/portal/enterInfo.js` |
| enterMCave | 1: 140030000 鏡洞窟 | `Reference/Cosmic/scripts/portal/enterMCave.js` |
| enterMagiclibrar | 1: 101000000 エリニア | `Reference/Cosmic/scripts/portal/enterMagiclibrar.js` |
| enterNepenthes | 1: 200060000 散歩道2 | `Reference/Cosmic/scripts/portal/enterNepenthes.js` |
| enterPort | 1: 140020200 雪に覆われた原野 3 | `Reference/Cosmic/scripts/portal/enterPort.js` |
| enterRider | 1: 211050000 凍てつく野原 | `Reference/Cosmic/scripts/portal/enterRider.js` |
| enterRienFirst | 1: 140010000 リエン西のフィールド | `Reference/Cosmic/scripts/portal/enterRienFirst.js` |
| enterSecondDH | 1: 130020000 訓練場の入口 | `Reference/Cosmic/scripts/portal/enterSecondDH.js` |
| enterWarehouse | 1: 300000010 キャンプ会議場 | `Reference/Cosmic/scripts/portal/enterWarehouse.js` |
| enterWitch | 1: 240040510 死んだ龍の巣 | `Reference/Cosmic/scripts/portal/enterWitch.js` |
| enter_earth00 | 1: 120000101 航海室 | `Reference/Cosmic/scripts/portal/enter_earth00.js` |
| enter_earth01 | 1: 221000300 司令室 | `Reference/Cosmic/scripts/portal/enter_earth01.js` |
| enterfourthDH | 1: 130020000 訓練場の入口 | `Reference/Cosmic/scripts/portal/enterfourthDH.js` |
| enterthirdDH | 1: 130020000 訓練場の入口 | `Reference/Cosmic/scripts/portal/enterthirdDH.js` |
| entertraining | 1: 1010000 冒険者の修練場入口 | `Reference/Cosmic/scripts/portal/entertraining.js` |
| evanEntrance | 1: 100030320 大きい一本道 2 | `Reference/Cosmic/scripts/portal/evanEntrance.js` |
| evanFall | 1: 900020100 鬱蒼とした森 | `Reference/Cosmic/scripts/portal/evanFall.js` |
| evanFarmCT | 1: 100030300 ウェタンギル中心地 | `Reference/Cosmic/scripts/portal/evanFarmCT.js` |
| evanGarden0 | 1: 100030102 前庭 | `Reference/Cosmic/scripts/portal/evanGarden0.js` |
| evanGarden1 | 1: 100030102 前庭 | `Reference/Cosmic/scripts/portal/evanGarden1.js` |
| evanRoom0 | 1: 100030100 人里離れた森の中 | `Reference/Cosmic/scripts/portal/evanRoom0.js` |
| evanRoom1 | 1: 100030100 人里離れた森の中 | `Reference/Cosmic/scripts/portal/evanRoom1.js` |
| evanlivingRoom | 1: 100030101 一軒家 | `Reference/Cosmic/scripts/portal/evanlivingRoom.js` |
| evantalk00 | 1: 900010000 夢見る森入口 | `Reference/Cosmic/scripts/portal/evantalk00.js` |
| evantalk01 | 1: 900010000 夢見る森入口 | `Reference/Cosmic/scripts/portal/evantalk01.js` |
| evantalk02 | 1: 900010000 夢見る森入口 | `Reference/Cosmic/scripts/portal/evantalk02.js` |
| evantalk10 | 1: 900010100 夢見る一本道 | `Reference/Cosmic/scripts/portal/evantalk10.js` |
| evantalk11 | 1: 900010100 夢見る一本道 | `Reference/Cosmic/scripts/portal/evantalk11.js` |
| evantalk20 | 1: 900010200 夢見る森 | `Reference/Cosmic/scripts/portal/evantalk20.js` |
| evantalk21 | 1: 900010200 夢見る森 | `Reference/Cosmic/scripts/portal/evantalk21.js` |
| evantalk40 | 1: 900020200 忘れてしまった森入口 | `Reference/Cosmic/scripts/portal/evantalk40.js` |
| evantalk41 | 1: 900020200 忘れてしまった森入口 | `Reference/Cosmic/scripts/portal/evantalk41.js` |
| evantalk42 | 1: 900020200 忘れてしまった森入口 | `Reference/Cosmic/scripts/portal/evantalk42.js` |
| evantalk50 | 1: 900020210 忘れてしまった一本道 | `Reference/Cosmic/scripts/portal/evantalk50.js` |
| evantalk60 | 1: 900020220 忘れてしまった森 | `Reference/Cosmic/scripts/portal/evantalk60.js` |
| foxLaidy_map | 1: 222010300 狐の丘 | `Reference/Cosmic/scripts/portal/foxLaidy_map.js` |
| gaga_success | 1: 922240000 ガガを救出せよ！ | `Reference/Cosmic/scripts/portal/gaga_success.js` |
| ghostgate_open | 1: 990000700 シャレン3世の墓 | `Reference/Cosmic/scripts/portal/ghostgate_open.js` |
| glpqPortal0 | 1: 803000700 最奥への通路 | `Reference/Cosmic/scripts/portal/glpqPortal0.js` |
| glpqPortal00 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/portal/glpqPortal00.js` |
| glpqPortal01 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/portal/glpqPortal01.js` |
| glpqPortal02 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/portal/glpqPortal02.js` |
| glpqPortal03 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/portal/glpqPortal03.js` |
| glpqPortal04 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/portal/glpqPortal04.js` |
| glpqPortal1 | 1: 803000800 忘れられた保管庫 | `Reference/Cosmic/scripts/portal/glpqPortal1.js` |
| glpqPortal2 | 1: 803000900 疾さの試練 | `Reference/Cosmic/scripts/portal/glpqPortal2.js` |
| glpqPortal3 | 1: 803000900 疾さの試練 | `Reference/Cosmic/scripts/portal/glpqPortal3.js` |
| glpqPortal4 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/portal/glpqPortal4.js` |
| glpqPortal5 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/portal/glpqPortal5.js` |
| glpqPortal6 | 1: 803001200 全知全能の間 | `Reference/Cosmic/scripts/portal/glpqPortal6.js` |
| go_secretroom | 1: 106021000 摩天楼3 | `Reference/Cosmic/scripts/portal/go_secretroom.js` |
| gotocastle | 1: 106020400 分かれ道 | `Reference/Cosmic/scripts/portal/gotocastle.js` |
| gryphius | 1: 240020100 火と闇の戦場 | `Reference/Cosmic/scripts/portal/gryphius.js` |
| guild1F00 | 1: 990000700 シャレン3世の墓 | `Reference/Cosmic/scripts/portal/guild1F00.js` |
| guild1F01 | 1: 990000611 迷路の終りA | `Reference/Cosmic/scripts/portal/guild1F01.js` |
| guild1F02 | 1: 990000620 水路の迷路2 | `Reference/Cosmic/scripts/portal/guild1F02.js` |
| guild1F03 | 1: 990000631 迷路の終りB | `Reference/Cosmic/scripts/portal/guild1F03.js` |
| guild1F04 | 1: 990000641 迷路の終りC | `Reference/Cosmic/scripts/portal/guild1F04.js` |
| hontale_BtoB1 | 1: 240050100 迷路部屋 | `Reference/Cosmic/scripts/portal/hontale_BtoB1.js` |
| hontale_C | 1: 240050200 選択の洞窟 | `Reference/Cosmic/scripts/portal/hontale_C.js` |
| hontale_morph2 | 1: 240040700 生命の洞窟入口 | `Reference/Cosmic/scripts/portal/hontale_morph2.js` |
| hontale_out1 | 1: 240050600 ホーンテイルの洞窟 | `Reference/Cosmic/scripts/portal/hontale_out1.js` |
| inDragonEgg | 1: 100030300 ウェタンギル中心地 | `Reference/Cosmic/scripts/portal/inDragonEgg.js` |
| inERShip | 1: 101000000 エリニア | `Reference/Cosmic/scripts/portal/inERShip.js` |
| infoAttack | 1: 40000 小さな森 | `Reference/Cosmic/scripts/portal/infoAttack.js` |
| infoMinimap | 1: 10000 キノコの丘 | `Reference/Cosmic/scripts/portal/infoMinimap.js` |
| infoPickup | 1: 40000 小さな森 | `Reference/Cosmic/scripts/portal/infoPickup.js` |
| infoReactor | 1: 1000000 アムホスト | `Reference/Cosmic/scripts/portal/infoReactor.js` |
| infoSkill | 1: 40000 小さな森 | `Reference/Cosmic/scripts/portal/infoSkill.js` |
| infoWorldmap | 1: 50000 危険な森 | `Reference/Cosmic/scripts/portal/infoWorldmap.js` |
| jnr12_in | 1: 926110400 中央研究室入口 | `Reference/Cosmic/scripts/portal/jnr12_in.js` |
| jnr1_out | 1: 926110001 暗い通路 | `Reference/Cosmic/scripts/portal/jnr1_out.js` |
| jnr1_pt00 | 1: 926110000 怪しい研究室 | `Reference/Cosmic/scripts/portal/jnr1_pt00.js` |
| jnr2_out | 1: 926110100 不快な実験室 | `Reference/Cosmic/scripts/portal/jnr2_out.js` |
| jnr3_in0 | 1: 926110200 特殊な実験室 | `Reference/Cosmic/scripts/portal/jnr3_in0.js` |
| jnr3_in1 | 1: 926110200 特殊な実験室 | `Reference/Cosmic/scripts/portal/jnr3_in1.js` |
| jnr3_out | 1: 926110200 特殊な実験室 | `Reference/Cosmic/scripts/portal/jnr3_out.js` |
| jnr4_r1 | 1: 926110300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/jnr4_r1.js` |
| jnr4_r2 | 1: 926110300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/jnr4_r2.js` |
| jnr4_r3 | 1: 926110300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/jnr4_r3.js` |
| jnr4_r4 | 1: 926110300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/jnr4_r4.js` |
| jnr6_out | 1: 926110203 ユレテの研究室 | `Reference/Cosmic/scripts/portal/jnr6_out.js` |
| jnr_201_0 | 1: 926110201 暗い研究室1 | `Reference/Cosmic/scripts/portal/jnr_201_0.js` |
| jnr_202 | 1: 926110202 暗い研究室2 | `Reference/Cosmic/scripts/portal/jnr_202.js` |
| kinggate2_open | 1: 990000800 王の回廊 | `Reference/Cosmic/scripts/portal/kinggate2_open.js` |
| kinggate_open | 1: 990000800 王の回廊 | `Reference/Cosmic/scripts/portal/kinggate_open.js` |
| ludi021 | 1: 922000009 秘密通路 | `Reference/Cosmic/scripts/portal/ludi021.js` |
| magatia_dark0 | 1: 261020600 研究所B-3区域 | `Reference/Cosmic/scripts/portal/magatia_dark0.js` |
| mayong | 1: 240020400 フリーズケンタウロスの領域 | `Reference/Cosmic/scripts/portal/mayong.js` |
| met_in | 1: 102040000 工事現場-北 | `Reference/Cosmic/scripts/portal/met_in.js` |
| met_out | 1: 910320000 捨てられた地下鉄の歴史 | `Reference/Cosmic/scripts/portal/met_out.js` |
| metalgate_open | 1: 990000430 信念の部屋 | `Reference/Cosmic/scripts/portal/metalgate_open.js` |
| metro_Chat00 | 1: 910320000 捨てられた地下鉄の歴史 | `Reference/Cosmic/scripts/portal/metro_Chat00.js` |
| metro_in00 | 1: 910320000 捨てられた地下鉄の歴史 | `Reference/Cosmic/scripts/portal/metro_in00.js` |
| minar_job4 | 1: 240010500 動物の谷 | `Reference/Cosmic/scripts/portal/minar_job4.js` |
| mirtalk00 | 1: 900010000 夢見る森入口 | `Reference/Cosmic/scripts/portal/mirtalk00.js` |
| mirtalk01 | 1: 900010100 夢見る一本道 | `Reference/Cosmic/scripts/portal/mirtalk01.js` |
| moveBefore | 1: 108000710 鍛冶屋外部 | `Reference/Cosmic/scripts/portal/moveBefore.js` |
| moveNext | 1: 108000700 大将翁の鍛冶屋 | `Reference/Cosmic/scripts/portal/moveNext.js` |
| move_elin | 1: 222020400 時間制御室 | `Reference/Cosmic/scripts/portal/move_elin.js` |
| nets_in | 1: 260020500 サヘル地帯3 | `Reference/Cosmic/scripts/portal/nets_in.js` |
| nets_out | 1: 926010000 ピラミッドの丘 | `Reference/Cosmic/scripts/portal/nets_out.js` |
| obstacle | 1: 106020300 奥深きキノコの森 | `Reference/Cosmic/scripts/portal/obstacle.js` |
| outChild | 1: 914000300 森の最奥 | `Reference/Cosmic/scripts/portal/outChild.js` |
| outDarkEreb | 1: 913030000 エレヴ | `Reference/Cosmic/scripts/portal/outDarkEreb.js` |
| outMagiclib | 1: 910110000 魔法図書館 | `Reference/Cosmic/scripts/portal/outMagiclib.js` |
| outMaha | 1: 914020000 マッハとの対峙 | `Reference/Cosmic/scripts/portal/outMaha.js` |
| outNix1 | 1: 240020600 人里離れた森 | `Reference/Cosmic/scripts/portal/outNix1.js` |
| outNix2 | 1: 240020600 人里離れた森 | `Reference/Cosmic/scripts/portal/outNix2.js` |
| outTemple | 1: 270000100 時間の神殿 | `Reference/Cosmic/scripts/portal/outTemple.js` |
| out_pepeking | 1: 106021500 結婚式場入口 | `Reference/Cosmic/scripts/portal/out_pepeking.js` |
| outtestWolf | 1: 914030000 オオカミの試験 | `Reference/Cosmic/scripts/portal/outtestWolf.js` |
| party3_gardenin | 1: 920010800 庭園 | `Reference/Cosmic/scripts/portal/party3_gardenin.js` |
| party3_jail1 | 1: 920010900 罪人の部屋 | `Reference/Cosmic/scripts/portal/party3_jail1.js` |
| party3_jail2 | 1: 920010900 罪人の部屋 | `Reference/Cosmic/scripts/portal/party3_jail2.js` |
| party3_jail3 | 1: 920010900 罪人の部屋 | `Reference/Cosmic/scripts/portal/party3_jail3.js` |
| party3_r4pt | 1: 920010600 ラウンジ | `Reference/Cosmic/scripts/portal/party3_r4pt.js` |
| party3_r6pt | 1: 920010700 登り道 | `Reference/Cosmic/scripts/portal/party3_r6pt.js` |
| party3_room1 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room1.js` |
| party3_room2 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room2.js` |
| party3_room3 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room3.js` |
| party3_room4 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room4.js` |
| party3_room5 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room5.js` |
| party3_room6 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room6.js` |
| party3_room8 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/portal/party3_room8.js` |
| party6_out | 1: 930000600 毒の森 | `Reference/Cosmic/scripts/portal/party6_out.js` |
| party6_stage501 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage501.js` |
| party6_stage502 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage502.js` |
| party6_stage503 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage503.js` |
| party6_stage504 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage504.js` |
| party6_stage505 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage505.js` |
| party6_stage506 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage506.js` |
| party6_stage507 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage507.js` |
| party6_stage508 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage508.js` |
| party6_stage509 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage509.js` |
| party6_stage510 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage510.js` |
| party6_stage511 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage511.js` |
| party6_stage512 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage512.js` |
| party6_stage513 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage513.js` |
| party6_stage514 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage514.js` |
| party6_stage515 | 1: 930000300 霧の森 | `Reference/Cosmic/scripts/portal/party6_stage515.js` |
| party6_stage800 | 1: 930000800 森出口 | `Reference/Cosmic/scripts/portal/party6_stage800.js` |
| q2073 | 1: 100030000 東の森 | `Reference/Cosmic/scripts/portal/q2073.js` |
| q3366in | 1: 926130100 実験室の入口 | `Reference/Cosmic/scripts/portal/q3366in.js` |
| q3366out | 1: 926130200 実験室の出口 | `Reference/Cosmic/scripts/portal/q3366out.js` |
| q3367in | 1: 926130100 実験室の入口 | `Reference/Cosmic/scripts/portal/q3367in.js` |
| q3367out | 1: 926130201 実験室の出口 | `Reference/Cosmic/scripts/portal/q3367out.js` |
| q3368in | 1: 926130100 実験室の入口 | `Reference/Cosmic/scripts/portal/q3368in.js` |
| q3368out | 1: 926130203 実験室の出口 | `Reference/Cosmic/scripts/portal/q3368out.js` |
| reundodraco | 1: 240000110 ステーション&lt;オルビス行き&gt; | `Reference/Cosmic/scripts/portal/reundodraco.js` |
| rienCaveEnter | 1: 140010200 氷原野 | `Reference/Cosmic/scripts/portal/rienCaveEnter.js` |
| rienTutor1 | 1: 140090100 冷たい森1 | `Reference/Cosmic/scripts/portal/rienTutor1.js` |
| rienTutor2 | 1: 140090200 冷たい森2 | `Reference/Cosmic/scripts/portal/rienTutor2.js` |
| rienTutor3 | 1: 140090300 冷たい森3 | `Reference/Cosmic/scripts/portal/rienTutor3.js` |
| rienTutor4 | 1: 140090400 冷たい森4 | `Reference/Cosmic/scripts/portal/rienTutor4.js` |
| rienTutor5 | 1: 140090500 冷たい森5 | `Reference/Cosmic/scripts/portal/rienTutor5.js` |
| rienTutor6 | 1: 140090500 冷たい森5 | `Reference/Cosmic/scripts/portal/rienTutor6.js` |
| rienTutor7 | 1: 140010000 リエン西のフィールド | `Reference/Cosmic/scripts/portal/rienTutor7.js` |
| rienTutor8 | 1: 140000000 リエン村 | `Reference/Cosmic/scripts/portal/rienTutor8.js` |
| rnj12_in | 1: 926100400 中央研究室入口 | `Reference/Cosmic/scripts/portal/rnj12_in.js` |
| rnj1_out | 1: 926100001 暗い通路 | `Reference/Cosmic/scripts/portal/rnj1_out.js` |
| rnj1_pt00 | 1: 926100000 怪しい研究室 | `Reference/Cosmic/scripts/portal/rnj1_pt00.js` |
| rnj2_out | 1: 926100100 不快な実験室 | `Reference/Cosmic/scripts/portal/rnj2_out.js` |
| rnj3_in0 | 1: 926100200 特殊な実験室 | `Reference/Cosmic/scripts/portal/rnj3_in0.js` |
| rnj3_in1 | 1: 926100200 特殊な実験室 | `Reference/Cosmic/scripts/portal/rnj3_in1.js` |
| rnj3_out | 1: 926100200 特殊な実験室 | `Reference/Cosmic/scripts/portal/rnj3_out.js` |
| rnj4_r1 | 1: 926100300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/rnj4_r1.js` |
| rnj4_r2 | 1: 926100300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/rnj4_r2.js` |
| rnj4_r3 | 1: 926100300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/rnj4_r3.js` |
| rnj4_r4 | 1: 926100300 ユレテの事務所 | `Reference/Cosmic/scripts/portal/rnj4_r4.js` |
| rnj6_out | 1: 926100203 ユレテの研究室 | `Reference/Cosmic/scripts/portal/rnj6_out.js` |
| rnj_201_0 | 1: 926100201 暗い研究室1 | `Reference/Cosmic/scripts/portal/rnj_201_0.js` |
| rnj_202 | 1: 926100202 暗い研究室2 | `Reference/Cosmic/scripts/portal/rnj_202.js` |
| s4berserk | 1: 105090800 神殿の入口4 | `Reference/Cosmic/scripts/portal/s4berserk.js` |
| s4berserk_move | 1: 910500200 忘れられた神殿 | `Reference/Cosmic/scripts/portal/s4berserk_move.js` |
| s4firehawk | 1: 211042200 試練の洞窟3 | `Reference/Cosmic/scripts/portal/s4firehawk.js` |
| s4hitman | 1: 101030104 遺跡発掘ベースキャンプ | `Reference/Cosmic/scripts/portal/s4hitman.js` |
| s4iceeagle | 1: 211040700 危ない絶壁 | `Reference/Cosmic/scripts/portal/s4iceeagle.js` |
| s4mind_end | 1: 925010300 デリーを守れ！ | `Reference/Cosmic/scripts/portal/s4mind_end.js` |
| s4nest | 1: 240010700 空の巣1 | `Reference/Cosmic/scripts/portal/s4nest.js` |
| s4resur_enter | 1: 220070400 忘れられた回廊 | `Reference/Cosmic/scripts/portal/s4resur_enter.js` |
| s4resur_out | 1: 922020000 忘れられた闇 | `Reference/Cosmic/scripts/portal/s4resur_out.js` |
| s4resurrection | 1: 230040300 危険な海峡2 | `Reference/Cosmic/scripts/portal/s4resurrection.js` |
| s4rush | 1: 105090700 神殿の入口3 | `Reference/Cosmic/scripts/portal/s4rush.js` |
| s4ship_out | 1: 912010100 カイリンの訓練場 | `Reference/Cosmic/scripts/portal/s4ship_out.js` |
| s4super_out | 1: 912010000 カイリンの訓練場 | `Reference/Cosmic/scripts/portal/s4super_out.js` |
| s4tornado_enter | 1: 220010001 雲のバルコニー | `Reference/Cosmic/scripts/portal/s4tornado_enter.js` |
| secretgate1_open | 1: 990000610 水路の迷路1 | `Reference/Cosmic/scripts/portal/secretgate1_open.js` |
| secretgate2_open | 1: 990000630 水路の迷路3 | `Reference/Cosmic/scripts/portal/secretgate2_open.js` |
| secretgate3_open | 1: 990000640 水路の迷路4 | `Reference/Cosmic/scripts/portal/secretgate3_open.js` |
| skyrom | 1: 260000302 アリアント宮殿&lt;廊下&gt; | `Reference/Cosmic/scripts/portal/skyrom.js` |
| space_return | 1: 922240200 ガガ救出失敗 | `Reference/Cosmic/scripts/portal/space_return.js` |
| speargate_open | 1: 990000400 騎士のホール | `Reference/Cosmic/scripts/portal/speargate_open.js` |
| statuegate_open | 1: 990000300 シャレニアン城門 | `Reference/Cosmic/scripts/portal/statuegate_open.js` |
| stonegate_open | 1: 990000400 騎士のホール | `Reference/Cosmic/scripts/portal/stonegate_open.js` |
| subway_in2 | 1: 103000100 切符売り場 | `Reference/Cosmic/scripts/portal/subway_in2.js` |
| tamepig_out2 | 1: 923010100 飼育室通路 | `Reference/Cosmic/scripts/portal/tamepig_out2.js` |
| templeenter | 1: 200090510 ミナルへの道 | `Reference/Cosmic/scripts/portal/templeenter.js` |
| thief_in1 | 1: 260010401 岩坂 | `Reference/Cosmic/scripts/portal/thief_in1.js` |
| tristanEnter | 1: 105100100 神殿の底 | `Reference/Cosmic/scripts/portal/tristanEnter.js` |
| tutoChatNPC | 1: 10000 キノコの丘 | `Reference/Cosmic/scripts/portal/tutoChatNPC.js` |
| tutorHelper | 1: 130030000 開始の森1 | `Reference/Cosmic/scripts/portal/tutorHelper.js` |
| tutorMinimap | 1: 130030001 開始の森2 | `Reference/Cosmic/scripts/portal/tutorMinimap.js` |
| under30gate | 1: 990000600 地下水路 | `Reference/Cosmic/scripts/portal/under30gate.js` |
| watergate_open | 1: 990000500 賢者の噴水 | `Reference/Cosmic/scripts/portal/watergate_open.js` |

## Portal scripts JMS maps use that neither Cosmic nor Cronus has (JMS-only content)

| script | JMS maps |
|---|---|
| unityPortal2 | 17: 100000000 ヘネシス, 101000000 エリニア, 102000000 ぺリオン … |
| StartHarpMsg | 10: 749050100 お菓子の部屋, 749050101 お菓子の部屋, 749050102 お菓子の部屋 … |
| cheeseEnd | 10: 926120400 チーズ倉庫, 926120401 チーズ倉庫, 926120402 チーズ倉庫 … |
| cheeseLog | 10: 926120400 チーズ倉庫, 926120401 チーズ倉庫, 926120402 チーズ倉庫 … |
| move_EliEre | 10: 200090030 エレヴ行き, 200090032 エレヴ行き, 200090034 エレヴ行き … |
| move_EreEli | 10: 200090031 エリニア行き, 200090033 エリニア行き, 200090035 エリニア行き … |
| move_EreOrb | 10: 200090021 オルビス行き, 200090023 オルビス行き, 200090025 オルビス行き … |
| move_OrbEre | 10: 200090020 エレヴ行き, 200090022 エレヴ行き, 200090024 エレヴ行き … |
| move_RieRit | 10: 200090070 リス港口行, 200090071 リス港口行, 200090072 リス港口行 … |
| move_RitRie | 10: 200090060 リエン行, 200090061 リエン行, 200090062 リエン行 … |
| passStage1 | 10: 749050100 お菓子の部屋, 749050101 お菓子の部屋, 749050102 お菓子の部屋 … |
| passStage2 | 10: 749050100 お菓子の部屋, 749050101 お菓子の部屋, 749050102 お菓子の部屋 … |
| passStage3 | 10: 749050100 お菓子の部屋, 749050101 お菓子の部屋, 749050102 お菓子の部屋 … |
| passStage4 | 10: 749050100 お菓子の部屋, 749050101 お菓子の部屋, 749050102 お菓子の部屋 … |
| Sky_Next | 7: 240080100 天空地域1, 240080200 天空地域2, 240080300 天空地域3 … |
| visitorPT_B | 7: 502030002 生産キューブ, 502030004 増殖キューブ, 502030007 接近禁止キューブ … |
| visitorPT_L | 7: 502030001 次元キューブ, 502030003 エネルギーキューブ, 502030004 増殖キューブ … |
| visitorPT_T | 7: 502030002 生産キューブ, 502030003 エネルギーキューブ, 502030005 選択のキューブ … |
| Sky_Previous | 6: 240080200 天空地域2, 240080300 天空地域3, 240080400 天空地域4 … |
| ghostShip_next | 6: 923020110 第1作戦室, 923020111 第2作戦室, 923020112 第3作戦室 … |
| visitorPT_R | 6: 502030000 進入キューブ, 502030001 次元キューブ, 502030003 エネルギーキューブ … |
| Lamp7th_enter2 | 5: 805000200 地下監獄, 805000201 地下監獄, 805000202 地下監獄 … |
| Lamp7th_enter3 | 5: 805000210 地下監獄, 805000211 地下監獄, 805000212 地下監獄 … |
| BlackHoleReturn | 4: 502040100 墜落した宇宙船の深海, 502040200 化石鉱物の鉱山, 502040300 加工工場輸送路 … |
| GS_backSpace | 4: 923020111 第2作戦室, 923020112 第3作戦室, 923020113 第4作戦室 … |
| Sky_ReviveOut | 4: 240080040 天空の復活場, 240080041 天空の復活場, 240080050 亡子の洞窟 … |
| goldkey1 | 4: 980041000 魔女の塔1階, 980042000 魔女の塔1階, 980043000 魔女の塔1階 … |
| goldkey10 | 4: 980041100 魔女の塔2階, 980042100 魔女の塔2階, 980043100 魔女の塔2階 … |
| goldkey2 | 4: 980041000 魔女の塔1階, 980042000 魔女の塔1階, 980043000 魔女の塔1階 … |
| goldkey3 | 4: 980041000 魔女の塔1階, 980042000 魔女の塔1階, 980043000 魔女の塔1階 … |
| goldkey4 | 4: 980041000 魔女の塔1階, 980042000 魔女の塔1階, 980043000 魔女の塔1階 … |
| goldkey5 | 4: 980041000 魔女の塔1階, 980042000 魔女の塔1階, 980043000 魔女の塔1階 … |
| goldkey6 | 4: 980041000 魔女の塔1階, 980042000 魔女の塔1階, 980043000 魔女の塔1階 … |
| goldkey7 | 4: 980041100 魔女の塔2階, 980042100 魔女の塔2階, 980043100 魔女の塔2階 … |
| goldkey8 | 4: 980041100 魔女の塔2階, 980042100 魔女の塔2階, 980043100 魔女の塔2階 … |
| goldkey9 | 4: 980041100 魔女の塔2階, 980042100 魔女の塔2階, 980043100 魔女の塔2階 … |
| party_portal | 4: 910340100 첫번째동행&lt;1관문&gt;, 910340200 첫번째동행&lt;2관문&gt;, 910340300 첫번째동행&lt;3관문&gt; … |
| aM_start | 3: 980010101 一番目の闘技場&lt;競技場&gt;, 980010201 二番目の闘技場&lt;競技場&gt;, 980010301 三番目の闘技場&lt;競技場&gt; |
| connetNPC0 | 3: 229030200 3番目の部屋, 229030600 3番目の部屋, 229030900 3番目の部屋 |
| rand_ola | 3: 109030001 のぼれ～のぼれ～＜1段階＞, 109030002 のぼれ～のぼれ～＜２段階＞, 109030003 のぼれ～のぼれ～＜３段階＞ |
| Ravana_Enter | 2: 809061000 封印された神殿の入口, 950101000 封印された神殿の入口 |
| enterExitMirror | 2: 809060000 金箔寺, 950100000 金箔寺 |
| goGoblin | 2: 809060000 金箔寺, 950100000 金箔寺 |
| goMonkey | 2: 809060000 金箔寺, 950100000 金箔寺 |
| goShrine | 2: 809060000 金箔寺, 950100000 金箔寺 |
| goback | 2: 809060000 金箔寺, 950100000 金箔寺 |
| goldenShrine_Out | 2: 809061000 封印された神殿の入口, 950101000 封印された神殿の入口 |
| pachinkoEnter | 2: 801000300 ショーワ町通り, 810000000 パチンコ屋入口 |
| CrimsonPQH | 1: 803000519 遠征隊通路-挑戦者の道- |
| CrimsonPQL | 1: 803000509 遠征隊通路-修練の道- |
| Crimsonhuji00 | 1: 803000202 重ねの道 |
| Crimsonhuji01 | 1: 803000304 危殆なる道 |
| Crimsonhuji02 | 1: 803000400 クリムゾンウッドの聖地 |
| Depart_topFloor | 1: 103040400 7階 8階 A区域 |
| DualTutor1 | 1: 804000100 隠れ修練場 |
| DualTutor10 | 1: 804000060 焼けた忍者村 |
| DualTutor2 | 1: 804000101 隠れ修練場 |
| DualTutor3 | 1: 804000102 隠れ修練場 |
| DualTutor4 | 1: 804000103 隠れ修練場 |
| DualTutor5 | 1: 804000200 隠れ修練場 |
| DualTutor6 | 1: 804000201 隠れ修練場 |
| DualTutor7 | 1: 804000202 隠れ修練場 |
| DualTutor8 | 1: 804000203 隠れ修練場 |
| DualTutor9 | 1: 804000300 隠れ修練場 |
| DualTutorMono0 | 1: 804000100 隠れ修練場 |
| DualTutorMono1 | 1: 804000101 隠れ修練場 |
| DualTutorMono2 | 1: 804000102 隠れ修練場 |
| DualTutorMono3 | 1: 804000200 隠れ修練場 |
| Dual_moveGate | 1: 103000000 カニングシティー |
| EvanDbPortaljp | 1: 103050101 千火の部屋 |
| EvanEggPortalJP | 1: 100030400 入口 |
| EvanOrbisjp | 1: 804000500 オルビス公園 |
| EvanPortalJP | 1: 100030000 東の森 |
| Evanflowerjp | 1: 804000070 秘桜蔭2階 |
| GS_revive | 1: 923020109 セカンドチャンス |
| GS_stage5_water | 1: 923020114 第5作戦室 |
| GS_switchChat | 1: 923020114 第5作戦室 |
| HglpqPortal0 | 1: 803010700 最奥への通路 |
| HglpqPortal00 | 1: 803011100 統一の試練 |
| HglpqPortal01 | 1: 803011100 統一の試練 |
| HglpqPortal02 | 1: 803011100 統一の試練 |
| HglpqPortal03 | 1: 803011100 統一の試練 |
| HglpqPortal04 | 1: 803011100 統一の試練 |
| HglpqPortal1 | 1: 803010800 忘れられた保管庫 |
| HglpqPortal2 | 1: 803010900 疾さの試練 |
| HglpqPortal3 | 1: 803010900 疾さの試練 |
| HglpqPortal4 | 1: 803011000 機知の試練 |
| HglpqPortal5 | 1: 803011100 統一の試練 |
| HglpqPortal6 | 1: 803011200 全知全能の間 |
| Lamp7th_out | 1: 805000300 地下監獄出口 |
| MCrevive7 | 1: 980000702 プレミアムカーニバルフィールド1&lt;復活の場&gt; |
| MCrevive8 | 1: 980000802 プレミアムカーニバルフィールド2&lt;復活の場&gt; |
| MCrevive9 | 1: 980000902 プレミアムカーニバルフィールド3&lt;復活の場&gt; |
| OutPerrion_1 | 1: 105100100 神殿の底 |
| OutPerrion_2 | 1: 105100100 神殿の底 |
| PB_wich | 1: 980040000 魔女の塔入口 |
| Sky_BossOut | 1: 240080800 天空の巣 |
| Sky_BossSummon | 1: 240080800 天空の巣 |
| Sky_Enter | 1: 240080000 天空の渡し場 |
| Sky_Out | 1: 240080100 天空地域1 |
| StudioZone_Out | 1: 970000100 スタジオ控え室 |
| TD_MC_faild | 1: 106020601 番兵の警戒所 |
| TokPQ1 | 1: 802000801 六本木モール2102年(エントランス) |
| Tokyoboss1_go | 1: 802000209 お台場 2100年 |
| Tokyoboss2_go | 1: 802000309 公園 2095年 |
| Tokyoboss3_go | 1: 802000409 秋葉原 2102年 |
| Tokyoboss4_go | 1: 802000609 旗艦ファイア・オールドフォックス甲板 2102年 |
| Tokyoboss5_go | 1: 802000109 カムナ |
| Tokyoboss6_go | 1: 802000709 渋谷2102年 |
| Tokyoboss7_go | 1: 802000819 六本木モール最上階2102年 |
| amoria_out | 1: 680000000 ウェディングタウン |
| bingcube_EnterPT | 1: 502029000 墜落した宇宙船入口 |
| bosscube_outpotal | 1: 502030011 代表者のキューブ |
| cellar | 1: 300000010 キャンプ会議場 |
| cheeseEx | 1: 926120410 チーズ倉庫の出口 |
| cheeseOut | 1: 926120410 チーズ倉庫の出口 |
| chimney00_open | 1: 229030000 1番目の部屋 |
| chimney01_open | 1: 229030000 1番目の部屋 |
| chimney02_open | 1: 229030000 1番目の部屋 |
| chimney03_open | 1: 229030000 1番目の部屋 |
| chimney10_open | 1: 229030400 1番目の部屋 |
| chimney11_open | 1: 229030400 1番目の部屋 |
| chimney12_open | 1: 229030400 1番目の部屋 |
| chimney13_open | 1: 229030400 1番目の部屋 |
| chimney20_open | 1: 229030700 1番目の部屋 |
| chimney21_open | 1: 229030700 1番目の部屋 |
| chimney22_open | 1: 229030700 1番目の部屋 |
| chimney23_open | 1: 229030700 1番目の部屋 |
| clearRider | 1: 921110000 プニの原野 |
| crane_MR | 1: 200090300 武陵行き |
| crane_SS | 1: 200090310 航海中&lt;オルビス行き&gt; |
| davy_next00 | 1: 251010404 海賊船の向こう |
| dual_ballRoom | 1: 103050100 秘桜蔭 2階 |
| dual_lv20 | 1: 103050300 秘桜蔭地下室 |
| dual_lv25 | 1: 103050300 秘桜蔭地下室 |
| dual_lv30 | 1: 103050300 秘桜蔭地下室 |
| end_black | 1: 912000000 アジト |
| enterAttackedAgit | 1: 804000600 カニングシティー裏通り |
| enterBlackBC | 1: 220011000 空のテラス5 |
| enterBlackFrog | 1: 220000300 ルディブリアム住宅街 |
| enterBlackRoom | 1: 200080600 オルビス塔&lt;16層&gt; |
| enterPottery | 1: 251010403 赤鼻海賊団の盗掘3 |
| enterSDI | 1: 922030000 蛙口の家 |
| enterSnowDragon | 1: 914100010 雪に覆われた森 |
| evanDollGR | 1: 106010102 ゴーレム寺院の入口 |
| evanGolemDoor | 1: 106010101 息をする岩 |
| find_james | 1: 106021201 中央の城塔 |
| fishing_out | 1: 741000200 釣り場 |
| ghost1 | 1: 229030100 2番目の部屋 |
| ghost2 | 1: 229030500 2番目の部屋 |
| ghost3 | 1: 229030800 2番目の部屋 |
| ghostOut1 | 1: 229030100 2番目の部屋 |
| ghostOut2 | 1: 229030500 2番目の部屋 |
| ghostOut3 | 1: 229030800 2番目の部屋 |
| guardmap_pt_researcher | 1: 502010100 秘密基地地下道 |
| guardmap_pt_shuttle | 1: 502010400 加工工場輸送路 |
| halloween_out | 1: 229010000 庭園 |
| hidenPT_0 | 1: 100000000 ヘネシス |
| hidenPT_1 | 1: 220000000 ルディブリアム |
| hidenPT_2 | 1: 200000000 オルビス |
| hidenPT_3 | 1: 221000000 地球防衛本部 |
| hontale_boss1 | 1: 240060000 試験の洞窟1 |
| hontale_boss2 | 1: 240060100 試験の洞窟2 |
| hontale_morph | 1: 240040700 生命の洞窟入口 |
| hwqout | 1: 229030300 執事の部屋 |
| hwtool | 1: 229000310 人形工房 |
| inhalloween | 1: 229010000 庭園 |
| investigate1 | 1: 106020300 奥深きキノコの森 |
| investigate2 | 1: 106020500 城壁の端 |
| jnr6_act | 1: 926110203 ユレテの研究室 |
| mapleTree_out | 1: 970010000 紅葉の木の庭園 |
| move_RitSDI | 1: 200090080 용이 잠든 섬으로 |
| move_SDIRit | 1: 200090090 리스항구행 |
| ninja_Boss | 1: 800040401 楓城 天守閣2階 |
| nooutShip | 1: 914000500 避難準備完了 |
| outAfrienMemory | 1: 900030000 陣地裏手 |
| outBabyBird | 1: 910020000 ヒナの巣 |
| outSDI | 1: 914100022 眠った洞窟 |
| pField_out | 1: 390009999 宝倉庫出口 |
| pachinko | 1: 809030000 パチンコ屋 |
| phasePT_00 | 1: 502030001 次元キューブ |
| photoOut | 1: 970000000 メイプルスタジオ |
| piramid_Chat00 | 1: 926010000 ピラミッドの丘 |
| piramid_in00 | 1: 926010000 ピラミッドの丘 |
| pqEnterPortal | 1: 803000502 クリムゾン砦3 |
| rnj6_act | 1: 926100203 ユレテの研究室 |
| rnj_clearQ | 1: 261000000 マガティア |
| ropeEarth1 | 1: 922230000 月の国 |
| ropeEarth2 | 1: 922230001 月の穴 |
| ropeMoon1 | 1: 922230001 月の穴 |
| ropeMoon2 | 1: 922230002 月の下 |
| s4common1_clear | 1: 921100300 タイラスを守れ |
| stage6_portal | 1: 922010600 放置された塔&lt;6段階&gt; |
| stopIceWall | 1: 914100020 眠った洞窟 |
| stopIceWall2 | 1: 914100022 眠った洞窟 |
| summondragon | 1: 240040611 ナインスピリット |
| tH_Out | 1: 980040000 魔女の塔入口 |
| third4_portal | 1: 108010400 光る水晶の通路 |
| tutorWorldmap | 1: 130030006 小さな橋 |
| visitorPT_Boss | 1: 502030015 中立キューブ |
| visitorPT_Exit | 1: 502030012 復活キューブ |
| visitorPT_Revive | 1: 502030012 復活キューブ |
| visitor_energy | 1: 502022010 エネルギー研究所への道 |
| visitor_jinbee82 | 1: 502021010 未来のヘネシス外郭 |
| visitor_time_p | 1: 502021010 未来のヘネシス外郭 |
| visitorhidenPT | 1: 502010010 地下道入口 |
| warpEarth | 1: 922230002 月の下 |
| warpMoon | 1: 922231000 月うさぎの遊び場 |

## Reactor scripts for reactors JMS maps place, that Cosmic has and Cronus lacks

| reactor | JMS maps | Cosmic file |
|---|---|---|
| 2508000 | 32: 925020100 武陵道場 1階, 925020200 武陵道場 2階, 925020300 武陵道場 3階 … | `Reference/Cosmic/scripts/reactor/2508000.js` |
| 2112012 | 23: 280010000 知られざる閉鉱, 280010010 1-1区域, 280010011 1-2区域 … | `Reference/Cosmic/scripts/reactor/2112012.js` |
| 2402000 | 16: 240010000 リプレ西側の森, 240010100 ミナル西側の境界, 240010101 毛玉の森 … | `Reference/Cosmic/scripts/reactor/2402000.js` |
| 2402001 | 16: 240010000 リプレ西側の森, 240010100 ミナル西側の境界, 240010101 毛玉の森 … | `Reference/Cosmic/scripts/reactor/2402001.js` |
| 2110000 | 15: 280010011 1-2区域, 280010031 3-2区域, 280010041 4-2区域 … | `Reference/Cosmic/scripts/reactor/2110000.js` |
| 8098000 | 15: 809050000 迷路, 809050001 迷路, 809050002 迷路 … | `Reference/Cosmic/scripts/reactor/8098000.js` |
| 2111000 | 14: 280010031 3-2区域, 280010041 4-2区域, 280010050 5-1区域 … | `Reference/Cosmic/scripts/reactor/2111000.js` |
| 2202002 | 13: 220020000 オモチャ工場-1工程&lt;1区域&gt;, 220020100 オモチャ工場-1工程&lt;2区域&gt;, 220020200 オモチャ工場-1工程&lt;3区域&gt; … | `Reference/Cosmic/scripts/reactor/2202002.js` |
| 2302000 | 11: 230010000 海の道, 230010001 ペンギンの遊び場, 230010100 水晶の峡谷 … | `Reference/Cosmic/scripts/reactor/2302000.js` |
| 1072000 | 8: 107000000 さまよい沼1, 107000100 さまよい沼2, 107000200 さまよい沼3 … | `Reference/Cosmic/scripts/reactor/1072000.js` |
| 2112013 | 8: 280010000 知られざる閉鉱, 280010011 1-2区域, 280010040 4-1区域 … | `Reference/Cosmic/scripts/reactor/2112013.js` |
| 2202003 | 8: 922010200 放置された塔&lt;2段階&gt;, 922010201 塔の罠, 922010501 塔の迷路１ … | `Reference/Cosmic/scripts/reactor/2202003.js` |
| 2112001 | 7: 280010040 4-1区域, 280010091 9-2区域, 280011000 16区域&lt;どこかにある閉鉱&gt; … | `Reference/Cosmic/scripts/reactor/2112001.js` |
| 1102000 | 6: 110010000 海の家, 110020000 ロランロラン, 110020001 ロランロランロラン … | `Reference/Cosmic/scripts/reactor/1102000.js` |
| 1102001 | 6: 110010000 海の家, 110020000 ロランロラン, 110020001 ロランロランロラン … | `Reference/Cosmic/scripts/reactor/1102001.js` |
| 1102002 | 6: 110010000 海の家, 110020000 ロランロラン, 110020001 ロランロランロラン … | `Reference/Cosmic/scripts/reactor/1102002.js` |
| 2002017 | 6: 920010911 監獄倉庫1, 920010912 監獄倉庫1, 920010921 監獄倉庫2 … | `Reference/Cosmic/scripts/reactor/2002017.js` |
| 2222000 | 6: 222010000 カラス山の入口, 222010001 カラス山の麓, 222010002 小さい泉 … | `Reference/Cosmic/scripts/reactor/2222000.js` |
| 2302001 | 6: 230040000 深淵海峡1, 230040100 深淵海峡2, 230040200 危険な海峡1 … | `Reference/Cosmic/scripts/reactor/2302001.js` |
| 1012000 | 5: 101010000 エリニアの北側フィールド, 101010100 そびえ立つ木1, 101010101 そびえ立つ木2 … | `Reference/Cosmic/scripts/reactor/1012000.js` |
| 2112004 | 5: 280010091 9-2区域, 280010110 11-1区域, 280010140 14-1区域 … | `Reference/Cosmic/scripts/reactor/2112004.js` |
| 2008007 | 4: 920010700 登り道, 920010910 監獄１, 920010920 監獄2 … | `Reference/Cosmic/scripts/reactor/2008007.js` |
| 2112000 | 4: 280010050 5-1区域, 280010081 8-2区域, 280010130 13-1区域 … | `Reference/Cosmic/scripts/reactor/2112000.js` |
| 2112009 | 4: 280010011 1-2区域, 280010040 4-1区域, 280010110 11-1区域 … | `Reference/Cosmic/scripts/reactor/2112009.js` |
| 2302002 | 4: 230030000 青い海草の道, 230030001 魚達の憩いの広場, 230030100 キノコ珊瑚の丘 … | `Reference/Cosmic/scripts/reactor/2302002.js` |
| 2408002 | 4: 240050101 一番目の迷路部屋, 240050102 二番目の迷路部屋, 240050103 三番目の迷路部屋 … | `Reference/Cosmic/scripts/reactor/2408002.js` |
| 2612003 | 4: 926100201 暗い研究室1, 926100202 暗い研究室2, 926110201 暗い研究室1 … | `Reference/Cosmic/scripts/reactor/2612003.js` |
| 1021000 | 3: 910200000 未公開の遺跡１, 910200001 未公開の遺跡２, 910200002 未公開の遺跡３ | `Reference/Cosmic/scripts/reactor/1021000.js` |
| 1021001 | 3: 910200000 未公開の遺跡１, 910200001 未公開の遺跡２, 910200002 未公開の遺跡３ | `Reference/Cosmic/scripts/reactor/1021001.js` |
| 1022000 | 3: 910200000 未公開の遺跡１, 910200001 未公開の遺跡２, 910200002 未公開の遺跡３ | `Reference/Cosmic/scripts/reactor/1022000.js` |
| 2002018 | 3: 920010912 監獄倉庫1, 920010922 監獄倉庫2, 920010932 監獄倉庫3 | `Reference/Cosmic/scripts/reactor/2002018.js` |
| 2112003 | 3: 280010081 8-2区域, 280011000 16区域&lt;どこかにある閉鉱&gt;, 280011005 16区域5 | `Reference/Cosmic/scripts/reactor/2112003.js` |
| 2200001 | 3: 220030200 オモチャ工場-メイン工程2, 220030300 オモチャ工場-2工程&lt;3 区域&gt;, 220030400 オモチャ工場-2工程&lt;4 区域&gt; | `Reference/Cosmic/scripts/reactor/2200001.js` |
| 2502000 | 3: 250010500 天桃の果樹園1, 250010600 天桃の果樹園2, 250010700 天桃の果樹園3 | `Reference/Cosmic/scripts/reactor/2502000.js` |
| 2502001 | 3: 250010500 天桃の果樹園1, 250010600 天桃の果樹園2, 250010700 天桃の果樹園3 | `Reference/Cosmic/scripts/reactor/2502001.js` |
| 2512000 | 3: 925100000 海賊船への道, 925100200 甲板突破1, 925100300 甲板突破2 | `Reference/Cosmic/scripts/reactor/2512000.js` |
| 6802000 | 3: 680000401 ウェディング披露宴会場, 680000600, 889300500 ウェディング披露宴会場(ハウスウェディング) | `Reference/Cosmic/scripts/reactor/6802000.js` |
| 6802001 | 3: 680000401 ウェディング披露宴会場, 680000600, 889300500 ウェディング披露宴会場(ハウスウェディング) | `Reference/Cosmic/scripts/reactor/6802001.js` |
| 8091002 | 3: 809050002 迷路, 809050003 迷路, 809050008 迷路 | `Reference/Cosmic/scripts/reactor/8091002.js` |
| 2001 | 2: 1000000 アムホスト, 1000002 アムホストの民家 | `Reference/Cosmic/scripts/reactor/2001.js` |
| 2002000 | 2: 200000000 オルビス, 200000200 オルビス公園 | `Reference/Cosmic/scripts/reactor/2002000.js` |
| 2112006 | 2: 280010031 3-2区域, 280010101 10-2区域 | `Reference/Cosmic/scripts/reactor/2112006.js` |
| 2112008 | 2: 280010100 10-1区域, 280010120 12-1区域 | `Reference/Cosmic/scripts/reactor/2112008.js` |
| 2112010 | 2: 280010020 2-1区域, 280011004 16区域4 | `Reference/Cosmic/scripts/reactor/2112010.js` |
| 2112011 | 2: 280010041 4-2区域, 280011005 16区域5 | `Reference/Cosmic/scripts/reactor/2112011.js` |
| 2408003 | 2: 240060000 試験の洞窟1, 240060100 試験の洞窟2 | `Reference/Cosmic/scripts/reactor/2408003.js` |
| 2511001 | 2: 925100200 甲板突破1, 925100300 甲板突破2 | `Reference/Cosmic/scripts/reactor/2511001.js` |
| 2512001 | 2: 925100201 100年生キキョウの領域1, 925100301 100年生キキョウの領域2 | `Reference/Cosmic/scripts/reactor/2512001.js` |
| 2612001 | 2: 926100202 暗い研究室2, 926110202 暗い研究室2 | `Reference/Cosmic/scripts/reactor/2612001.js` |
| 2612002 | 2: 926100201 暗い研究室1, 926110201 暗い研究室1 | `Reference/Cosmic/scripts/reactor/2612002.js` |
| 2618000 | 2: 926100100 不快な実験室, 926110100 不快な実験室 | `Reference/Cosmic/scripts/reactor/2618000.js` |
| 2618001 | 2: 926100200 特殊な実験室, 926110200 特殊な実験室 | `Reference/Cosmic/scripts/reactor/2618001.js` |
| 2618002 | 2: 926100200 特殊な実験室, 926110200 特殊な実験室 | `Reference/Cosmic/scripts/reactor/2618002.js` |
| 6109000 | 2: 803000800 忘れられた保管庫, 803000900 疾さの試練 | `Reference/Cosmic/scripts/reactor/6109000.js` |
| 6109001 | 2: 803000800 忘れられた保管庫, 803000900 疾さの試練 | `Reference/Cosmic/scripts/reactor/6109001.js` |
| 6109002 | 2: 803000800 忘れられた保管庫, 803000900 疾さの試練 | `Reference/Cosmic/scripts/reactor/6109002.js` |
| 6109003 | 2: 803000800 忘れられた保管庫, 803000900 疾さの試練 | `Reference/Cosmic/scripts/reactor/6109003.js` |
| 6109004 | 2: 803000800 忘れられた保管庫, 803000900 疾さの試練 | `Reference/Cosmic/scripts/reactor/6109004.js` |
| 8091000 | 2: 809050000 迷路, 809050010 迷路 | `Reference/Cosmic/scripts/reactor/8091000.js` |
| 8091001 | 2: 809050001 迷路, 809050009 迷路 | `Reference/Cosmic/scripts/reactor/8091001.js` |
| 8091003 | 2: 809050004 迷路, 809050007 迷路 | `Reference/Cosmic/scripts/reactor/8091003.js` |
| 8091004 | 2: 809050005 迷路, 809050006 迷路 | `Reference/Cosmic/scripts/reactor/8091004.js` |
| 9202000 | 2: 990000100 守護の谷, 990000200 遺跡の入口 | `Reference/Cosmic/scripts/reactor/9202000.js` |
| 9202001 | 2: 990000410 名誉の部屋, 990000431 制約の部屋 | `Reference/Cosmic/scripts/reactor/9202001.js` |
| 9202009 | 2: 990000100 守護の谷, 990000200 遺跡の入口 | `Reference/Cosmic/scripts/reactor/9202009.js` |
| 9202012 | 2: 990001000 シャレン3世の部屋, 990001101 ギルド協会キャンプ | `Reference/Cosmic/scripts/reactor/9202012.js` |
| 9208000 | 2: 910210000 シャレニアン城門, 990000300 シャレニアン城門 | `Reference/Cosmic/scripts/reactor/9208000.js` |
| 9208001 | 2: 910210000 シャレニアン城門, 990000300 シャレニアン城門 | `Reference/Cosmic/scripts/reactor/9208001.js` |
| 2000 | 1: 200000300 出会いの丘 | `Reference/Cosmic/scripts/reactor/2000.js` |
| 1020000 | 1: 910200000 未公開の遺跡１ | `Reference/Cosmic/scripts/reactor/1020000.js` |
| 1020001 | 1: 910200000 未公開の遺跡１ | `Reference/Cosmic/scripts/reactor/1020001.js` |
| 1020002 | 1: 910200000 未公開の遺跡１ | `Reference/Cosmic/scripts/reactor/1020002.js` |
| 1022001 | 1: 101030101 遺跡発掘地1 | `Reference/Cosmic/scripts/reactor/1022001.js` |
| 1029000 | 1: 101040001 ワイルドボアの棲みか | `Reference/Cosmic/scripts/reactor/1029000.js` |
| 1050000 | 1: 910500200 忘れられた神殿 | `Reference/Cosmic/scripts/reactor/1050000.js` |
| 1052000 | 1: 910500200 忘れられた神殿 | `Reference/Cosmic/scripts/reactor/1052000.js` |
| 1052001 | 1: 105100301 バルログが消えた場所 | `Reference/Cosmic/scripts/reactor/1052001.js` |
| 1052002 | 1: 105100401 イージーモード_バルログが消えた場所 | `Reference/Cosmic/scripts/reactor/1052002.js` |
| 1200000 | 1: 912020000 バトの部屋 | `Reference/Cosmic/scripts/reactor/1200000.js` |
| 1202002 | 1: 120000301 動力室 | `Reference/Cosmic/scripts/reactor/1202002.js` |
| 1209000 | 1: 912020000 バトの部屋 | `Reference/Cosmic/scripts/reactor/1209000.js` |
| 1302000 | 1: 130030004 開始の森5 | `Reference/Cosmic/scripts/reactor/1302000.js` |
| 1402000 | 1: 140090400 冷たい森4 | `Reference/Cosmic/scripts/reactor/1402000.js` |
| 2001000 | 1: 920010800 庭園 | `Reference/Cosmic/scripts/reactor/2001000.js` |
| 2001001 | 1: 920010800 庭園 | `Reference/Cosmic/scripts/reactor/2001001.js` |
| 2001002 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001002.js` |
| 2001003 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001003.js` |
| 2001004 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001004.js` |
| 2001005 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001005.js` |
| 2001006 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001006.js` |
| 2001007 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001007.js` |
| 2001008 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001008.js` |
| 2001009 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001009.js` |
| 2001010 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001010.js` |
| 2001011 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001011.js` |
| 2001012 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001012.js` |
| 2001013 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001013.js` |
| 2001014 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001014.js` |
| 2001015 | 1: 920010300 倉庫 | `Reference/Cosmic/scripts/reactor/2001015.js` |
| 2001016 | 1: 920010800 庭園 | `Reference/Cosmic/scripts/reactor/2001016.js` |
| 2002001 | 1: 920010000 入口 | `Reference/Cosmic/scripts/reactor/2002001.js` |
| 2002002 | 1: 920010604 客室304号 | `Reference/Cosmic/scripts/reactor/2002002.js` |
| 2002003 | 1: 920010800 庭園 | `Reference/Cosmic/scripts/reactor/2002003.js` |
| 2002004 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002004.js` |
| 2002005 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002005.js` |
| 2002006 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002006.js` |
| 2002007 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002007.js` |
| 2002008 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002008.js` |
| 2002009 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002009.js` |
| 2002010 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002010.js` |
| 2002011 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2002011.js` |
| 2002012 | 1: 920010500 封印された部屋 | `Reference/Cosmic/scripts/reactor/2002012.js` |
| 2002013 | 1: 920010700 登り道 | `Reference/Cosmic/scripts/reactor/2002013.js` |
| 2002014 | 1: 920011100 宝物倉庫 | `Reference/Cosmic/scripts/reactor/2002014.js` |
| 2006000 | 1: 920010000 入口 | `Reference/Cosmic/scripts/reactor/2006000.js` |
| 2006001 | 1: 920010100 中央塔 | `Reference/Cosmic/scripts/reactor/2006001.js` |
| 2008006 | 1: 920010400 休憩室 | `Reference/Cosmic/scripts/reactor/2008006.js` |
| 2111001 | 1: 280030000 ジャクムの祭壇 | `Reference/Cosmic/scripts/reactor/2111001.js` |
| 2112005 | 1: 280011005 16区域5 | `Reference/Cosmic/scripts/reactor/2112005.js` |
| 2112007 | 1: 280011001 16区域1 | `Reference/Cosmic/scripts/reactor/2112007.js` |
| 2112014 | 1: 280011005 16区域5 | `Reference/Cosmic/scripts/reactor/2112014.js` |
| 2112015 | 1: 921100000 溶岩の心臓部 | `Reference/Cosmic/scripts/reactor/2112015.js` |
| 2112016 | 1: 921100200 フェニックスの巣 | `Reference/Cosmic/scripts/reactor/2112016.js` |
| 2112017 | 1: 921100210 フリズベルクの巣 | `Reference/Cosmic/scripts/reactor/2112017.js` |
| 2119000 | 1: 211041100 死んだ木の森1 | `Reference/Cosmic/scripts/reactor/2119000.js` |
| 2119001 | 1: 211041200 死んだ木の森2 | `Reference/Cosmic/scripts/reactor/2119001.js` |
| 2119002 | 1: 211041300 死んだ木の森3 | `Reference/Cosmic/scripts/reactor/2119002.js` |
| 2119003 | 1: 211041400 死んだ木の森4 | `Reference/Cosmic/scripts/reactor/2119003.js` |
| 2119004 | 1: 211010000 凍結注意！凍りの道1 | `Reference/Cosmic/scripts/reactor/2119004.js` |
| 2119005 | 1: 211020000 凍結注意！凍りの道2 | `Reference/Cosmic/scripts/reactor/2119005.js` |
| 2119006 | 1: 211050000 凍てつく野原 | `Reference/Cosmic/scripts/reactor/2119006.js` |
| 2200000 | 1: 922000010 人形の家 | `Reference/Cosmic/scripts/reactor/2200000.js` |
| 2200002 | 1: 922010200 放置された塔&lt;2段階&gt; | `Reference/Cosmic/scripts/reactor/2200002.js` |
| 2201000 | 1: 922000020 秘密工程１ | `Reference/Cosmic/scripts/reactor/2201000.js` |
| 2201001 | 1: 922010300 放置された塔&lt;3段階&gt; | `Reference/Cosmic/scripts/reactor/2201001.js` |
| 2201002 | 1: 922010700 放置された塔&lt;7段階&gt; | `Reference/Cosmic/scripts/reactor/2201002.js` |
| 2201003 | 1: 922010900 時空の亀裂 | `Reference/Cosmic/scripts/reactor/2201003.js` |
| 2201004 | 1: 220080001 時計塔の深層部 | `Reference/Cosmic/scripts/reactor/2201004.js` |
| 2202000 | 1: 922000010 人形の家 | `Reference/Cosmic/scripts/reactor/2202000.js` |
| 2202001 | 1: 922000000 オモチャ工場&lt;4 区域&gt; | `Reference/Cosmic/scripts/reactor/2202001.js` |
| 2202004 | 1: 922011000 放置された塔&lt;ボーナス&gt; | `Reference/Cosmic/scripts/reactor/2202004.js` |
| 2212000 | 1: 221030401 プラティアンの草原 | `Reference/Cosmic/scripts/reactor/2212000.js` |
| 2212001 | 1: 221030501 メカティアンの草原 | `Reference/Cosmic/scripts/reactor/2212001.js` |
| 2212002 | 1: 221030301 ガンティアンの草原 | `Reference/Cosmic/scripts/reactor/2212002.js` |
| 2212003 | 1: 221040402 ドゴンス基地 | `Reference/Cosmic/scripts/reactor/2212003.js` |
| 2212004 | 1: 221000200 格納庫 | `Reference/Cosmic/scripts/reactor/2212004.js` |
| 2212005 | 1: 221040100 クーラン草原2 | `Reference/Cosmic/scripts/reactor/2212005.js` |
| 2221000 | 1: 222010401 山奥の廃屋 | `Reference/Cosmic/scripts/reactor/2221000.js` |
| 2221001 | 1: 222010401 山奥の廃屋 | `Reference/Cosmic/scripts/reactor/2221001.js` |
| 2221002 | 1: 222010401 山奥の廃屋 | `Reference/Cosmic/scripts/reactor/2221002.js` |
| 2221003 | 1: 222000000 下町 | `Reference/Cosmic/scripts/reactor/2221003.js` |
| 2221004 | 1: 222000000 下町 | `Reference/Cosmic/scripts/reactor/2221004.js` |
| 2229009 | 1: 222010300 狐の丘 | `Reference/Cosmic/scripts/reactor/2229009.js` |
| 2302003 | 1: 923000100 冷たい祭壇 | `Reference/Cosmic/scripts/reactor/2302003.js` |
| 2302005 | 1: 923010000 ケンタの飼育室 | `Reference/Cosmic/scripts/reactor/2302005.js` |
| 2401000 | 1: 240060200 ホーンテイルの洞窟 | `Reference/Cosmic/scripts/reactor/2401000.js` |
| 2401001 | 1: 924000100 空の巣の端 | `Reference/Cosmic/scripts/reactor/2401001.js` |
| 2401002 | 1: 924000100 空の巣の端 | `Reference/Cosmic/scripts/reactor/2401002.js` |
| 2402006 | 1: 240050310 闇の洞窟 | `Reference/Cosmic/scripts/reactor/2402006.js` |
| 2406000 | 1: 240040611 ナインスピリット | `Reference/Cosmic/scripts/reactor/2406000.js` |
| 2408004 | 1: 240040611 ナインスピリット | `Reference/Cosmic/scripts/reactor/2408004.js` |
| 2502002 | 1: 250010301 野生熊の領土1 | `Reference/Cosmic/scripts/reactor/2502002.js` |
| 2511000 | 1: 925100000 海賊船への道 | `Reference/Cosmic/scripts/reactor/2511000.js` |
| 2516000 | 1: 925100500 海賊船長の威厳 | `Reference/Cosmic/scripts/reactor/2516000.js` |
| 2519000 | 1: 925100400 海賊退治! | `Reference/Cosmic/scripts/reactor/2519000.js` |
| 2519001 | 1: 925100400 海賊退治! | `Reference/Cosmic/scripts/reactor/2519001.js` |
| 2519002 | 1: 925100400 海賊退治! | `Reference/Cosmic/scripts/reactor/2519002.js` |
| 2519003 | 1: 925100400 海賊退治! | `Reference/Cosmic/scripts/reactor/2519003.js` |
| 2602000 | 1: 926000010 王妃の宝物庫 | `Reference/Cosmic/scripts/reactor/2602000.js` |
| 2612000 | 1: 926120300 雪園バラが育つ地 | `Reference/Cosmic/scripts/reactor/2612000.js` |
| 2612004 | 1: 261000010 ジェニミスト協会 | `Reference/Cosmic/scripts/reactor/2612004.js` |
| 2612005 | 1: 926130101 ユレテの実験室１ | `Reference/Cosmic/scripts/reactor/2612005.js` |
| 2619000 | 1: 926120100 閉鎖された研究室 | `Reference/Cosmic/scripts/reactor/2619000.js` |
| 2619003 | 1: 261020200 研究所B-1区域 | `Reference/Cosmic/scripts/reactor/2619003.js` |
| 2619004 | 1: 261020400 研究所C-2区域 | `Reference/Cosmic/scripts/reactor/2619004.js` |
| 2619005 | 1: 261020600 研究所B-3区域 | `Reference/Cosmic/scripts/reactor/2619005.js` |
| 2708000 | 1: 270050100 神々の黄昏 | `Reference/Cosmic/scripts/reactor/2708000.js` |
| 3001000 | 1: 930000600 毒の森 | `Reference/Cosmic/scripts/reactor/3001000.js` |
| 3002000 | 1: 930000200 変質した森 | `Reference/Cosmic/scripts/reactor/3002000.js` |
| 3002001 | 1: 930000500 森の広場 | `Reference/Cosmic/scripts/reactor/3002001.js` |
| 3008000 | 1: 930000800 森出口 | `Reference/Cosmic/scripts/reactor/3008000.js` |
| 3009000 | 1: 930000200 変質した森 | `Reference/Cosmic/scripts/reactor/3009000.js` |
| 6102002 | 1: 803001300 隠された兵器庫 | `Reference/Cosmic/scripts/reactor/6102002.js` |
| 6102003 | 1: 803001300 隠された兵器庫 | `Reference/Cosmic/scripts/reactor/6102003.js` |
| 6102004 | 1: 803011300 隠された兵器庫 | `Reference/Cosmic/scripts/reactor/6102004.js` |
| 6102005 | 1: 803011300 隠された兵器庫 | `Reference/Cosmic/scripts/reactor/6102005.js` |
| 6109005 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/reactor/6109005.js` |
| 6109006 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/reactor/6109006.js` |
| 6109007 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/reactor/6109007.js` |
| 6109008 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/reactor/6109008.js` |
| 6109009 | 1: 803001100 統一の試練 | `Reference/Cosmic/scripts/reactor/6109009.js` |
| 6109013 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109013.js` |
| 6109014 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109014.js` |
| 6109016 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109016.js` |
| 6109017 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109017.js` |
| 6109018 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109018.js` |
| 6109019 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109019.js` |
| 6109020 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109020.js` |
| 6109021 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109021.js` |
| 6109022 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109022.js` |
| 6109023 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109023.js` |
| 6109024 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109024.js` |
| 6109025 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109025.js` |
| 6109026 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109026.js` |
| 6109027 | 1: 803001000 機知の試練 | `Reference/Cosmic/scripts/reactor/6109027.js` |
| 8001000 | 1: 801040100 悪夢の果て | `Reference/Cosmic/scripts/reactor/8001000.js` |
| 9101000 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9101000.js` |
| 9102000 | 1: 200090011 船室&lt;オルビス行き&gt; | `Reference/Cosmic/scripts/reactor/9102000.js` |
| 9102001 | 1: 101000003 魔法図書館 | `Reference/Cosmic/scripts/reactor/9102001.js` |
| 9102002 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9102002.js` |
| 9102003 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9102003.js` |
| 9102004 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9102004.js` |
| 9102005 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9102005.js` |
| 9102006 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9102006.js` |
| 9102007 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9102007.js` |
| 9108000 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9108000.js` |
| 9108001 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9108001.js` |
| 9108002 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9108002.js` |
| 9108003 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9108003.js` |
| 9108004 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9108004.js` |
| 9108005 | 1: 910010000 月見草の坂 | `Reference/Cosmic/scripts/reactor/9108005.js` |
| 9201000 | 1: 990000630 水路の迷路3 | `Reference/Cosmic/scripts/reactor/9201000.js` |
| 9201001 | 1: 990000700 シャレン3世の墓 | `Reference/Cosmic/scripts/reactor/9201001.js` |
| 9201002 | 1: 990000900 エレゴスの王子 | `Reference/Cosmic/scripts/reactor/9201002.js` |
| 9202002 | 1: 990000430 信念の部屋 | `Reference/Cosmic/scripts/reactor/9202002.js` |
| 9202003 | 1: 990000501 中央宴会室 | `Reference/Cosmic/scripts/reactor/9202003.js` |
| 9202004 | 1: 990000502 酒貯蔵庫 | `Reference/Cosmic/scripts/reactor/9202004.js` |
| 9202005 | 1: 990000611 迷路の終りA | `Reference/Cosmic/scripts/reactor/9202005.js` |
| 9202006 | 1: 990000620 水路の迷路2 | `Reference/Cosmic/scripts/reactor/9202006.js` |
| 9202007 | 1: 990000631 迷路の終りB | `Reference/Cosmic/scripts/reactor/9202007.js` |
| 9202008 | 1: 990000641 迷路の終りC | `Reference/Cosmic/scripts/reactor/9202008.js` |
| 9208002 | 1: 990000300 シャレニアン城門 | `Reference/Cosmic/scripts/reactor/9208002.js` |
| 9208004 | 1: 990000400 騎士のホール | `Reference/Cosmic/scripts/reactor/9208004.js` |
| 9208007 | 1: 990000440 正義の部屋 | `Reference/Cosmic/scripts/reactor/9208007.js` |
| 9208009 | 1: 990000800 王の回廊 | `Reference/Cosmic/scripts/reactor/9208009.js` |

## Cosmic event scripts (PQ / boss / ride instances) that name JMS maps

| event | JMS maps referenced |
|---|---|
| 3rdJob_bowman | 3: 105040305 スリーピーダンジョン5, 108010100 光る水晶の通路, 108010101 次元の世界 |
| 3rdJob_magician | 3: 100040106 邪気の森2, 108010200 光る水晶の通路, 108010201 次元の世界 |
| 3rdJob_mount | 2: 923010000 ケンタの飼育室, 923010100 飼育室通路 |
| 3rdJob_pirate | 3: 105070200 エビルアイの巣２, 108010500 光る水晶の通路, 108010501 次元の世界 |
| 3rdJob_thief | 3: 107000402 ルーパン沼2, 108010400 光る水晶の通路, 108010401 次元の世界 |
| 3rdJob_warrior | 3: 105070001 アリの巣-広場, 108010300 光る水晶の通路, 108010301 次元の世界 |
| 4jaerial | 2: 120000102 ジョナサンの部屋, 912020000 バトの部屋 |
| 4jship | 3: 120000101 航海室, 912010000 カイリンの訓練場, 912010200 カイリンの訓練場 |
| 4jsuper | 3: 120000101 航海室, 912010100 カイリンの訓練場, 912010200 カイリンの訓練場 |
| AirPlane | 1: 103000000 カニングシティー |
| Aran_2ndmount | 2: 211050000 凍てつく野原, 921110000 プニの原野 |
| Aran_3rdmount | 2: 140010210 オオカミの原野, 914030000 オオカミの試験 |
| AreaBossBamboo | 2: 251010101 60年生薬草の畑, 800020120 地図から消えた集落 |
| AreaBossCentipede | 1: 251010102 80年生薬草の畑 |
| AreaBossDeo | 1: 260010201 ローヤルカツスの砂漠 |
| AreaBossDyle | 1: 107000300 危険なクロコ1 |
| AreaBossEliza1 | 1: 200010300 空の階段2 |
| AreaBossFaust1 | 1: 100040105 邪気の森1 |
| AreaBossFaust2 | 1: 100040106 邪気の森2 |
| AreaBossKimera | 1: 261030000 暗黒の魔法使いの研究室へ続く道 |
| AreaBossKingClang | 1: 110040000 熱砂地帯 |
| AreaBossKingSageCat | 1: 250010504 妖怪の森2 |
| AreaBossLeviathan | 1: 240040401 レヴィアタンの峡谷 |
| AreaBossMano | 1: 104000400 海岸の草原3 |
| AreaBossNineTailedFox | 1: 222010310 月の丘 |
| AreaBossSeruf | 1: 230020100 海草の塔 |
| AreaBossSnackBar | 1: 105090310 ドレイクの棲み処 |
| AreaBossStumpy | 1: 101030404 東の岩山5 |
| AreaBossTaeRoon | 1: 250010304 風来坊熊の領土 |
| AreaBossTimer1 | 1: 220050100 時間の渦巻 |
| AreaBossTimer2 | 1: 220050000 なくした時間1 |
| AreaBossTimer3 | 1: 220050200 なくした時間2 |
| AreaBossZeno | 1: 221040301 グレイの草原 |
| BalrogBattle | 3: 105100100 神殿の底, 105100300 バルログの墓, 105100301 バルログが消えた場所 |
| BalrogBattle_Easy | 3: 105100100 神殿の底, 105100400 イージーモード_バルログの墓, 105100401 イージーモード_バルログが消えた場所 |
| BalrogQuest | 2: 105100100 神殿の底, 910520000 英雄の記憶 |
| Boats | 9: 101000300 エリニアステーション, 101000301 控え室&lt;オルビス行き&gt;, 200000100 オルビスチケット売場, 200000111 ステーション&lt;エリニア行き&gt; … |
| Cabin | 8: 200000100 オルビスチケット売場, 200000131 ステーション&lt;リプレ行き&gt;, 200000132 控え室&lt;リプレ行き&gt;, 200090200 リプレ行き … |
| CafePQ_1 | 4: 190000000 別の寺院, 190000001 息を吹く寺院, 190000002 息を吹く寺院Ⅱ, 193000000 ネットカフェ |
| CafePQ_2 | 3: 191000000 サルの森, 191000001 木のダンジョン, 193000000 ネットカフェ |
| CafePQ_3 | 3: 192000000 不毛の岩山Ⅰ, 192000001 不毛の岩山Ⅱ, 193000000 ネットカフェ |
| CafePQ_4 | 5: 193000000 ネットカフェ, 195000000 危ないアリの巣, 195010000 エビルアイの洞窟, 195020000 光を失った洞窟 … |
| CafePQ_5 | 3: 193000000 ネットカフェ, 196000000 冷え冷えの坂, 196010000 冷え冷えの絶壁 |
| CafePQ_6 | 3: 193000000 ネットカフェ, 197000000 風テラス1, 197010000 風テラス2 |
| Cygnus_Magic_Library | 2: 101000000 エリニア, 910110000 魔法図書館 |
| DelliBattle | 2: 925010200 デリーを探しに3, 925010300 デリーを守れ！ |
| DollHouse | 2: 221024400 エオス塔100階, 922000010 人形の家 |
| ElementalBattle | 2: 220050300 時間の通路, 922020100 タナトスの部屋 |
| Elevator | 6: 222020100 ヘリオス塔2階, 222020110 エレベーター&lt;ルディブリアム行き&gt;, 222020111 エレベーター&lt;ルディブリアム行き&gt;, 222020200 ヘリオス塔99階 … |
| EllinPQ | 10: 300030100 深い妖精の森, 930000000 森の前, 930000100 森の端, 930000200 変質した森 … |
| ElnathPQ | 3: 211000001 長老の官邸, 211040100 氷の谷1, 921100300 タイラスを守れ |
| Genie | 7: 200000100 オルビスチケット売場, 200000151 ステーション&lt;アリアント行き&gt;, 200000152 控え室&lt;アリアント行き&gt;, 200090400 アリアント行き … |
| GuildQuest | 30: 101030104 遺跡発掘ベースキャンプ, 990000000 遺跡発掘現場, 990000100 守護の谷, 990000200 遺跡の入口 … |
| Hak | 4: 200000141 ステーション通路&lt;武陵行き&gt;, 200090300 武陵行き, 200090310 航海中&lt;オルビス行き&gt;, 250000100 武陵神社 |
| HenesysPQ | 6: 100000200 広場, 910010000 月見草の坂, 910010100 近道, 910010200 豚の村 … |
| HorntailBattle | 5: 240050400 ホーンテイルの洞窟入口, 240050600 ホーンテイルの洞窟, 240060000 試験の洞窟1, 240060100 試験の洞窟2 … |
| HorntailPQ | 11: 240050000 洞窟の入口, 240050100 迷路部屋, 240050101 一番目の迷路部屋, 240050102 二番目の迷路部屋 … |
| KerningPQ | 4: 103000000 カニングシティー, 103000800 一つ目の同行&lt;1st&gt;, 103000805 一つ目の同行&lt;Bonus&gt;, 103000890 一つ目の同行&lt;出口&gt; |
| KerningTrain | 4: 103000100 切符売り場, 103000301 地下鉄&lt;カニングスクエア行き&gt;, 103000302 地下鉄&lt;カニングスクエア行き&gt;, 103000310 カニングスクエア駅 |
| KingPepeAndYetis | 2: 106021400 東の塔, 106021500 結婚式場入口 |
| LudiMazePQ | 4: 220000000 ルディブリアム, 809050000 迷路, 809050016 商品交換所, 809050017 イベント出口 |
| LudiPQ | 25: 221024500 エオス塔101階, 922010000 放置された塔&lt;冒険の終わり&gt;, 922010100 放置された塔&lt;1段階&gt;, 922010200 放置された塔&lt;2段階&gt; … |
| MK_PrimeMinister | 2: 106021402 最後の城塔, 106021600 結婚式場 |
| MK_PrimeMinister2 | 2: 106021402 最後の城塔, 106021601 結婚式場 |
| MagatiaPQ_A | 18: 261000021 アルカドノ秘密の部屋, 926110000 怪しい研究室, 926110001 暗い通路, 926110100 不快な実験室 … |
| MagatiaPQ_Z | 18: 261000011 ジェニミスト秘密の部屋, 926100000 怪しい研究室, 926100001 暗い通路, 926100100 不快な実験室 … |
| MahaBattle | 2: 140000000 リエン村, 914020000 マッハとの対峙 |
| NineSpirit | 2: 240040610 危険な巣の下, 240040611 ナインスピリット |
| OrbisPQ | 28: 200080101 見知らぬ塔, 920010000 入口, 920010100 中央塔, 920010200 散歩路 … |
| PapulatusBattle | 2: 220080000 時計塔の奥, 220080001 時計塔の深層部 |
| PinkBeanBattle | 4: 270050000 忘れられた黄昏, 270050100 神々の黄昏, 270050200 失われた黄昏, 270050300 黄昏の黎明の間 |
| PiratePQ | 13: 251010404 海賊船の向こう, 925100000 海賊船への道, 925100100 船首突破, 925100200 甲板突破1 … |
| Puppeteer | 2: 105070300 エビルアイの巣３, 910510000 人形使いの洞窟 |
| RescueGaga | 3: 922240000 ガガを救出せよ！, 922240100 ガガ救出成功!?, 922240200 ガガ救出失敗 |
| RockSpirit | 3: 103040400 7階 8階 A区域, 103040410 7階 8階 B区域, 103040420 7階 8階 C区域 |
| RockSpiritVIP | 2: 103040400 7階 8階 A区域, 103040410 7階 8階 B区域 |
| ShowaBattle | 3: 801040004 武器庫, 801040100 悪夢の果て, 801040101 アジト前(天晴れ) |
| Subway | 1: 103000100 切符売り場 |
| Trains | 8: 200000100 オルビスチケット売場, 200000121 ステーション&lt;ルディブリアム行き&gt;, 200000122 控え室&lt;ルディブリアム行き&gt;, 200090100 ルディブリアム行き … |
| WeddingCathedral | 5: 680000000 ウェディングタウン, 680000200 ウェディングホール待機室(大聖堂), 680000400 ジュエリープラザ, 680000401 ウェディング披露宴会場 … |
| WeddingChapel | 4: 680000000 ウェディングタウン, 680000400 ジュエリープラザ, 680000401 ウェディング披露宴会場, 680000500 出口 |
| ZakumBattle | 3: 211042300 ジャクムへの門, 211042400 ジャクムの祭壇入口, 280030000 ジャクムの祭壇 |
| ZakumPQ | 31: 211042300 ジャクムへの門, 280010000 知られざる閉鉱, 280010010 1-1区域, 280010011 1-2区域 … |
| q3239 | 2: 922000000 オモチャ工場&lt;4 区域&gt;, 922000009 秘密通路 |
| s4aWorld | 2: 105090200 別世界への扉, 910500000 弓使いの修練場 |
