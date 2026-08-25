# NPC coverage (generated — do not edit by hand)

Regenerate with `python DevTools/npc_coverage.py` (needs gamedata.db).

Status meaning: **script** = server script exists / **shop** = vendor via the shop
table / **quest-data** = the client's own quest UI drives it (data-driven accept &
complete work server-side) / **none** = nothing behind it yet (the click falls back
to the generic line). The wz-hint column shows what the ORIGINAL server had for it —
`script:<name>` names Nexon's server script (an authoring lead), trunk = storage,
parcel = home delivery.

## Summary

- NPCs spawning across all maps: **1447**
- script: **211** (14%)
- shop: **90** (6%)
- quest-data: **499** (34%)
- none: **647** (44%)

## Actionable queue (spawning, status=none, with a wz script hint)

| NPC | 名前 | wzヒント | 代表マップ |
|---|---|---|---|
| 1012113 | ウサクン | script:moonrabbit_bonus | 910010100 近道 他2 |
| 1012114 | タイクン | script:moonrabbit_tiger | 910010000 月見草の坂 |
| 1012115 | ヘネシス草むら | script:blackShadowHene1 | 100020000 東の草むら |
| 1012116 | ヘネシス草むら | script:blackShadowHene2 | 100020000 東の草むら |
| 1013001 | ドラゴン | script:dragon_dream | 900010200 夢見る森 |
| 1013002 | ドラゴンの巣 | script:dragonEgg | 900020220 忘れてしまった森 |
| 1013104 | 卵の箱 | script:giveEggEvan | 100030102 前庭 |
| 1013106 | 輝く碑石 | script:evan_lv200 | 100030301 鬱蒼とした森 |
| 1013200 | 赤ちゃんオオカミ | script:babyPig | 900020100 鬱蒼とした森 他1 |
| 1013205 | フリエン | script:Afirentalk | 900030000 陣地裏手 |
| 1013207 | クン | script:contimoveRitSDI | 200090080 용이 잠든 섬으로 他2 |
| 1022103 | 分数彫像 | script:s4strike_statue | 910210000 シャレニアン城門 |
| 1022105 | ヘンケル | script:enter_warrior | 101040000 分かれ道-東 |
| 1022107 | ペリオン警告板 | script:periPatrol | 101030000 東の峰 他4 |
| 1032109 | 魔法図書館隈 | script:blackShadowEli1 | 910110000 魔法図書館 |
| 1032110 | 魔法図書館隈 | script:blackShadowEli2 | 910110000 魔法図書館 |
| 1032111 | 小さい切り株 | script:giveSap | 101010103 渓流&lt;バンジージャンプ台&gt; |
| 1032114 | ヘンケル | script:enter_magicion | 100050000 南フィールド |
| 1043000 | 花の摘み | script:bush1 | 101000101 忍耐の森-2段階 |
| 1043001 | 薬草の藪 | script:bush2 | 101000104 忍耐の森-5段階 |
| 1052008 | 宝箱 | script:subway_get1 | 103000902 B1&lt;地下鉄基地&gt; |
| 1052009 | 宝箱 | script:subway_get2 | 103000905 B2&lt;地下鉄基地&gt; |
| 1052010 | 宝箱 | script:subway_get3 | 103000909 B3&lt;地下鉄基地&gt; |
| 1052011 | 出口 | script:subway_out | 103000900 B1&lt;1区域&gt; 他6 |
| 1052013 | パソコン | script:go_pcmap | 193000000 ネットカフェ |
| 1052107 | 小さな街灯 | script:sca_Shade | 103000105 1号線-4区 他1 |
| 1052109 | 地下鉄のゴミ箱 | script:givebubbleDoll1 | 103000101 1号線-1区 |
| 1052110 | 地下鉄のゴミ箱 | script:givebubbleDoll2 | 103000101 1号線-1区 |
| 1052111 | 地下鉄のゴミ箱 | script:givebubbleDoll3 | 103000101 1号線-1区 |
| 1052112 | 地下鉄のゴミ箱 | script:givebubbleDoll4 | 103000101 1号線-1区 |
| 1052114 | ヘンケル | script:enter_thief | 103010000 工事現場 |
| 1052125 | ガードマン | script:Depart_topFloorIn | 103040400 7階 8階 A区域 |
| 1052126 | 선대 다크로드의 일기장 | script:dual_Diary | 910350100 선대 다크로드의 방 |
| 1061007 | 崩れている石像 | script:flower_out | 105040310 忍耐の森「1段階」 他6 |
| 1061009 | 次元の扉 | script:crack | 100040106 邪気の森2 他4 |
| 1061010 | 光る水晶 | script:3jobExit | 108010101 次元の世界 他5 |
| 1061018 | ムヨン | script:balog_InOut | 105100300 バルログの墓
 他1 |
| 1061100 | ホテルガイド | script:hotel1 | 105040400 スリーピーホテルロビー |
| 1063000 | 桃色の花山 | script:viola_pink | 105040311 忍耐の森「2段階」 |
| 1063001 | 青色の花山 | script:viola_blue | 105040313 忍耐の森「4段階」 |
| 1063002 | 白色の花山 | script:viola_white | 105040316 忍耐の森「7段階」 |
| 1063011 | エビルアイの壁画 | script:Dollcave | 105070300 エビルアイの巣３ |
| 1063012 | 霊験な石1 | script:holySton | 105050200 アリの巣3 他2 |
| 1063013 | 霊験な石2 | script:holySton | 105090000 光を失った洞窟1 他1 |
| 1063016 | 不思議な石像 | script:DollWayKeeper1 | 910510100 人形使いの秘密通路 |
| 1063017 | 不思議な石像 | script:DollWayKeeper2 | 910510100 人形使いの秘密通路 |
| 1072004 | 戦士転職教官 | script:inside_swordman | 108000300 戦士の岩山1 他2 |
| 1072005 | 魔法使い転職教官 | script:inside_magician | 108000200 木のダンジョン1 他2 |
| 1072006 | 弓使い転職教官 | script:inside_archer | 108000100 アリの巣1 他2 |
| 1072007 | 盗賊転職教官 | script:inside_rogue | 108000400 盗賊の工事現場1 他2 |
| 1092016 | 輝く石 | script:nautil_stone | 120000301 動力室 |
| 1092018 | ゴミ箱 | script:nautil_letter | 120000100 上階廊下 |
| 1092090 | 母牛 | script:mom_cow | 912000100 ノーチラスの牛小屋  |
| 1092091 | 母牛 | script:mom_cow | 912000100 ノーチラスの牛小屋  |
| 1092094 | 子牛 | script:baby_cow | 912000100 ノーチラスの牛小屋  |
| 1092095 | 子牛 | script:baby_cow | 912000100 ノーチラスの牛小屋  |
| 1094002 | 草原 | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094003 | 草原 | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094004 | 草原 | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094005 | 草原 | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094006 | 草原 | script:nautil_Abel1 | 120000000 ノーチラス |
| 1095000 | シュリンツ | script:s4mind_out | 925010000 デリーを探しに1 |
| 1095002 | ヘンケル | script:enter_pirate | 120010000 船着場への道 |
| 1102001 | キリコ | script:outSecondDH | 108000600 第2訓練場 他2 |
| 1102003 | キダン | script:cygnus_lv120 | 130000100 騎士の殿堂 他1 |
| 1103005 | ナインハート | script:erebWarp | 913040006 シグナス騎士団 |
| 1104000 | フランシス | script:DollMaster | 910510001 人形使いの本拠地 |
| 1104200 | 倒れた騎士 | script:enterBlackEreb | 924010100 暗黒の魔女の洞窟 |
| 1202009 | 番人ヴォルフ | script:enterWolf | 140010200 氷原野 |
| 1202010 | プディン | script:aran_lv200 | 140010110 英雄の殿堂 他1 |
| 1204001 | フランシス | script:dollMaster00 | 910510200 人形使いの洞窟 |
| 1204005 | トゥルー | script:downTrue | 910400000 危険な情報屋 |
| 1204030 | 図書館書棚 | script:Warehouse | 930010000 危険な魔法図書館 |
| 1204032 | ヘレナ | script:downHelena | 910050000 危険な弓使い学院 |
| 1300012 | 東石塔門 | script:TD_MC_bossEnter | 106021400 東の塔 |
| 1300013 | マックフィンイブグ | script:TD_MC_violetaEnter | 106021402 最後の城塔 |
| 1300014 | セルフ | script:forself | 106020300 奥深きキノコの森 他2 |
| 2010003 | ネーブ | script:make_orbis | 200000200 オルビス公園 |
| 2012000 | イフ | script:sell_ticket | 200000100 オルビスチケット売場 |
| 2012006 | イス | script:getAboard | 200000100 オルビスチケット売場 |
| 2012014 | オルビス魔法石 | script:ossyria3_1 | 200080200 オルビス塔&lt;20層&gt; |
| 2012015 | エルナス魔法石 | script:ossyria3_2 | 200082100 オルビス塔&lt;1層&gt; |
| 2012027 | ヒューズ | script:elizaHarp1 | 920020000 エリジャーの庭園 |
| 2012028 | ハープ弦&lt;レ&gt; | script:elizaHarp2 | 920020000 エリジャーの庭園 |
| 2012029 | ハープ弦&lt;ミ&gt; | script:elizaHarp3 | 920020000 エリジャーの庭園 |
| 2012030 | ハープ弦&lt;ファ&gt; | script:elizaHarp4 | 920020000 エリジャーの庭園 |
| 2012031 | ハープ弦&lt;ソ&gt; | script:elizaHarp5 | 920020000 エリジャーの庭園 |
| 2012032 | ハープ弦&lt;ラ&gt; | script:elizaHarp6 | 920020000 エリジャーの庭園 |
| 2012033 | ハープ弦&lt;シ&gt; | script:elizaHarp7 | 920020000 エリジャーの庭園 |
| 2013001 | 侍従イク | script:party3_play | 920010100 中央塔 他10 |
| 2013002 | 女神ミネルバ | script:party3_minerva | 920011300 女神の祝福 |
| 2022004 | タイラス | script:s4common1_out | 921100301 タイラス護衛完遂 |
| 2023000 | 危険地域弾丸タクシー | script:ossyria_taxi | 211000000 エルナス 他2 |
| 2030006 | 聖なる岩 | script:holyStone | 211040401 雪原の聖地 |
| 2030011 | アーリ | script:Zakum04 | 280090000 悲恋の部屋 |
| 2030014 | 古代氷石 | script:s4freeze_item | 921100100 氷の谷 |
| 2032002 | アウラ | script:Zakum01 | 280010000 知られざる閉鉱 |
| 2032003 | リラー | script:Zakum02 | 280020001 火山の息&lt;2段階&gt; |
| 2040020 | ジロクン | script:make_ludi2 | 220000303 ジロクンとペイの家 |
| 2040021 | ペイ | script:make_ludi3 | 220000303 ジロクンとペイの家 |
| 2040022 | ライドル | script:make_ludi4 | 220020600 オモチャ工場-機械室 |
| 2040024 | 一番目のエオス石 | script:ludi014 | 221024400 エオス塔100階 |
| 2040025 | 二番目のエオス石 | script:ludi015 | 221022900 エオス塔71階 |
| 2040026 | 三番目のエオス石 | script:ludi016 | 221021700 エオス塔41階 |
| 2040027 | 四番目のエオス石 | script:ludi017 | 221020000 エオス塔1階 |
| 2040028 | マークくん | script:ludi024 | 922000010 人形の家 |
| 2040030 | ウィスブ | script:ludi026 | 220000400 エオス塔入口 |
| 2040031 | 文書束 | script:ludi027 | 220000304 クロイの家 |
| 2040032 | ウィーバー | script:ludi028 | 220000006 ルディブリアムの散歩路 |
| 2040033 | ネル | script:ludi029 | 220000006 ルディブリアムの散歩路 |
| 2042001 | シュピゲルマン | script:mc_enter1 | 980000100 カーニバルフィールド1&lt;控え室&gt; 他8 |
| 2042002 | シュピゲルマン | script:mc_move | 103000000 カニングシティー 他19 |
| 2042003 | 助手レッド | script:mc_roomout | 980000100 カーニバルフィールド1&lt;控え室&gt; 他5 |
| 2042004 | 助手ブルー | script:mc_roomout | 980000200 カーニバルフィールド2&lt;控え室&gt; 他2 |
| 2042007 | シュピゲルマン | script:mc2_move | 980030010  他5 |
| 2042008 | 助手レッド | script:mc2_roomout | 980031000  他2 |
| 2050014 | 隕石1 | script:earth009 | 221040000 クーラン草原1 |
| 2050015 | 隕石2 | script:earth010 | 221040200 クーラン草原3 |
| 2050016 | 隕石3 | script:earth011 | 221040300 クーラン草原4 |
| 2050017 | 隕石4 | script:earth012 | 221040100 クーラン草原2 |
| 2050018 | 隕石5 | script:earth013 | 221040201 バーナードの草原 |
| 2050019 | 隕石6 | script:earth014 | 221040400 クーラン草原5 |
| 2060009 | イルカ | script:aqua_taxi | 230000000 アクアリウム 他1 |
| 2060010 | イルカ | script:aqua_taxi3 | 923020000 座礁された幽霊船 |
| 2071012 | 見覚えがある少女(キツネ) | script:foxLaidy | 922220000 冷たく寒い森 |
| 2080000 | モス | script:minar_weapon | 240000000 リプレ |
| 2081005 | ケロベン | script:hontale_keroben | 240040700 生命の洞窟入口 |
| 2081010 | ムス(FieldsetEnterance) | script:s4blocking | 924000000 修練場への道 他2 |
| 2083000 | 遠征隊暗号石版 | script:hontale_enterToE | 240050000 洞窟の入口 |
| 2083001 | ホーンテイルの道標 | script:hontale_enter1 | 240050000 洞窟の入口 他3 |
| 2083002 | 木の根の水晶 | script:hontale_out | 240050100 迷路部屋 他13 |
| 2083003 | 迷路部屋の切り株 | script:hontale_Bdoor | 240050100 迷路部屋 |
| 2083004 | 遠征隊の標識 | script:hontale_accept | 240050400 ホーンテイルの洞窟入口 |
| 2083005 | 生命の泉 | script:s4holycharge | 240050400 ホーンテイルの洞窟入口 |
| 2084000 | ゴールドコンパス | script:goldCompass | 390000000 ゴールドリッチの宝倉庫&lt;1&gt; 他10 |
| 2085001 | 天空の扉 | script:SkyGate | 240080000 天空の渡し場 他1 |
| 2085002 | 天空の扉 | script:skyquest | 240030102 消えた森 他1 |
| 2091006 | 武陵道場掲示板 | script:dojang_move | 250000100 武陵神社 |
| 2091009 | 封印された社員入口 | script:enterShadow | 250020300 上級修練場 |
| 2092101 | ギオン | script:Pottery | 925110000 海賊の宝物倉庫 |
| 2094001 | キキョウニジン | script:davy_clear | 925100600 キキョウニジンの感謝 |
| 2094002 | キキョウコライ | script:davyJohn_play | 925100000 海賊船への道 他10 |
| 2096000 | 練習記録帳 | script:sca_dollBear | 250020000 初級修練場 |
| 2101015 | アブドラ８世 | script:aMatchScore | 980010010 王の部屋 |
| 2101016 | アレダ | script:aMatchRwd | 980010010 王の部屋 |
| 2101017 | セザール | script:aMatchPlay | 980010100 一番目の闘技場&lt;控え室&gt; 他5 |
| 2102000 | アセソン | script:get_ticket | 260000100 アリアント乗降場 |
| 2103000 | 王宮オアシス | script:ariant_oasis | 260000300 アリアント宮殿 |
| 2103001 | 秘密の壁 | script:secret_wall | 260000200 アリアント集落地 |
| 2103002 | 王妃の飾り棚 | script:ariant_ring | 260000303 アリアント宮殿&lt;王室&gt; |
| 2103003 | アリアント民家1 | script:ariant_house1 | 260000202 民家1 |
| 2103004 | アリアント民家2 | script:ariant_house2 | 260000203 民家2 |
| 2103005 | アリアント民家4 | script:ariant_house3 | 260000205 民家4 |
| 2103006 | アリアント民家6 | script:ariant_house4 | 260000207 民家6 |
| 2103008 | 奇妙な声 | script:thief_in2 | 260010401 岩坂 |
| 2103009 | 民家1収納場所(透明) | script:ariant_gold1 | 260000202 民家1 |
| 2103010 | 民家2収納場所(透明) | script:ariant_gold2 | 260000203 民家2 |
| 2103011 | 民家4収納場所(透明) | script:ariant_gold3 | 260000205 民家4 |
| 2103012 | 民家7収納場所(透明) | script:ariant_gold4 | 260000207 民家6 |
| 2111010 | アルカドノの本棚 | script:magatia_dark1 | 926120000 光が消えた研究室 |
| 2111011 | 失踪した錬金術師の家の壁(透明) | script:absence_wall | 261000001 失踪した錬金術師の家 |
| 2111012 | 失踪した錬金術師の家の本棚(透明) | script:absence_box | 261000001 失踪した錬金術師の家 |
| 2111013 | 失踪した錬金術師の家の額縁(透明) | script:absence_frame | 261000001 失踪した錬金術師の家 |
| 2111014 | 失踪した錬金術師の家の机(透明) | script:absence_desk | 261000001 失踪した錬金術師の家 |
| 2111015 | ラセルロンの机(透明) | script:alcadno_potion | 261020200 研究所B-1区域 |
| 2111017 | 一番目のパイプ取っ手(透明) | script:pipe1 | 261000001 失踪した錬金術師の家 |
| 2111018 | 二番目のパイプ取っ手(透明) | script:pipe2 | 261000001 失踪した錬金術師の家 |
| 2111019 | 三番目のパイプ取っ手(透明) | script:pipe3 | 261000001 失踪した錬金術師の家 |
| 2111020 | 一番目の魔法陣(透明) | script:alceCircle1 | 261040000 暗黒の魔法使いの研究室 |
| 2111021 | 二番目の魔法陣(透明) | script:alceCircle2 | 261040000 暗黒の魔法使いの研究室 |
| 2111022 | 三番目の魔法陣(透明) | script:alceCircle3 | 261040000 暗黒の魔法使いの研究室 |
| 2111023 | 魔法陣中央(透明) | script:alceCircle4 | 261040000 暗黒の魔法使いの研究室 |
| 2111024 | 秘密通路(透明) | script:secretNPC | 261010000 研究所1階廊下 他1 |
| 2111025 | 制御装置 | script:sca_auto | 261020401 関係者以外出入禁止区域 |
| 2111026 | 未完成魔法陣 | script:sca_DitRoi | 261010102 研究所202号 |
| 2112005 | ジュリエット(進行) | script:juliet | 926110200 特殊な実験室 |
| 2112006 | ロミオ(進行) | script:romio | 926100200 特殊な実験室 |
| 2112007 | 調査結果 | script:rnj_look | 926100000 怪しい研究室 他1 |
| 2112013 | 調査結果 | script:jnr_look | 926110000 怪しい研究室 他2 |
| 2112016 | 隠された文書 | script:q3367npc | 926130102 ユレテの実験室2 |
| 2120001 | 門番 | script:gateKeeper | 229010000 庭園 |
| 2120002 | 執事 | script:halloweenpq | 229000000 中央ホール  他9 |
| 2120009 | 執事 | script:hwreward | 229030300 執事の部屋 |
| 2121001 | 碑石が倒れた墓 | script:tablet01 | 229010100 墓地 |
| 2121002 | 名のない墓 | script:tablet02 | 229010100 墓地 |
| 2121003 | 訪ねる者のない墓 | script:tablet03 | 229010100 墓地 |
| 2121004 | 誰かの墓 | script:tablet04 | 229010100 墓地 |
| 2121005 | ピアノ | script:musicNote | 229000000 中央ホール  |
| 2121006 | 誰かの額縁1 | script:picture1 | 229000211 額縁部屋 |
| 2121007 | 誰かの額縁2 | script:picture4 | 229000211 額縁部屋 |
| 2121008 | 誰かの額縁3 | script:picture5 | 229000211 額縁部屋 |
| 2121009 | 誰かの額縁4 | script:picture3 | 229000211 額縁部屋 |
| 2121010 | 誰かの額縁5 | script:picture2 | 229000211 額縁部屋 |
| 2121011 | ソフィリアの額縁 | script:hwpicture | 229000211 額縁部屋 |
| 2133001 | エリン | script:party6_elin | 930000000 森の前 他7 |
| 2133002 | エリン森道しるべ | script:party6_giveUp | 930000300 霧の森 |
| 2133004 | スプライト | script:party6_spra | 930000500 森の広場 |
| 2141001 | 忘れられた神殿管理人 | script:PinkBeen_accept | 270050000 忘れられた黄昏 |
| 2141002 | 忘れられた神殿管理人 | script:PinkBeen_Out | 270050100 神々の黄昏 他1 |
| 9000000 | ポル | script:Event00 | 261000000 マガティア |
| 9000001 | ジャング | script:Event00 | 104000000 港口 |
| 9000002 | ピエトロ | script:Event02 | 109050000 商品交換所 |
| 9000003 | バイカン | script:Event03 | 109010000 宝を探せ！ |
| 9000004 | バイコン | script:Event03 | 109010100 東フィールド |
| 9000005 | バイケン | script:Event03 | 109010200 南フィールド |
| 9000006 | バイクン | script:Event03 | 109050001 イベント出口 |
| 9000007 | 天地 | script:Event04 | 103010000 工事現場 |
| 9000010 | ピエトラ | script:Event06 | 109050001 イベント出口 |
| 9000011 | マティン | script:Event00 | 200000000 オルビス |
| 9000012 | ハーリー | script:Event09 | 109080000 ココナッツシーズン 他3 |
| 9000013 | トニ | script:Event00 | 220000000 ルディブリアム |
| 9000031 | カサンドラ | script:out_jp7th | 805000100 地下監獄入口 |
| 9000039 | 要員W | script:watermelon_out | 922210300 スイカ畑出口 |
| 9000041 | 寄付 | script:Donation | 100000200 広場 他6 |
| 9000044 | 迷子の渡り鳥 | script:itemDoyo | 910020100 踏切板への棘の罠 他15 |
| 9000049 | 童話妖精クレコス | script:treasureHunter | 180000000 撮影現場 他1 |
| 9000060 | ジンジャーマン | script:PB_bossOut | 980041200 魔女の塔最上階 |
| 9000075 | チャン | script:MD_goblin | 809060000 金箔寺 他1 |
| 9000080 | ダオ | script:MD_monkey | 809060000 金箔寺 他1 |
| 9000082 | ポン | script:Ravana_out | 809061010 悪霊の神殿 他3 |
| 9001004 | 北極熊のフープ | script:Event10 | 109080010  他2 |
| 9001105 | 할아버지 월묘 | script:spaceGaGa_papa | 922231001 月うさぎの遊び場 他4 |
| 9001107 | 案内うさぎ | script:outRabbitJump | 922231000 月うさぎの遊び場 他1 |
| 9001108 | 案内うさぎ | script:moonFlower | 922230000 月の国 他21 |
| 9010017 | 開発者の人形 | script:test | 180000000 撮影現場 |
| 9010018 | クリシャ | script:mapleTCG | 220000000 ルディブリアム |
| 9040001 | ヌリス | script:guildquest1_clear | 990001100 帰り道 |
| 9040005 | 帰還碑 | script:guildquest1_out | 990000100 守護の谷 他8 |
| 9040006 | 正邪の彫刻 | script:guildquest1_baseball | 990000500 賢者の噴水 |
| 9040007 | シャレン3世の遺言書 | script:guildquest1_will | 990000600 地下水路 |
| 9040009 | ライオン像 | script:guildquest1_statue | 990000300 シャレニアン城門 |
| 9040010 | キメラ像 | script:guildquest1_bonus | 990000900 エレゴスの王子 |
| 9040011 | 掲示板 | script:guildquest1_board | 101030104 遺跡発掘ベースキャンプ 他1 |
| 9040012 | 騎士鎧 | script:guildquest1_knight | 990000400 騎士のホール |
| 9060000 | ケンタ | script:tamepig_out | 923010000 ケンタの飼育室 |
| 9100200 | パチンコ1 | script:Pachinko_machine0 | 809030000 パチンコ屋 |
| 9100201 | パチンコ2 | script:Pachinko_machine0 | 809030000 パチンコ屋 |
| 9100202 | パチンコ3 | script:Pachinko_machine1 | 809030000 パチンコ屋 |
| 9100203 | パチンコ4 | script:Pachinko_machine1 | 809030000 パチンコ屋 |
| 9100204 | パチンコ5 | script:Pachinko_machine2 | 809030000 パチンコ屋 |
| 9100205 | パチンコ6 | script:Pachinko_machine2 | 809030000 パチンコ屋 |
| 9102100 | ? | script:multipet_success | 100000202 ペットの散歩路 |
| 9102101 | ? | script:multipet_fail | 100000202 ペットの散歩路 |
| 9103000 | ピエトル | script:party_ludimaze_goal | 809050015 迷路 |
| 9103001 | ガイドモモ | script:party_ludimaze_enter | 220000000 ルディブリアム |
| 9103002 | ガイドララ | script:party_ludimaze_success | 809050016 商品交換所 |
| 9103003 | ガイドルル | script:party_ludimaze_fail | 809050017 イベント出口 |
| 9105017 | ボンちゃん | script:checkBlackDragon | 804000400 オルビス公園 他1 |
| 9105019 | 助手みどり | script:hair_EVDB | 100000000 ヘネシス |
| 9110008 | ペリー | script:goKerning | 800000000 キノコ神社 |
| 9110009 | 賽銭箱 | script:God2010 | 800000000 キノコ神社 |
| 9110105 | ナオスケ | script:ninja_maze | 800040211 楓城 百間廊下 |
| 9110115 | ナオスケ | script:JP_medal1 | 800040500 楓城 装備部屋 |
| 9110116 | イシラズ | script:JP_medal1_out | 800040500 楓城 装備部屋 |
| 9110200 | ドーク | script:Keconsiki | 889300201 ウェディングホール(セカンドウェディング) |
| 9110201 | イノン | script:Kecon | 680000000 ウェディングタウン 他1 |
| 9110202 | ギンコ | script:watingKecon | 889300200 ウェディングホール待機室(セカンドウェディング) 他1 |
| 9110203 | ノイン | script:beginCeremony3 | 889300201 ウェディングホール(セカンドウェディング) |
| 9110204 | チャヨ | script:KeconCoordinator | 680000000 ウェディングタウン |
| 9110205 | トーマス | script:Thomas2 | 889300600 出口1 他2 |
| 9120015 | コンペイ | script:con1 | 801000000 ショーワ町 |
| 9120020 | ミンシュタイン | script:zcap_out | 809020000 ジャクムの兜パワーアップ |
| 9120021 | 貝殻 | script:hina03 | 809010000 断崖絶壁 |
| 9120022 | ミンシュタイン | script:hina_out | 809010000 断崖絶壁 |
| 9120030 | マール | script:Go_boss2_out | 802000300 公園 2095年 他2 |
| 9120034 | ノラン | script:Make_Stone | 802000500 秋葉原司令室 2102年 |
| 9120036 | アーシア | script:Go_boss5 | 802000110 カムナ 他1 |
| 9120037 | ガルーダ司令 | script:Go_boss3 | 802000410 秋葉原 2102年 他1 |
| 9120038 | ディーダ | script:Go_boss2 | 802000310 公園 2095年 |
| 9120039 | 旗艦ファイア・オールドフォックス支援AI | script:Go_boss4 | 802000610 旗艦ファイア・オールドフォックス甲板 2102年 他1 |
| 9120040 | ポニチャル | script:Go_boss1 | 802000210 お台場 2100年 他1 |
| 9120045 | ? | script:JP_medal5 | 105040402 回復サウナ室＜高級＞ |
| 9120050 | 入室制御装置 | script:Go_boss7 | 802000820 六本木モール最上階2102年 他1 |
| 9120052 | ディーダ | script:Go_boss6 | 802000710 渋谷2102年 他1 |
| 9120053 | 入室制御装置 | script:TokyoPQ | 802000800 六本木モール2102年 他4 |
| 9120054 | ジャック | script:CrimsonStoryL | 803000700 最奥への通路

 他6 |
| 9120055 | ジャック | script:CrimsonStoryH | 803010700 最奥への通路
 他6 |
| 9120101 | 助手みどり | script:hair_shouwa2 | 801000001 美容院 |
| 9120102 | ヒゲクロ先生 | script:face_shouwa1 | 801000002 整形外科 |
| 9120103 | 助手サエコ | script:face_shouwa2 | 801000002 整形外科 |
| 9120105 | キャサリン | script:pachinkoDungeonEnter | 100000000 ヘネシス 他14 |
| 9120106 | パチンコ玉交換機 | script:Pachinko_dama_machine | 809030000 パチンコ屋 |
| 9120108 | キャサリン | script:pachinkoDungeonEnter | 809030100 パチンコミニダンジョン 他11 |
| 9120200 | コンペイ | script:con2 | 801040000 アジト前 |
| 9120201 | コンペイ | script:s_dungeon | 801040004 武器庫 |
| 9120202 | コンペイ | script:con3 | 801040100 悪夢の果て |
| 9120203 | コンペイ | script:con4 | 801040101 アジト前(天晴れ) |
| 9201002 | 教皇ジョン | script:HighPriest | 680000000 ウェディングタウン 他1 |
| 9201004 | 聖賢エームズ | script:wedding_Info | 680000000 ウェディングタウン |
| 9201005 | ニコル | script:cathedral | 680000000 ウェディングタウン 他1 |
| 9201006 | デビー | script:watingCathedral | 680000200 ウェディングホール待機室(大聖堂) 他1 |
| 9201007 | ナンシー | script:beginCeremony | 680000210 ウェディング(大聖堂) |
| 9201008 | ボニー | script:Chapel | 680000000 ウェディングタウン 他1 |
| 9201009 | ジャッキー | script:watingChapel | 889300100 ウェディングホール待機室(ハウスウェディング) 他1 |
| 9201010 | トラヴィス | script:beginCeremony2 | 889300101 ウェディング(ハウスウェディング) |
| 9201011 | ビバップ | script:Vibab | 889300101 ウェディング(ハウスウェディング) |
| 9201012 | ウェイン | script:ChapelCoordinator | 680000000 ウェディングタウン |
| 9201013 | ビクトリア | script:cathedralCoordinator | 680000000 ウェディングタウン |
| 9201014 | ピーラ | script:divorce | 680000000 ウェディングタウン |
| 9201015 | ジュリアス | script:hair_wedding1 | 680000002 美容院 |
| 9201016 | シェイマス | script:hair_wedding2 | 680000002 美容院 |
| 9201018 | アルバーツ | script:face_wedding1 | 680000003 整形外科 |
| 9201019 | シャキ | script:face_wedding2 | 680000003 整形外科 |
| 9201021 | ロビン | script:weddingParty | 680000300 ウェディングフォトスタジオ 他7 |
| 9201022 | トーマス | script:Thomas | 680000500 出口 他2 |
| 9201023 | ナナ | script:amoria_enter | 100000200 広場 他1 |
| 9201035 | ジャコブ | script:ringChange | 680000000 ウェディングタウン |
| 9201036 | アンジェリーク | script:presentExchange | 680000000 ウェディングタウン 他3 |
| 9201037 | ガリ&amp;シャティマ | script:loveOath | 680000000 ウェディングタウン |
| 9201082 | ペティト | script:naomi1 | 801000000 ショーワ町 |
| 9201094 | コリーン | script:TCG3 | 240000000 リプレ |
| 9201099 | フォウ
 | script:MoStore | 803000205 侵食の沼
 |
| 9201107 | マスターウォリアー | script:glpqstatue0 | 803001100 統一の試練
 他3 |
| 9201108 | マスターボウマン | script:glpqstatue1 | 803001100 統一の試練
 他3 |
| 9201109 | マスターメイジ | script:glpqstatue2 | 803001100 統一の試練
 他5 |
| 9201110 | マスターシーフ | script:glpqstatue3 | 803001100 統一の試練
 他3 |
| 9201111 | マスターパイレーツ | script:glpqstatue4 | 803001100 統一の試練
 他1 |
| 9201112 | ジャック | script:CrimsonpqEnter | 803000502 クリムゾン砦3

 他1 |
| 9201113 | ジャック | script:CpqStartL | 803000510 遠征隊(入場) ‐修練の道-

 |
| 9201114 | ジャック | script:CpqStartH | 803000520 遠征隊(入場) ‐挑戦者の道-

 |
| 9201115 | 戦女神の像 | script:CrimsonRaid | 803100000 支配者の秘密の間-孤高の戦場-
 |
| 9250072 | Gauss | script:start_punch | 501030106  |
| 9250073 | Checom | script:punchTicketEvent | 501030106  |
| 9250076 | Tomic | script:out_punch | 501030106  |
| 9250127 | OS4シャトル | script:osssStation_check | 502010000 OSSS秘密基地格納庫 |
| 9250128 | OS4シャトル | script:return_osssStation | 502010200 墜落した宇宙船の深海 他4 |
| 9250129 | OS4シャトル | script:gooutside_npcPT | 502010000 OSSS秘密基地格納庫 |
| 9250136 | ビンポス | script:visitor_gogocube | 100000000 ヘネシス 他3 |
| 9250137 | ビンポス | script:visitor_gooutcube | 502029000 墜落した宇宙船入口 |
| 9250138 | ブラックホール生成器 | script:bingtimetravel_check | 502040000 ドクタービンのキューブ |
| 9250143 | 現場のドクタービン | script:visitorPT_In | 502029000 墜落した宇宙船入口 |
| 9250144 | タイムマシーン | script:visitor_timemachine_future | 502010030 OSSS秘密基地ドクタービンの部屋 |
| 9250152 | OS3Aマシーン | script:visitor_guardmap_transfer | 502010010 地下道入口 |
| 9250153 | 公衆電話 | script:goVisitorStartMap | 502050001  |
| 9250155 | OSSS研究員 | script:Stage0_visitor_gooutcube | 502029000 墜落した宇宙船入口 |
| 9250156 | OSSS研究員 | script:Stage0_visitor_gogocube | 100000000 ヘネシス 他3 |
| 9310004 | 婦人警官ポリン | script:shanghai001 | 701010320 中原山岳地帯2 |
| 9310006 | 警察官ミカーファイ | script:shanghai003 | 701010322 抜け道 |
| 9310007 | 警察官ハーク | script:shanghai004 | 701010322 抜け道 他2 |
| 9310039 | 掛け軸 | script:q8535s | 702070400 蔵経閣7階 |
| 9310044 | 掛け軸 | script:outshaolinBoss | 702060000 修行の間 |
| 9330032 | 果物屋トレホレ | script:nightmarket02 | 741020101 夜市場裏道1 他1 |
| 9330046 | 釣り爺 | script:fishing | 741000200 釣り場 他8 |
| 9330073 | 海産物店チョム | script:q8704s | 741000000 夜市場 |
| 9330093 | ビッキィ＆ケッキー | script:enter4thEvent | 100000000 ヘネシス 他2 |
| 9330094 | パティ | script:PinkBeenEventPQ | 749050000 お菓子の部屋(入口)


 |
| 9330097 | ド | script:cakeEventHarp1 | 749050100 お菓子の部屋

 他9 |
| 9330098 | レ | script:cakeEventHarp2 | 749050100 お菓子の部屋

 他9 |
| 9330099 | ミ | script:cakeEventHarp3 | 749050100 お菓子の部屋

 他9 |
| 9330100 | ファ | script:cakeEventHarp4 | 749050100 お菓子の部屋

 他9 |
| 9330101 | ソ | script:cakeEventHarp5 | 749050100 お菓子の部屋

 他9 |
| 9330102 | ラ | script:cakeEventHarp6 | 749050100 お菓子の部屋

 他9 |
| 9330103 | シ | script:cakeEventHarp7 | 749050100 お菓子の部屋

 他9 |
| 9330104 | ピンクビーン | script:4thEventFinalStage | 749050100 お菓子の部屋

 他9 |
| 9330105 | パティ | script:PinkBeenEventReward | 749050200 お菓子の部屋(出口)


 |
| 9330106 | ショコラ | script:GuideMap | 749050100 お菓子の部屋

 他9 |
| 9900001 | NimaKIN | script:levelUP2 | 180000000 撮影現場 |
| 9901000 | ? | script:rank_user | 102000003 戦士の聖殿 |
| 9901001 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901002 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901003 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901004 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901005 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901006 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901007 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901008 | ? | script:rank_user | 102000004 戦士の殿堂 |
| 9901100 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901101 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901102 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901103 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901104 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901105 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901106 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901107 | ? | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901200 | ? | script:rank_user | 100000204 弓使いの殿堂 |
| 9901300 | ? | script:rank_user | 103000008 盗賊の殿堂 |
| 9901301 | ? | script:rank_user | 103000008 盗賊の殿堂 |
| 9901500 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901501 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901502 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901503 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901504 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901505 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901506 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901507 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901508 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901509 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901510 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901511 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901512 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901513 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901514 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901515 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901516 | ? | script:rank_user | 130000100 騎士の殿堂 |
| 9901517 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901518 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901519 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901520 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901521 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901522 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901523 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901524 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901525 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901526 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901527 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901528 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901529 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901530 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901531 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901532 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901533 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901534 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901535 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901536 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901537 | ? | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901538 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901539 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901540 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901541 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901542 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901543 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901544 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901545 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901546 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901547 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901548 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901549 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901550 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901551 | ? | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901600 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901601 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901602 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901603 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901604 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901605 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901606 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901607 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901608 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901609 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901610 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901611 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901612 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901613 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901614 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901615 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901616 | ? | script:rank_user | 140010110 英雄の殿堂 |
| 9901700 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901701 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901702 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901703 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901704 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901705 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901706 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901707 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901708 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901709 | ? | script:rank_user | 102000005 戦士の殿堂 |
| 9901710 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901711 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901712 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901713 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901714 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901715 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901716 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901717 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901718 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901719 | ? | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901720 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901721 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901722 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901723 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901724 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901725 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901726 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901727 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901728 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901729 | ? | script:rank_user | 100000205 弓使いの殿堂 |
| 9901730 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901731 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901732 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901733 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901734 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901735 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901736 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901737 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901738 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901739 | ? | script:rank_user | 103000009 盗賊の殿堂 |
| 9901740 | ? | script:rank_user | 120000105 訓練場 |
| 9901741 | ? | script:rank_user | 120000105 訓練場 |
| 9901742 | ? | script:rank_user | 120000105 訓練場 |
| 9901743 | ? | script:rank_user | 120000105 訓練場 |
| 9901744 | ? | script:rank_user | 120000105 訓練場 |
| 9901745 | ? | script:rank_user | 120000105 訓練場 |
| 9901746 | ? | script:rank_user | 120000105 訓練場 |
| 9901747 | ? | script:rank_user | 120000105 訓練場 |
| 9901748 | ? | script:rank_user | 120000105 訓練場 |
| 9901749 | ? | script:rank_user | 120000105 訓練場 |
| 9901800 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901801 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901802 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901803 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901804 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901805 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901806 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901807 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901808 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901809 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901810 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901811 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901812 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901813 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901814 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901815 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901816 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901817 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901818 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901819 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901820 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901821 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901822 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901823 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901824 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901825 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901826 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901827 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901828 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901829 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901830 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901831 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901832 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901833 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901834 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901835 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901836 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901837 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901838 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901839 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901840 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901841 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901842 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901843 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901844 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901845 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901846 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901847 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901848 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901849 | ? | script:rank_user | 130000101 騎士の殿堂 |
| 9901900 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901901 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901902 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901903 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901904 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901905 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901906 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901907 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901908 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901909 | ? | script:rank_user | 140010111 英雄の殿堂 |
| 9901910 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901911 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901912 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901913 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901914 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901915 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901916 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901917 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901918 | ? | script:rank_user | 100030301 鬱蒼とした森 |
| 9901919 | ? | script:rank_user | 100030301 鬱蒼とした森 |

## Full list (spawning NPCs)

| NPC | 名前 | 状態 | クエスト数 | wzヒント | 代表マップ |
|---|---|---|---|---|---|
| 2000 | ローザー | quest-data | 2 |  | 20000 デンデンの丘 |
| 2001 | セン | quest-data | 5 |  | 30001 キノコ村の民家 |
| 2002 | ピーター | quest-data | 1 |  | 40000 小さな森 |
| 2003 | ロビン | quest-data | 2 | script:begin5 | 50000 危険な森 |
| 2004 | トード | quest-data | 1 |  | 40000 小さな森 |
| 2005 | サム | quest-data | 3 |  | 50000 危険な森 |
| 2006 | ティエンク | quest-data | 4 |  | 104000000 港口 |
| 2007 | イベントガイド | quest-data | 2 | script:tutorialSkip | 10000 キノコの丘 |
| 2100 | セーラ | quest-data | 3 |  | 10000 キノコの丘 |
| 2101 | ヒナ | quest-data | 3 |  | 10000 キノコの丘 |
| 2102 | ニナ | quest-data | 5 |  | 30000 デンデンの花園 |
| 2103 | マリア | quest-data | 7 |  | 1000000 アムホスト |
| 10000 | ピオ | quest-data | 3 |  | 1000000 アムホスト |
| 10200 | ヘレナ | script |  | script:infoArcher | 1020000 選択の分かれ道 |
| 10201 | ハインズ | script |  | script:infoMagician | 1020000 選択の分かれ道 |
| 10202 | コブシを開いて立て | script |  | script:infoSwordman | 1020000 選択の分かれ道 |
| 10203 | ダークロード | script |  | script:infoRogue | 1020000 選択の分かれ道 |
| 10204 | カイリン | script |  | script:infoPirate | 1020000 選択の分かれ道 |
| 11000 | シード | shop |  |  | 1000001 アムホストの武器屋 |
| 11100 | ルーシー | shop |  |  | 1000003 アムホストの雑貨屋 |
| 12000 | ルーカス | quest-data | 9 |  | 1000000 アムホスト |
| 12100 | マイ | quest-data | 16 |  | 1010000 冒険者の修練場入口 |
| 12101 | レイン | quest-data | 14 | script:rein | 1000000 アムホスト |
| 20000 | ゾーン | quest-data | 11 |  | 104000000 港口 |
| 20001 | バリ | quest-data | 3 |  | 1010000 冒険者の修練場入口 |
| 20002 | ビックス | quest-data | 5 |  | 2000000 サウスペリ |
| 20100 | ユナ | quest-data | 15 |  | 1010000 冒険者の修練場入口 |
| 21000 | パン | shop |  |  | 2000001 サウスペリの何でも屋 |
| 22000 | シャンクス | quest-data | 2 | script:begin7 | 2000000 サウスペリ 他1 |
| 1001000 | シルバー | shop |  |  | 104000003 武器屋 |
| 1001001 | ナターシャ | shop |  |  | 104000001 防具屋 |
| 1001100 | ミナ | shop |  |  | 104000002 雑貨屋 |
| 1002000 | ピル | script |  | script:rithTeleport | 104000000 港口 |
| 1002001 | テオ | quest-data | 27 |  | 104000000 港口 |
| 1002002 | ペィソン | script | 2 | script:florina2 | 104000000 港口 |
| 1002003 | モンロンジジ | script |  | script:friend00 | 104000000 港口 |
| 1002004 | リス港口高級タクシー | script |  | script:mTaxi | 104000000 港口 |
| 1002005 | ゴールドマン | script |  |  | 104000000 港口 |
| 1002006 | チェフ | quest-data | 4 | script:bookPrize | 104000000 港口 |
| 1002007 | リス港口中型タクシー | script |  | script:taxi6 | 104000000 港口 |
| 1002100 | ジェーン | quest-data | 15 | script:jane | 104000000 港口 |
| 1002101 | クン | quest-data | 36 | script:contimoveSDIRit | 104000000 港口 |
| 1002103 | アール | quest-data | 5 | script:leaderAl | 104000000 港口 他1 |
| 1002104 | トゥルー | quest-data | 34 |  | 104000004 リス港口情報屋 |
| 1010100 | リナ | quest-data | 16 |  | 100000000 ヘネシス |
| 1011000 | カール | shop |  |  | 100000101 武器/防具屋 |
| 1011001 | サム | shop |  |  | 100000101 武器/防具屋 |
| 1011100 | ルナ | shop |  |  | 100000102 雑貨屋 |
| 1011101 | オシマ | shop |  |  | 100000100 市場 |
| 1012000 | メイプル運輸大型タクシー | script |  | script:taxi2 | 100000000 ヘネシス |
| 1012002 | ビシャス | script |  | script:refine_henesys | 100000100 市場 |
| 1012003 | 長老スタン | quest-data | 30 |  | 100000000 ヘネシス |
| 1012004 | キュト | shop |  |  | 100000100 市場 他1 |
| 1012005 | クロイ | script |  | script:petmaster | 100000200 広場 |
| 1012006 | バルトス | quest-data | 8 | script:pet_lifeitem | 100000202 ペットの散歩路 |
| 1012007 | プロド | quest-data | 1 | script:pet_letter | 100000202 ペットの散歩路 |
| 1012008 | カイジ | script |  | script:minigame00 | 100000203 ゲームパーク |
| 1012009 | ゴールドマン | script |  |  | 100000200 広場 |
| 1012100 | ヘレナ | script | 32 | script:bowman | 100000201 弓使い学院 |
| 1012101 | マヤ | quest-data | 28 |  | 100000001 民家 |
| 1012102 | ピア | quest-data | 7 |  | 100000200 広場 |
| 1012103 | ナタリー | script |  | script:hair_henesys1 | 100000104 美容院 |
| 1012104 | ブリトニー | script |  | script:hair_henesys2 | 100000104 美容院 |
| 1012105 | 私美女 | script |  | script:skin_henesys1 | 100000105 スキンケアーショップ |
| 1012106 | Mrs.ミンミン | quest-data | 11 |  | 100000000 ヘネシス |
| 1012107 | ユータ | quest-data | 1 |  | 900000000 ユータの豚農場 |
| 1012108 | カミラ | quest-data | 11 |  | 100000000 ヘネシス |
| 1012109 | ジェイ | quest-data | 23 |  | 100000000 ヘネシス |
| 1012110 | エン | quest-data | 8 |  | 100010000 東の丘 |
| 1012111 | ブルス | quest-data | 15 |  | 100000000 ヘネシス |
| 1012112 | ウサチャン | quest-data | 2 | script:moonrabbit | 100000200 広場 他2 |
| 1012113 | ウサクン | none |  | script:moonrabbit_bonus | 910010100 近道 他2 |
| 1012114 | タイクン | none |  | script:moonrabbit_tiger | 910010000 月見草の坂 |
| 1012115 | ヘネシス草むら | none |  | script:blackShadowHene1 | 100020000 東の草むら |
| 1012116 | ヘネシス草むら | none |  | script:blackShadowHene2 | 100020000 東の草むら |
| 1012117 | ロイヤル美容師 | script |  | script:hair_royal | 100000104 美容院 |
| 1012118 | ヘンケル | quest-data | 13 |  | 910060000 弓使い修行場 他1 |
| 1012119 | ヘンケル | quest-data | 9 | script:enter_archer | 100010000 東の丘 |
| 1013001 | ドラゴン | none |  | script:dragon_dream | 900010200 夢見る森 |
| 1013002 | ドラゴンの巣 | none |  | script:dragonEgg | 900020220 忘れてしまった森 |
| 1013100 | エナ | quest-data | 4 |  | 100030101 一軒家 |
| 1013101 | ドナ | quest-data | 8 |  | 100030102 前庭 |
| 1013102 | エゾオオカミ | quest-data | 1 |  | 100030102 前庭 |
| 1013103 | グリド | quest-data | 15 |  | 100030300 ウェタンギル中心地 |
| 1013104 | 卵の箱 | none |  | script:giveEggEvan | 100030102 前庭 |
| 1013105 | イルモ | quest-data | 2 |  | 100030310 大きい一本道 1 |
| 1013106 | 輝く碑石 | none |  | script:evan_lv200 | 100030301 鬱蒼とした森 |
| 1013200 | 赤ちゃんオオカミ | none |  | script:babyPig | 900020100 鬱蒼とした森 他1 |
| 1013203 | イベフ | quest-data | 11 |  | 922030000 蛙口の家 |
| 1013205 | フリエン | none |  | script:Afirentalk | 900030000 陣地裏手 |
| 1013207 | クン | none |  | script:contimoveRitSDI | 200090080 용이 잠든 섬으로 他2 |
| 1020000 | 豚と一緒に踊りを | quest-data | 7 |  | 102000000 ぺリオン |
| 1021000 | リバー | shop |  |  | 102000001 武器/防具屋 |
| 1021001 | ハリー | shop |  |  | 102000001 武器/防具屋 |
| 1021100 | グレゴリソン | shop |  |  | 102000002 雑貨屋 |
| 1022000 | コブシを開いて立て | script | 23 | script:fighter | 102000003 戦士の聖殿 |
| 1022001 | メイプル運輸大型タクシー | script |  | script:taxi1 | 102000000 ぺリオン |
| 1022002 | マンジ | quest-data | 29 | script:Manji | 102000000 ぺリオン |
| 1022003 | サンダー | script |  | script:refine_perion | 102000000 ぺリオン |
| 1022004 | スミス | script |  | script:refine_perion2 | 102000000 ぺリオン |
| 1022005 | 倉庫業者ゴールドマン | script |  |  | 102000000 ぺリオン |
| 1022006 | ウィンスターン | quest-data | 28 |  | 101030401 東の岩山2 |
| 1022007 | イヤン | quest-data | 20 |  | 102000000 ぺリオン |
| 1022008 | 燃えた剣 | quest-data | 2 |  | 101030402 東の岩山3 |
| 1022100 | ソフィア | quest-data | 15 |  | 102000002 雑貨屋 |
| 1022102 | 遺跡発掘団掲示板 | quest-data | 1 |  | 101030104 遺跡発掘ベースキャンプ |
| 1022103 | 分数彫像 | none |  | script:s4strike_statue | 910210000 シャレニアン城門 |
| 1022104 | ヘンケル | quest-data | 11 |  | 910220000 戦士修行場 |
| 1022105 | ヘンケル | none |  | script:enter_warrior | 101040000 分かれ道-東 |
| 1022106 | 探検家さん | quest-data | 2 |  | 106000000 深い谷1 他2 |
| 1022107 | ペリオン警告板 | none |  | script:periPatrol | 101030000 東の峰 他4 |
| 1031000 | 妖精フローラ | shop |  |  | 101000001 武器/防具屋 |
| 1031001 | 妖精セラビー | shop |  |  | 101000001 武器/防具屋 |
| 1031100 | 妖精レン | shop |  |  | 101000002 雑貨屋 |
| 1032000 | メイプル運輸大型タクシー | script |  | script:taxi4 | 101000000 エリニア |
| 1032001 | ハインズ | script | 52 | script:magician | 101000003 魔法図書館 |
| 1032002 | エトラン | quest-data | 3 | script:refine_ellinia | 101000000 エリニア |
| 1032003 | シェイン | quest-data | 2 | script:herb_in | 101000000 エリニア |
| 1032004 | ルイス | script |  | script:herb_out | 101000100 忍耐の森-1段階 他4 |
| 1032005 | エリニア高級タクシー | script |  | script:mTaxi | 101000000 エリニア |
| 1032006 | 倉庫業者ゴールドマン | script |  |  | 101000000 エリニア |
| 1032007 | ジョエル | script |  | script:sell_ticket | 101000300 エリニアステーション |
| 1032008 | チェリ | script |  | script:get_ticket | 101000300 エリニアステーション |
| 1032009 | プリン | script |  | script:goOutWaitingRoom | 101000301 控え室&lt;オルビス行き&gt; |
| 1032100 | 妖精アルウェン | quest-data | 13 | script:owen | 101000000 エリニア |
| 1032101 | 妖精ロウェン | quest-data | 21 |  | 101000000 エリニア |
| 1032102 | 妖精マル | quest-data | 2 | script:pet_life | 101000200 マルの森 |
| 1032103 | エルモス | shop |  |  | 101010102 そびえ立つ木3 |
| 1032104 | Dr.ベティ | quest-data | 15 |  | 101000000 エリニア |
| 1032105 | エステル | quest-data | 10 |  | 100050000 南フィールド |
| 1032106 | 妖精ウィング | quest-data | 18 |  | 101000000 エリニア |
| 1032107 | 怒りのリッフ | quest-data | 3 |  | 910100000 呪われた森 |
| 1032108 | 明るいリッフ | quest-data | 1 |  | 910100001 呪われた森 |
| 1032109 | 魔法図書館隈 | none |  | script:blackShadowEli1 | 910110000 魔法図書館 |
| 1032110 | 魔法図書館隈 | none |  | script:blackShadowEli2 | 910110000 魔法図書館 |
| 1032111 | 小さい切り株 | none |  | script:giveSap | 101010103 渓流&lt;バンジージャンプ台&gt; |
| 1032112 | おしゃべりな木 | quest-data | 1 |  | 100040000 南の森 |
| 1032113 | ヘンケル | quest-data | 11 |  | 910120000 魔法使い修行場 |
| 1032114 | ヘンケル | none |  | script:enter_magicion | 100050000 南フィールド |
| 1040000 | ルーク | quest-data | 7 |  | 106010100 ダンジョンの入口 |
| 1040001 | マイク | quest-data | 17 | script:mike | 106000300 ダンジョンの入口 |
| 1040002 | ファンシー | quest-data | 8 |  | 101020000 北の森 |
| 1043000 | 花の摘み | none |  | script:bush1 | 101000101 忍耐の森-2段階 |
| 1043001 | 薬草の藪 | none |  | script:bush2 | 101000104 忍耐の森-5段階 |
| 1051000 | マンシュタイン | shop |  |  | 103000001 武器/防具屋 |
| 1051001 | マパ | shop |  |  | 103000001 武器/防具屋 |
| 1051002 | うわさの爺 | shop |  |  | 103000002 明薬局 |
| 1052000 | アレックス | quest-data | 6 |  | 103000000 カニングシティー |
| 1052001 | ダークロード | script | 25 | script:rogue | 103000003 盗賊のアジト |
| 1052002 | 裏通りのゼイエム | quest-data | 9 | script:refine_kerning | 103000000 カニングシティー |
| 1052003 | クリス | script |  | script:refine_kerning2 | 103000006 修理屋 |
| 1052004 | 院長デンマ | script |  | script:face_henesys1 | 100000103 整形外科 |
| 1052005 | 医者ヘッポコ | script |  | script:face_henesys2 | 100000103 整形外科 |
| 1052006 | ウンイ | quest-data | 3 | script:subway_ticket | 103000100 切符売り場 |
| 1052007 | 改札口 | script |  | script:subway_in | 103000100 切符売り場 |
| 1052008 | 宝箱 | none |  | script:subway_get1 | 103000902 B1&lt;地下鉄基地&gt; |
| 1052009 | 宝箱 | none |  | script:subway_get2 | 103000905 B2&lt;地下鉄基地&gt; |
| 1052010 | 宝箱 | none |  | script:subway_get3 | 103000909 B3&lt;地下鉄基地&gt; |
| 1052011 | 出口 | none |  | script:subway_out | 103000900 B1&lt;1区域&gt; 他6 |
| 1052012 | モンロン | script |  | script:go_pc | 103000000 カニングシティー |
| 1052013 | パソコン | none |  | script:go_pcmap | 193000000 ネットカフェ |
| 1052014 | 自販機 | none |  |  | 193000000 ネットカフェ |
| 1052015 | ビリ | script |  | script:mouse | 193000000 ネットカフェ |
| 1052016 | メイプル運輸大型タクシー | script |  | script:taxi3 | 103000000 カニングシティー |
| 1052017 | ゴールドマン | script |  |  | 103000000 カニングシティー |
| 1052100 | ドンジオバネ | script |  | script:hair_kerning1 | 103000005 美容院 |
| 1052101 | アンドレア | script |  | script:hair_kerning2 | 103000005 美容院 |
| 1052102 | シュミ | quest-data | 7 |  | 103000000 カニングシティー |
| 1052103 | ネーラ | quest-data | 28 |  | 103000000 カニングシティー |
| 1052104 | トルカス | shop |  |  | 107000100 さまよい沼2 |
| 1052105 | 正体不明の女 | quest-data | 11 |  | 103000004 おいで病院 |
| 1052106 | イカルス | quest-data | 27 |  | 103000000 カニングシティー |
| 1052107 | 小さな街灯 | none |  | script:sca_Shade | 103000105 1号線-4区 他1 |
| 1052108 | 倒れたコミ箱 | quest-data | 2 |  | 107000301 沼地のあばら屋 |
| 1052109 | 地下鉄のゴミ箱 | none |  | script:givebubbleDoll1 | 103000101 1号線-1区 |
| 1052110 | 地下鉄のゴミ箱 | none |  | script:givebubbleDoll2 | 103000101 1号線-1区 |
| 1052111 | 地下鉄のゴミ箱 | none |  | script:givebubbleDoll3 | 103000101 1号線-1区 |
| 1052112 | 地下鉄のゴミ箱 | none |  | script:givebubbleDoll4 | 103000101 1号線-1区 |
| 1052113 | ヘンケル | quest-data | 13 |  | 910310000 盗賊修行場 |
| 1052114 | ヘンケル | none |  | script:enter_thief | 103010000 工事現場 |
| 1052115 | 林次長 | script | 1 | script:metroIm | 910320000 捨てられた地下鉄の歴史 他8 |
| 1052125 | ガードマン | none |  | script:Depart_topFloorIn | 103040400 7階 8階 A区域 |
| 1052126 | 선대 다크로드의 일기장 | none |  | script:dual_Diary | 910350100 선대 다크로드의 방 |
| 1055000 | 紫音 | none |  |  | 103050300 秘桜蔭地下室 |
| 1055002 | 舞鶴 | none |  |  | 103050300 秘桜蔭地下室 |
| 1056000 | 千火 | quest-data | 21 |  | 103050101 千火の部屋 |
| 1057000 | 猿渡 | quest-data | 3 |  | 103050200 秘桜蔭1階 |
| 1057001 | 紅 | quest-data | 7 | script:hong-a | 103000000 カニングシティー 他1 |
| 1057002 | 睦実 | none |  |  | 103050100 秘桜蔭 2階 |
| 1057003 | 果月 | quest-data | 3 |  | 103050300 秘桜蔭地下室 |
| 1057004 | 由良 | quest-data | 21 |  | 804000060 焼けた忍者村 他10 |
| 1061000 | クリシュラマ | quest-data | 8 | script:refine_sleepy | 105040300 スリーピーウッド |
| 1061001 | 24時間屋台 | shop |  |  | 105070001 アリの巣-広場 |
| 1061002 | 極楽 | script |  |  | 105040401 回復サウナ室＜一般＞ |
| 1061003 | 超極楽 | script | 3 |  | 105040402 回復サウナ室＜高級＞ |
| 1061004 | ロニ | quest-data | 13 |  | 101020001 北の森-木の通路 |
| 1061005 | サビトラマ | quest-data | 22 |  | 105040300 スリーピーウッド |
| 1061006 | 変な形の石像 | quest-data | 6 | script:flower_in | 105040300 スリーピーウッド |
| 1061007 | 崩れている石像 | none |  | script:flower_out | 105040310 忍耐の森「1段階」 他6 |
| 1061008 | ゴールドマン | script |  |  | 105040300 スリーピーウッド |
| 1061009 | 次元の扉 | none |  | script:crack | 100040106 邪気の森2 他4 |
| 1061010 | 光る水晶 | none |  | script:3jobExit | 108010101 次元の世界 他5 |
| 1061011 | 修行者 | quest-data | 47 |  | 105040300 スリーピーウッド |
| 1061012 | 亡霊 | quest-data | 11 | script:s4snipe | 105090200 別世界への扉 |
| 1061013 | グウィン | quest-data | 4 |  | 105090200 別世界への扉 |
| 1061014 | ムヨン | quest-data | 9 | script:balog_accept | 105100100 神殿の底
 |
| 1061016 | 怪しい男 | quest-data | 3 | script:balog_scroll | 105100000 地下への下り道
 |
| 1061017 | トリスタンの魂 | quest-data | 2 |  | 105100101 トリスタンの魂 |
| 1061018 | ムヨン | none |  | script:balog_InOut | 105100300 バルログの墓
 他1 |
| 1061019 | ニッシ | quest-data | 6 |  | 105040300 スリーピーウッド |
| 1061100 | ホテルガイド | none |  | script:hotel1 | 105040400 スリーピーホテルロビー |
| 1063000 | 桃色の花山 | none |  | script:viola_pink | 105040311 忍耐の森「2段階」 |
| 1063001 | 青色の花山 | none |  | script:viola_blue | 105040313 忍耐の森「4段階」 |
| 1063002 | 白色の花山 | none |  | script:viola_white | 105040316 忍耐の森「7段階」 |
| 1063003 | 緑キノコ手配掲示板 | quest-data | 2 |  | 105030000 深い森 |
| 1063004 | カズアイ手配掲示板 | quest-data | 2 |  | 105040200 森の狩り場2 |
| 1063005 | エビルアイ手配掲示板 | quest-data | 2 |  | 105070100 エビルアイの巣１ |
| 1063007 | ゾンビキノコ手配掲示板 | quest-data | 2 |  | 105050300 アリの巣4 |
| 1063008 | ツノキノコ手配掲示板 | quest-data | 2 |  | 105050200 アリの巣3 |
| 1063009 | Jr.ブギ手配掲示板 | quest-data | 1 |  | 105070400 エビルアイの巣４ |
| 1063011 | エビルアイの壁画 | none |  | script:Dollcave | 105070300 エビルアイの巣３ |
| 1063012 | 霊験な石1 | none |  | script:holySton | 105050200 アリの巣3 他2 |
| 1063013 | 霊験な石2 | none |  | script:holySton | 105090000 光を失った洞窟1 他1 |
| 1063014 | 謎のメモ | quest-data | 1 |  | 105050000 アリの巣1 |
| 1063016 | 不思議な石像 | none |  | script:DollWayKeeper1 | 910510100 人形使いの秘密通路 |
| 1063017 | 不思議な石像 | none |  | script:DollWayKeeper2 | 910510100 人形使いの秘密通路 |
| 1063018 | 残された人形 | quest-data | 12 |  | 910050300 버려진 동굴 |
| 1072000 | 戦士転職教官 | script |  | script:change_swordman | 102020300 西の岩山4 |
| 1072001 | 魔法使い転職教官 | script |  | script:change_magician | 101020000 北の森 |
| 1072002 | 弓使い転職教官 | script |  | script:change_archer | 106010000 ダンジョンへの道 |
| 1072003 | 盗賊転職教官 | script |  | script:change_rogue | 102040000 工事現場-北 |
| 1072004 | 戦士転職教官 | none |  | script:inside_swordman | 108000300 戦士の岩山1 他2 |
| 1072005 | 魔法使い転職教官 | none |  | script:inside_magician | 108000200 木のダンジョン1 他2 |
| 1072006 | 弓使い転職教官 | none |  | script:inside_archer | 108000100 アリの巣1 他2 |
| 1072007 | 盗賊転職教官 | none |  | script:inside_rogue | 108000400 盗賊の工事現場1 他2 |
| 1072008 | カイリン | script |  | script:inside_pirate | 108000500 海賊のテスト場 他3 |
| 1081000 | バァレン | shop |  |  | 110000000 ビーチ |
| 1081001 | パイソン | script |  | script:florina1 | 110000000 ビーチ |
| 1081100 | リエル | quest-data | 18 |  | 110000000 ビーチ |
| 1081101 | ロエル | quest-data | 3 |  | 110000000 ビーチ |
| 1081102 | ラエル | quest-data | 2 | script:hina02 | 110000000 ビーチ |
| 1090000 | カイリン | script | 31 | script:kairinT | 120000101 航海室 他1 |
| 1091000 | モガン | shop |  |  | 120000200 中央廊下 |
| 1091001 | ロドス | shop |  |  | 120000200 中央廊下 |
| 1091002 | ギャラリー | shop |  |  | 120000200 中央廊下 |
| 1091003 | セリル | script |  | script:refine_nautillus | 120000200 中央廊下 |
| 1091004 | ドンツルレス | script |  |  | 120000200 中央廊下 |
| 1092000 | タンユン | quest-data | 14 | script:nautil_cow | 120000103 食堂 |
| 1092001 | ボニ | quest-data | 4 |  | 120000300 下階廊下 |
| 1092002 | ベイン | quest-data | 5 |  | 120000300 下階廊下 |
| 1092003 | シャル | quest-data | 7 |  | 120000300 下階廊下 |
| 1092004 | ギャリコ | quest-data | 1 |  | 120000200 中央廊下 |
| 1092006 | ブラックバーク | quest-data | 3 |  | 120000201 会議室 |
| 1092007 | ムラト | quest-data | 18 | script:nautil_black | 120000100 上階廊下 |
| 1092008 | シュリンツ | quest-data | 2 | script:s4mind_in | 120000104 訓練場 |
| 1092009 | リド | quest-data | 5 |  | 120000100 上階廊下 |
| 1092010 | ジャック | quest-data | 1 |  | 120000100 上階廊下 |
| 1092011 | バルトール | quest-data | 6 |  | 120000200 中央廊下 |
| 1092012 | ロロネ | quest-data | 7 |  | 120000200 中央廊下 |
| 1092013 | ポシェ | quest-data | 2 |  | 120000301 動力室 |
| 1092014 | ノーチラス大型タクシー | script |  | script:taxi5 | 120000000 ノーチラス |
| 1092015 | 浄水器 | none |  |  | 120000202 寝室 |
| 1092016 | 輝く石 | none |  | script:nautil_stone | 120000301 動力室 |
| 1092018 | ゴミ箱 | none |  | script:nautil_letter | 120000100 上階廊下 |
| 1092019 | ジョナサン | quest-data | 1 | script:s4strike | 120000102 ジョナサンの部屋 |
| 1092090 | 母牛 | none |  | script:mom_cow | 912000100 ノーチラスの牛小屋  |
| 1092091 | 母牛 | none |  | script:mom_cow | 912000100 ノーチラスの牛小屋  |
| 1092094 | 子牛 | none |  | script:baby_cow | 912000100 ノーチラスの牛小屋  |
| 1092095 | 子牛 | none |  | script:baby_cow | 912000100 ノーチラスの牛小屋  |
| 1093000 | プパ | none |  |  | 120000000 ノーチラス |
| 1094000 | バト | quest-data | 9 |  | 120000000 ノーチラス |
| 1094001 | アベル | quest-data | 2 |  | 120000000 ノーチラス |
| 1094002 | 草原 | none |  | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094003 | 草原 | none |  | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094004 | 草原 | none |  | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094005 | 草原 | none |  | script:nautil_Abel1 | 120000000 ノーチラス |
| 1094006 | 草原 | none |  | script:nautil_Abel1 | 120000000 ノーチラス |
| 1095000 | シュリンツ | none |  | script:s4mind_out | 925010000 デリーを探しに1 |
| 1095001 | ヘンケル | quest-data | 13 |  | 912030000 海賊修行場 |
| 1095002 | ヘンケル | none |  | script:enter_pirate | 120010000 船着場への道 |
| 1100000 | キリウム | script |  |  | 130000200 エレヴの分かれ道 |
| 1100001 | キリユ | shop |  |  | 130000200 エレヴの分かれ道 |
| 1100002 | キリウィング | shop |  |  | 130000200 エレヴの分かれ道 |
| 1100003 | キリル | script |  | script:contimoveEreEli | 130000210 ステーション |
| 1100004 | キル | script |  | script:contimoveEreOrb | 130000210 ステーション |
| 1100005 | キリル(船) | script |  | script:talkVic | 200090030 エレヴ行き 他19 |
| 1100006 | キル(船) | script |  | script:talkOrv | 200090020 エレヴ行き 他19 |
| 1100007 | キリル(エリニア) | script |  | script:contimoveEliEre | 101000400 ステーション&lt;エレヴ行き&gt; |
| 1100008 | キル(オルビス) | script |  | script:contimoveOrbEre | 200000161 ステーション&lt;エレヴ行き&gt; |
| 1101000 | シグナス | quest-data | 24 |  | 130000000 エレヴ |
| 1101001 | 神獣 | quest-data | 1 | script:createCygnus | 130000000 エレヴ |
| 1101002 | ナインハート | quest-data | 57 |  | 130000000 エレヴ |
| 1101003 | ミハエル | script | 12 |  | 130000000 エレヴ |
| 1101004 | オズ | script | 12 |  | 130000000 エレヴ |
| 1101005 | イリーナ | script | 12 |  | 130000000 エレヴ |
| 1101006 | イカルト | script | 12 |  | 130000000 エレヴ |
| 1101007 | ホークアイ | script | 12 |  | 130000000 エレヴ |
| 1102000 | キク | quest-data | 24 |  | 130010000 修行の森1 |
| 1102001 | キリコ | none |  | script:outSecondDH | 108000600 第2訓練場 他2 |
| 1102002 | キリド | quest-data | 32 | script:giveupRiding | 130010220 孵化場 |
| 1102003 | キダン | none |  | script:cygnus_lv120 | 130000100 騎士の殿堂 他1 |
| 1102004 | セディコン | quest-data | 1 |  | 130030001 開始の森2 |
| 1102005 | セヴァイ | quest-data | 3 |  | 130030002 開始の森3 |
| 1102006 | セミュワ | quest-data | 2 |  | 130030003 開始の森4 |
| 1102007 | セイ | quest-data | 2 |  | 130030004 開始の森5 |
| 1102008 | キシャ | quest-data | 1 |  | 130030005 開始の森出口 |
| 1103000 | デュナミス | none |  |  | 924010200 暗黒の魔女の洞窟 |
| 1103001 | ローカ | quest-data | 8 |  | 100000000 ヘネシス |
| 1103002 | マティアス | quest-data | 8 |  | 103000000 カニングシティー |
| 1103003 | ヘルシャ | quest-data | 7 |  | 101000000 エリニア |
| 1103004 | 10匹のブギ | quest-data | 13 |  | 102000000 ぺリオン |
| 1103005 | ナインハート | none |  | script:erebWarp | 913040006 シグナス騎士団 |
| 1104000 | フランシス | none |  | script:DollMaster | 910510001 人形使いの本拠地 |
| 1104200 | 倒れた騎士 | none |  | script:enterBlackEreb | 924010100 暗黒の魔女の洞窟 |
| 1104201 | シグナス | none |  |  | 913030000 エレヴ |
| 1104202 | ナインハート | none |  |  | 913030000 エレヴ |
| 1104203 | ミハエル | none |  |  | 913030000 エレヴ |
| 1104204 | オズ | none |  |  | 913030000 エレヴ |
| 1104205 | イリーナ | none |  |  | 913030000 エレヴ |
| 1104206 | イカルト | none |  |  | 913030000 エレヴ |
| 1104207 | ホークアイ | none |  |  | 913030000 エレヴ |
| 1104208 | 神獣 | quest-data | 2 |  | 913030000 エレヴ |
| 1200000 | プスルト | script |  |  | 140000000 リエン村 |
| 1200001 | プノウン | shop |  |  | 140000000 リエン村 |
| 1200002 | プリリ | shop |  |  | 140000000 リエン村 |
| 1200003 | プロ | script |  | script:contimoveRieRit | 140020300 ペンギン港 |
| 1200004 | プロ | script |  | script:contimoveRitRie | 104000000 港口 |
| 1200005 | プロ | script |  | script:PurotalkRie | 200090060 リエン行 他9 |
| 1200006 | プロ | script |  | script:PurotalkVic | 200090070 リス港口行 他9 |
| 1201000 | リリン | script | 43 | script:AranMaptext | 140000000 リエン村 |
| 1201001 | 巨大な鉾 | quest-data | 2 |  | 140000000 リエン村 |
| 1201002 | マッハ | quest-data | 15 |  | 140000000 リエン村 |
| 1202000 | リリン | script |  | script:awake | 140090000 氷洞窟 |
| 1202001 | プカ | quest-data | 2 |  | 140090100 冷たい森1 |
| 1202002 | プエン | quest-data | 1 |  | 140090200 冷たい森2 |
| 1202003 | プイル | quest-data | 1 |  | 140090200 冷たい森2 |
| 1202004 | プルン | quest-data | 2 |  | 140090300 冷たい森3 |
| 1202005 | プッキ | quest-data | 2 |  | 140090400 冷たい森4 |
| 1202006 | プオ | quest-data | 7 |  | 140010100 リエン修行場入口 |
| 1202007 | プニ | quest-data | 9 |  | 140020110 プニの原野 |
| 1202008 | 親分ヴォルフ | quest-data | 7 |  | 140010210 オオカミの原野 |
| 1202009 | 番人ヴォルフ | none |  | script:enterWolf | 140010200 氷原野 |
| 1202010 | プディン | none |  | script:aran_lv200 | 140010110 英雄の殿堂 他1 |
| 1203000 | 仙人翁 | quest-data | 2 |  | 108000700 大将翁の鍛冶屋 |
| 1203001 | ティティティ | quest-data | 2 |  | 108010701 刃のような絶壁 |
| 1204001 | フランシス | none |  | script:dollMaster00 | 910510200 人形使いの洞窟 |
| 1204005 | トゥルー | none |  | script:downTrue | 910400000 危険な情報屋 |
| 1204030 | 図書館書棚 | none |  | script:Warehouse | 930010000 危険な魔法図書館 |
| 1204032 | ヘレナ | none |  | script:downHelena | 910050000 危険な弓使い学院 |
| 1204033 | ゾーンの箱 | quest-data | 1 |  | 104000000 港口 |
| 1205000 | フリエン | quest-data | 6 |  | 914100021 眠った洞窟 |
| 1209000 | ヘレナ | quest-data | 1 | script:talkHelena | 914000100 避難準備中 |
| 1209001 | 避難民1 | none |  |  | 914000100 避難準備中 |
| 1209002 | 避難民2 | none |  |  | 914000100 避難準備中 |
| 1209003 | 避難民3・4・5 | none |  |  | 914000100 避難準備中 |
| 1209004 | 避難民6 | none |  |  | 914000100 避難準備中 |
| 1209005 | 避難民7 | none |  |  | 914000100 避難準備中 |
| 1209006 | はぐれた子供 | quest-data | 2 |  | 914000300 森の最奥 |
| 1209007 | ヘレナ | quest-data | 1 |  | 914000500 避難準備完了 |
| 1300000 | 王様キノコ | quest-data | 9 |  | 106020000 キノコの森町角 |
| 1300001 | ペペキング | none |  |  | 106021600 結婚式場 |
| 1300002 | ビオルタ | quest-data | 9 |  | 106021600 結婚式場 他1 |
| 1300003 | 内務大臣 | quest-data | 11 |  | 106020000 キノコの森町角 |
| 1300004 | 魔法大臣 | quest-data | 9 |  | 106020000 キノコの森町角 |
| 1300005 | 警護大将 | quest-data | 22 |  | 106020000 キノコの森町角 |
| 1300006 | ズペ王子 | none |  |  | 106021600 結婚式場 |
| 1300007 | スカス | quest-data | 9 |  | 106020000 キノコの森町角 |
| 1300008 | ジェイムズ | quest-data | 5 |  | 106021201 中央の城塔 |
| 1300012 | 東石塔門 | none |  | script:TD_MC_bossEnter | 106021400 東の塔 |
| 1300013 | マックフィンイブグ | none |  | script:TD_MC_violetaEnter | 106021402 最後の城塔 |
| 1300014 | セルフ | none |  | script:forself | 106020300 奥深きキノコの森 他2 |
| 1301000 | トル | none |  |  | 106020000 キノコの森町角 |
| 2010000 | チャーリー軍曹 | quest-data | 4 | script:carlie | 200000000 オルビス |
| 2010001 | ミノ | script |  | script:hair_orbis1 | 200000202 オルビスヘアーショップ |
| 2010002 | プランツ | script |  | script:face_orbis1 | 200000201 整形外科 |
| 2010003 | ネーブ | none |  | script:make_orbis | 200000200 オルビス公園 |
| 2010004 | ウィルソンくん | none |  |  | 200000200 オルビス公園 |
| 2010005 | ガイドシュリ | script | 3 | script:florina2 | 200000000 オルビス |
| 2010006 | ティニ | script |  |  | 200000000 オルビス |
| 2010007 | ヘラクル | script |  | script:guild_proc | 200000301 ギルド本部&lt;英雄の殿堂&gt; |
| 2010008 | レア | script |  | script:guild_mark | 200000301 ギルド本部&lt;英雄の殿堂&gt; |
| 2010009 | レナリウ | script |  | script:guild_union | 200000301 ギルド本部&lt;英雄の殿堂&gt; |
| 2012000 | イフ | none |  | script:sell_ticket | 200000100 オルビスチケット売場 |
| 2012001 | リニ | script |  | script:get_ticket | 200000111 ステーション&lt;エリニア行き&gt; |
| 2012002 | エリン | script |  | script:goOutWaitingRoom | 200000112 控え室&lt;エリニア行き&gt; |
| 2012003 | 妖精ネリ | shop |  |  | 200000001 武器屋 |
| 2012004 | 妖精ヌリ | shop |  |  | 200000001 武器屋 |
| 2012005 | 妖精エデル | shop |  |  | 200000002 雑貨屋 |
| 2012006 | イス | none |  | script:getAboard | 200000100 オルビスチケット売場 |
| 2012007 | リンス | script |  | script:hair_orbis2 | 200000202 オルビスヘアーショップ |
| 2012008 | ロミ | script |  | script:skin_orbis1 | 200000203 オルビススキンケアショップ |
| 2012009 | 助手リーザ | script |  | script:face_orbis2 | 200000201 整形外科 |
| 2012010 | メイドエルマ | quest-data | 20 |  | 200000200 オルビス公園 |
| 2012011 | 妖精クリエル | quest-data | 13 |  | 200000002 雑貨屋 |
| 2012012 | リーサ | quest-data | 37 | script:oldBook2 | 200000000 オルビス |
| 2012013 | スナ | script |  | script:get_ticket | 200000121 ステーション&lt;ルディブリアム行き&gt; |
| 2012014 | オルビス魔法石 | none |  | script:ossyria3_1 | 200080200 オルビス塔&lt;20層&gt; |
| 2012015 | エルナス魔法石 | none |  | script:ossyria3_2 | 200082100 オルビス塔&lt;1層&gt; |
| 2012017 | ヒューズ | quest-data | 20 |  | 200082301 オルビス塔&lt;ヒューズの研究室&gt; |
| 2012018 | エリック | quest-data | 21 |  | 200000200 オルビス公園 |
| 2012019 | ボンちゃん | quest-data | 18 |  | 200000000 オルビス |
| 2012020 | アルポン | quest-data | 9 |  | 200010000 雲の公園1 |
| 2012021 | ラミニ | script |  | script:get_ticket | 200000131 ステーション&lt;リプレ行き&gt; |
| 2012022 | ぺラス | script |  | script:goOutWaitingRoom | 200000132 控え室&lt;リプレ行き&gt; |
| 2012023 | 紅葉玉 | quest-data | 6 | script:s4tornado | 200000300 出会いの丘 |
| 2012024 | イグネト | script |  | script:goOutWaitingRoom | 200000152 控え室&lt;アリアント行き&gt; |
| 2012025 | ゼラス | script |  | script:get_ticket | 200000151 ステーション&lt;アリアント行き&gt; |
| 2012026 | エリジャー | script | 5 |  | 920020000 エリジャーの庭園 |
| 2012027 | ヒューズ | none |  | script:elizaHarp1 | 920020000 エリジャーの庭園 |
| 2012028 | ハープ弦&lt;レ&gt; | none |  | script:elizaHarp2 | 920020000 エリジャーの庭園 |
| 2012029 | ハープ弦&lt;ミ&gt; | none |  | script:elizaHarp3 | 920020000 エリジャーの庭園 |
| 2012030 | ハープ弦&lt;ファ&gt; | none |  | script:elizaHarp4 | 920020000 エリジャーの庭園 |
| 2012031 | ハープ弦&lt;ソ&gt; | none |  | script:elizaHarp5 | 920020000 エリジャーの庭園 |
| 2012032 | ハープ弦&lt;ラ&gt; | none |  | script:elizaHarp6 | 920020000 エリジャーの庭園 |
| 2012033 | ハープ弦&lt;シ&gt; | none |  | script:elizaHarp7 | 920020000 エリジャーの庭園 |
| 2012034 | 秘密のレンガ | quest-data | 3 |  | 200080601 オルビス塔&lt;秘密の部屋&gt; |
| 2013000 | 妖精ウィンキー | quest-data | 1 | script:party3_enter | 200080101 見知らぬ塔 他1 |
| 2013001 | 侍従イク | none |  | script:party3_play | 920010100 中央塔 他10 |
| 2013002 | 女神ミネルバ | none |  | script:party3_minerva | 920011300 女神の祝福 |
| 2020000 | ボゲン | script | 4 | script:refine_elnath | 211000100 市場 |
| 2020001 | スコット | shop |  |  | 211000101 武器/防具屋 |
| 2020002 | ゴードン | script | 5 | script:make_elnath | 211000100 市場 |
| 2020003 | フォックス曹長 | quest-data | 7 |  | 211000000 エルナス |
| 2020004 | オスマン | script |  |  | 211000100 市場 |
| 2020005 | アルケスタ | quest-data | 49 | script:oldBook1 | 211000100 市場 |
| 2020006 | ゼイド | quest-data | 13 |  | 211000000 エルナス |
| 2020007 | スカドル | quest-data | 26 |  | 211000000 エルナス |
| 2020008 | タイラス | script | 3 | script:warrior3 | 211000001 長老の官邸 |
| 2020009 | ロベイラ | script | 10 | script:wizard3 | 211000001 長老の官邸 |
| 2020010 | レネ | script | 5 | script:bowman3 | 211000001 長老の官邸 |
| 2020011 | アレク | script | 5 | script:thief3 | 211000001 長老の官邸 |
| 2020012 | 雪の精霊の像 | quest-data | 2 |  | 211040102 雪の精霊の憩いの広場 |
| 2020013 | ペドロ | script | 1 | script:pirate3 | 211000001 長老の官邸 |
| 2022000 | ルミ | shop |  |  | 211000101 武器/防具屋 |
| 2022001 | ハナ | shop |  |  | 211000102 雑貨屋 |
| 2022002 | バルン | shop |  |  | 200080800 オルビス塔&lt;14層&gt; |
| 2022003 | シャモス | quest-data | 21 |  | 211000001 長老の官邸 |
| 2022004 | タイラス | none |  | script:s4common1_out | 921100301 タイラス護衛完遂 |
| 2023000 | 危険地域弾丸タクシー | none |  | script:ossyria_taxi | 211000000 エルナス 他2 |
| 2030000 | ジェフ | script | 1 | script:goDungeon | 211040200 氷の谷2 |
| 2030001 | ブラボー伍長 | quest-data | 5 |  | 211050000 凍てつく野原 |
| 2030002 | イージー兵長 | quest-data | 8 |  | 200080000 雲の公園6 |
| 2030003 | 雪に覆われた岩 | quest-data | 2 |  | 211040100 氷の谷1 |
| 2030004 | 小さな墓 | quest-data | 4 |  | 211040500 狼の領域1 |
| 2030005 | 石像 | quest-data | 5 |  | 200081400 オルビス塔&lt;8層&gt; |
| 2030006 | 聖なる岩 | none |  | script:holyStone | 211040401 雪原の聖地 |
| 2030007 | 石像の破片 | quest-data | 2 |  | 211042101 裏洞窟 |
| 2030008 | アドビス | quest-data | 1 | script:Zakum00 | 211042300 ジャクムへの門 |
| 2030009 | グリーバ | shop |  |  | 211040200 氷の谷2 |
| 2030010 | アーモン | script |  | script:Zakum06 | 280020000 火山の息&lt;1段階&gt; 他2 |
| 2030011 | アーリ | none |  | script:Zakum04 | 280090000 悲恋の部屋 |
| 2030012 | ハークル | quest-data | 14 |  | 200081201 オルビス塔&lt;秘密の部屋&gt; |
| 2030013 | アドビス | script |  | script:zakum_accept | 211042400 ジャクムの祭壇入口 |
| 2030014 | 古代氷石 | none |  | script:s4freeze_item | 921100100 氷の谷 |
| 2030015 | 隠密な岩 | quest-data | 1 | script:enterHolyStoneDual | 211040400 険しき絶壁2 |
| 2032000 | ？？？ | quest-data | 11 |  | 200050001 老婆の家 |
| 2032001 | スピルナ | quest-data | 26 | script:oldBook5 | 200050001 老婆の家 |
| 2032002 | アウラ | none |  | script:Zakum01 | 280010000 知られざる閉鉱 |
| 2032003 | リラー | none |  | script:Zakum02 | 280020001 火山の息&lt;2段階&gt; |
| 2032004 | 溶岩 | quest-data | 2 |  | 211042101 裏洞窟 |
| 2040000 | モル | script |  | script:sell_ticket | 220000100 チケット売場 |
| 2040001 | ライトくん | quest-data | 3 |  | 221024400 エオス塔100階 |
| 2040002 | レフトくん | quest-data | 2 | script:ludi023 | 221024400 エオス塔100階 |
| 2040003 | ハカセくん | quest-data | 3 | script:ludi020 | 220020000 オモチャ工場-1工程&lt;1区域&gt; 他1 |
| 2040004 | 作業員1 | quest-data | 2 |  | 221024200 エオス塔98階 |
| 2040005 | 作業員2 | quest-data | 2 |  | 221023700 エオス塔93階 |
| 2040006 | 作業員3 | quest-data | 2 |  | 221023400 エオス塔76階-90階 |
| 2040007 | 作業員4 | quest-data | 2 |  | 221023200 エオス塔74階 |
| 2040008 | 作業員5 | quest-data | 6 |  | 221022700 エオス塔60階 |
| 2040009 | 作業員6 | quest-data | 4 |  | 221022200 エオス塔46階-55階 |
| 2040010 | 作業員7 | quest-data | 2 |  | 221021700 エオス塔41階 |
| 2040011 | 作業員8 | quest-data | 4 |  | 221021100 エオス塔21階 |
| 2040012 | 作業員9 | quest-data | 2 |  | 221020600 エオス塔7階 |
| 2040013 | 作業員10 | quest-data | 6 |  | 221020200 エオス塔3階 |
| 2040014 | チコ | script |  | script:minigame00 | 220000300 ルディブリアム住宅街 |
| 2040015 | 工場長カホ | quest-data | 4 |  | 220020300 オモチャ工場-メイン工程1 |
| 2040016 | パイ | script |  | script:make_ludi1 | 220000300 ルディブリアム住宅街 |
| 2040017 | プルレンジャーグリーン | quest-data | 6 |  | 221030400 ロスウェル草原4 |
| 2040018 | プルレンジャーブラック | quest-data | 6 |  | 221040000 クーラン草原1 |
| 2040019 | エバー | script |  | script:face_ludi2 | 220000003 整形外科 |
| 2040020 | ジロクン | none |  | script:make_ludi2 | 220000303 ジロクンとペイの家 |
| 2040021 | ペイ | none |  | script:make_ludi3 | 220000303 ジロクンとペイの家 |
| 2040022 | ライドル | none |  | script:make_ludi4 | 220020600 オモチャ工場-機械室 |
| 2040023 | 道迷い兵士 | quest-data | 2 |  | 221023700 エオス塔93階 |
| 2040024 | 一番目のエオス石 | none |  | script:ludi014 | 221024400 エオス塔100階 |
| 2040025 | 二番目のエオス石 | none |  | script:ludi015 | 221022900 エオス塔71階 |
| 2040026 | 三番目のエオス石 | none |  | script:ludi016 | 221021700 エオス塔41階 |
| 2040027 | 四番目のエオス石 | none |  | script:ludi017 | 221020000 エオス塔1階 |
| 2040028 | マークくん | none |  | script:ludi024 | 922000010 人形の家 |
| 2040029 | 掛け時計 | quest-data | 11 |  | 220050000 なくした時間1 |
| 2040030 | ウィスブ | none |  | script:ludi026 | 220000400 エオス塔入口 |
| 2040031 | 文書束 | none |  | script:ludi027 | 220000304 クロイの家 |
| 2040032 | ウィーバー | none |  | script:ludi028 | 220000006 ルディブリアムの散歩路 |
| 2040033 | ネル | none |  | script:ludi029 | 220000006 ルディブリアムの散歩路 |
| 2040034 | 表示板 | script | 1 | script:party2_enter | 221024500 エオス塔101階 |
| 2040035 | アルト | script |  | script:party2_play | 922011100 放置された塔&lt;冒険の結実&gt; |
| 2040036 | レッドバルーン | script |  | script:party2_play | 922010100 放置された塔&lt;1段階&gt; |
| 2040037 | オレンジバルーン | script |  | script:party2_play | 922010200 放置された塔&lt;2段階&gt; |
| 2040038 | イエローバルーン | script |  | script:party2_play | 922010300 放置された塔&lt;3段階&gt; |
| 2040039 | イエローグリーンバルーン | script |  | script:party2_play | 922010400 放置された塔&lt;4段階&gt; |
| 2040040 | グリーンバルーン | script |  | script:party2_play | 922010500 放置された塔&lt;5段階&gt; |
| 2040041 | アクアバルーン | script |  | script:party2_play | 922010600 放置された塔&lt;6段階&gt; |
| 2040042 | スカイブルーバルーン | script |  | script:party2_play | 922010700 放置された塔&lt;7段階&gt; |
| 2040043 | ブルーバルーン | script |  | script:party2_play | 922010800 放置された塔&lt;8段階&gt; |
| 2040044 | パープルバルーン | script |  | script:party2_play | 922010900 時空の亀裂 |
| 2040045 | ピンクバルーン | script |  | script:party2_play | 922011000 放置された塔&lt;ボーナス&gt; |
| 2040046 | ハリ | script |  | script:friend01 | 220000000 ルディブリアム |
| 2040047 | アンダソンくん | script |  | script:party2_out | 922010000 放置された塔&lt;冒険の終わり&gt; 他9 |
| 2040048 | ナナ | script | 1 | script:florina2 | 220000000 ルディブリアム |
| 2040049 | キャンディマシン | shop |  |  | 221021600 エオス塔26階-40階 |
| 2040050 | 風来坊錬金術師 | quest-data | 26 | script:make_ston | 105040300 スリーピーウッド 他3 |
| 2040051 | ツブー | shop |  |  | 220050300 時間の通路 |
| 2040052 | 司書ウィズ | quest-data | 29 | script:library | 222020000 ヘリオス塔の図書館 |
| 2041000 | ティティアン | script |  | script:get_ticket | 220000110 ステーション&lt;オルビス行き&gt; |
| 2041001 | エリン | script |  | script:goOutWaitingRoom | 200000122 控え室&lt;ルディブリアム行き&gt; 他1 |
| 2041002 | ヒド | shop |  |  | 220000001 武器/防具屋 |
| 2041003 | ミル | shop |  |  | 220000001 武器/防具屋 |
| 2041004 | マルセル | quest-data | 21 |  | 220000000 ルディブリアム |
| 2041005 | ネミ | quest-data | 7 |  | 220000302 カホの家 |
| 2041006 | ミスキ | shop |  |  | 220000002 雑貨屋 |
| 2041007 | ミユ | script |  | script:hair_ludi1 | 220000004 美容院 |
| 2041008 | セピ | script |  |  | 220000000 ルディブリアム |
| 2041009 | ミニ | script |  | script:hair_ludi2 | 220000004 美容院 |
| 2041010 | エル | script |  | script:face_ludi1 | 220000003 整形外科 |
| 2041011 | プルレンジャーイエロー | quest-data | 4 |  | 221030000 統制区域 |
| 2041012 | プルレンジャーピンク | quest-data | 6 |  | 221030200 ロスウェル草原2 |
| 2041013 | ジナ | script |  | script:skin_ludi1 | 220000005 スキンケアーショップ |
| 2041014 | ペトリシャ | none |  |  | 220000000 ルディブリアム |
| 2041015 | コリン | quest-data | 2 |  | 220000301 コリンの家 |
| 2041016 | ミルゲル | shop |  |  | 221022000 エオス塔44階 |
| 2041018 | 組立工レキ | quest-data | 2 |  | 220030100 オモチャ工場-2工程&lt;2区域&gt; |
| 2041019 | 修理工ロキ | quest-data | 2 |  | 220030200 オモチャ工場-メイン工程2 |
| 2041020 | 機械工ルキ | quest-data | 2 |  | 220030400 オモチャ工場-2工程&lt;4 区域&gt; |
| 2041021 | Mr.ピエロ | quest-data | 19 |  | 220050300 時間の通路 |
| 2041022 | 補佐官ティグン | quest-data | 10 |  | 220000400 エオス塔入口 |
| 2041023 | プロ | quest-data | 19 | script:s4efreet | 220050300 時間の通路 |
| 2041024 | 造形物 | none |  |  | 220080000 時計塔の奥 |
| 2041025 | 機械装置 | script |  | script:Populatus01 | 220080001 時計塔の深層部 |
| 2041026 | ゴーストハンターボブ | quest-data | 12 | script:giveupTimer | 220070000 忘れられた時間の道1 |
| 2041027 | 収集マニアメイション | quest-data | 4 |  | 220000307 空き家３ |
| 2041028 | 正体不明の盗賊 | quest-data | 3 |  | 922020200 隠れたバルコニー |
| 2041029 | カレン | none |  |  | 222020400 時間制御室 |
| 2042000 | シュピゲルマン | quest-data | 4 | script:mc_enter | 200000000 オルビス 他2 |
| 2042001 | シュピゲルマン | none |  | script:mc_enter1 | 980000100 カーニバルフィールド1&lt;控え室&gt; 他8 |
| 2042002 | シュピゲルマン | none |  | script:mc_move | 103000000 カニングシティー 他19 |
| 2042003 | 助手レッド | none |  | script:mc_roomout | 980000100 カーニバルフィールド1&lt;控え室&gt; 他5 |
| 2042004 | 助手ブルー | none |  | script:mc_roomout | 980000200 カーニバルフィールド2&lt;控え室&gt; 他2 |
| 2042005 | シュピゲルマン | quest-data | 4 | script:mc2_enter | 980030000 シュピゲルマンの事務室 |
| 2042007 | シュピゲルマン | none |  | script:mc2_move | 980030010  他5 |
| 2042008 | 助手レッド | none |  | script:mc2_roomout | 980031000  他2 |
| 2043000 | ビシャスプラント | quest-data | 4 | script:s4time | 922020300 時計塔の深層部 |
| 2050000 | マウンティーン | shop |  |  | 221000200 格納庫 |
| 2050001 | ドクター中村 | quest-data | 20 |  | 221000300 司令室 |
| 2050002 | 外界人グレイ | quest-data | 10 |  | 221040100 クーラン草原2 |
| 2050003 | スペイソン | shop |  |  | 221000200 格納庫 |
| 2050004 | グボ | script |  |  | 221000200 格納庫 |
| 2050005 | チョリ | quest-data | 4 |  | 221000200 格納庫 |
| 2050006 | フニ | quest-data | 4 |  | 221030300 ロスウェル草原3 |
| 2050007 | ガニ | quest-data | 4 |  | 221030500 ロスウェル草原5 |
| 2050008 | マエスター将軍 | quest-data | 9 |  | 221000000 地球防衛本部 |
| 2050009 | 参謀メディン | quest-data | 5 |  | 221000000 地球防衛本部 |
| 2050010 | 兵士ライス | quest-data | 10 |  | 221000000 地球防衛本部 |
| 2050011 | 兵士ケビン | quest-data | 10 |  | 221000000 地球防衛本部 |
| 2050012 | 要員 M | quest-data | 5 |  | 221000000 地球防衛本部 |
| 2050013 | ポーター | quest-data | 15 |  | 221000300 司令室 |
| 2050014 | 隕石1 | none |  | script:earth009 | 221040000 クーラン草原1 |
| 2050015 | 隕石2 | none |  | script:earth010 | 221040200 クーラン草原3 |
| 2050016 | 隕石3 | none |  | script:earth011 | 221040300 クーラン草原4 |
| 2050017 | 隕石4 | none |  | script:earth012 | 221040100 クーラン草原2 |
| 2050018 | 隕石5 | none |  | script:earth013 | 221040201 バーナードの草原 |
| 2050019 | 隕石6 | none |  | script:earth014 | 221040400 クーラン草原5 |
| 2050020 | 輸送船 | quest-data | 2 |  | 221030401 プラティアンの草原 |
| 2051000 | ペパ | shop |  |  | 221000200 格納庫 |
| 2051001 | ケイ | quest-data | 11 |  | 221000200 格納庫 |
| 2060000 | ナヌク | quest-data | 10 |  | 230010201 雪で覆われたクジラ島 |
| 2060001 | ロビンソン | quest-data | 4 |  | 230020201 二本の椰子の木 |
| 2060002 | 漁師爺 | quest-data | 1 |  | 230030101 漁師の渡し船 |
| 2060003 | メリアス | shop |  |  | 230000002 商店街 |
| 2060004 | オアンネス | shop |  |  | 230000002 商店街 |
| 2060005 | ケンタ | quest-data | 66 | script:tamepig_enter | 230000003 動物園 |
| 2060006 | ニュズ | quest-data | 12 |  | 230000003 動物園 |
| 2060007 | カリプン | shop |  |  | 230000002 商店街 |
| 2060008 | ジャラド | script |  |  | 230000002 商店街 |
| 2060009 | イルカ | none |  | script:aqua_taxi | 230000000 アクアリウム 他1 |
| 2060010 | イルカ | none |  | script:aqua_taxi3 | 923020000 座礁された幽霊船 |
| 2060100 | 海の魔女カルタ | quest-data | 17 | script:s4common2 | 230040001 カルタの洞窟 |
| 2060101 | 探査隊長タン | quest-data | 4 |  | 230040401 小さい難波船 |
| 2060103 | ノリングタン | quest-data | 1 | script:PRaid_enter | 923020000 座礁された幽霊船 他5 |
| 2070000 | オオトリ | script |  |  | 222000000 下町 |
| 2070001 | シズク | shop |  |  | 222000000 下町 |
| 2070002 | タツリキ | shop |  |  | 222000000 下町 |
| 2070003 | エンセキ | shop |  |  | 222000000 下町 |
| 2071000 | 黒龍老師 | quest-data | 12 |  | 222000000 下町 |
| 2071001 | キラク | quest-data | 3 |  | 222000000 下町 |
| 2071002 | トクゲツ | quest-data | 10 |  | 222000000 下町 |
| 2071003 | ギン | quest-data | 8 |  | 222000000 下町 |
| 2071004 | サクラコ | quest-data | 14 |  | 222000000 下町 |
| 2071005 | ソラ | quest-data | 10 |  | 222000000 下町 |
| 2071006 | ツバメ | quest-data | 11 |  | 222000000 下町 |
| 2071007 | 千代婆ちゃん | quest-data | 8 |  | 222000000 下町 |
| 2071008 | アサヒ | quest-data | 9 |  | 222000000 下町 |
| 2071009 | 清玄 | quest-data | 4 |  | 222000000 下町 |
| 2071010 | 山の神 | quest-data | 11 |  | 222010002 小さい泉 |
| 2071011 | オサム | quest-data | 3 |  | 222010102 虎の森2 |
| 2071012 | 見覚えがある少女(キツネ) | none |  | script:foxLaidy | 922220000 冷たく寒い森 |
| 2071013 | 黄鬼 | quest-data | 2 |  | 222010402 お化けの家 |
| 2071014 | 青鬼 | quest-data | 2 |  | 222010402 お化けの家 |
| 2071015 | 緑鬼 | quest-data | 2 |  | 222010402 お化けの家 |
| 2072000 | ソラの稲束 | quest-data | 2 |  | 222000000 下町 |
| 2072001 | ギンの稲束 | quest-data | 2 |  | 222000000 下町 |
| 2080000 | モス | none |  | script:minar_weapon | 240000000 リプレ |
| 2080001 | スライ | shop |  |  | 240000002 雑貨屋 |
| 2080002 | マックス | shop |  |  | 240000000 リプレ |
| 2080003 | ノマン | none |  |  | 240010800 空の巣の入口 |
| 2080004 | ムディ | shop |  |  | 240000000 リプレ |
| 2080005 | コスク | script |  |  | 240000000 リプレ |
| 2081000 | 村長タタモ | quest-data | 37 | script:job4_item | 240000000 リプレ |
| 2081001 | クモ | quest-data | 7 |  | 240000005 クモの家 |
| 2081002 | イト | quest-data | 2 |  | 240000004 イトの家 |
| 2081003 | ヤク | quest-data | 7 |  | 240000003 ヤクの家 |
| 2081004 | ファム | quest-data | 5 | script:babyfood | 240000006 ファムの家 |
| 2081005 | ケロベン | none |  | script:hontale_keroben | 240040700 生命の洞窟入口 |
| 2081006 | モイラ | quest-data | 10 |  | 240040700 生命の洞窟入口 |
| 2081007 | 騎士ラウル | quest-data | 15 |  | 240040400 ワイバーンの峡谷 |
| 2081009 | ムス | quest-data | 2 | script:s4blocking_enter | 240010400 森の分かれ道 |
| 2081010 | ムス(FieldsetEnterance) | none |  | script:s4blocking | 924000000 修練場への道 他2 |
| 2081011 | ナインスピリットの子龍 | quest-data | 1 |  | 240040612 ナインスピリットの巣 |
| 2081012 | ニックス | quest-data | 3 |  | 240020600 人里離れた森 |
| 2081013 | 禍々しい玉 | quest-data | 1 |  | 924010000 暗黒の魔女の洞窟 |
| 2081100 | ハルモニア | script | 18 | script:warrior4 | 240010501 師弟の森 |
| 2081200 | グリト | script | 18 | script:magician4 | 240010501 師弟の森 |
| 2081300 | レゴル | script | 15 | script:archer4 | 240010501 師弟の森 |
| 2081400 | ヘリン | script | 15 | script:thief4 | 240010501 師弟の森 |
| 2081500 | セミュオル | script | 13 | script:pirate4 | 240010501 師弟の森 |
| 2082000 | ミュ | script |  | script:sell_ticket | 240000100 チケット売場 他2 |
| 2082001 | タミ | script |  | script:get_ticket | 240000110 ステーション&lt;オルビス行き&gt; |
| 2082002 | ハリモ | script |  | script:goOutWaitingRoom | 240000111 控え室&lt;オルビス行き&gt; |
| 2082003 | コルバ | quest-data | 1 | script:flyminidraco | 240000110 ステーション&lt;オルビス行き&gt; |
| 2083000 | 遠征隊暗号石版 | none |  | script:hontale_enterToE | 240050000 洞窟の入口 |
| 2083001 | ホーンテイルの道標 | none |  | script:hontale_enter1 | 240050000 洞窟の入口 他3 |
| 2083002 | 木の根の水晶 | none |  | script:hontale_out | 240050100 迷路部屋 他13 |
| 2083003 | 迷路部屋の切り株 | none |  | script:hontale_Bdoor | 240050100 迷路部屋 |
| 2083004 | 遠征隊の標識 | none |  | script:hontale_accept | 240050400 ホーンテイルの洞窟入口 |
| 2083005 | 生命の泉 | none |  | script:s4holycharge | 240050400 ホーンテイルの洞窟入口 |
| 2084000 | ゴールドコンパス | none |  | script:goldCompass | 390000000 ゴールドリッチの宝倉庫&lt;1&gt; 他10 |
| 2084002 | ゴールドリッチ | quest-data | 1 |  | 390009999 宝倉庫出口 |
| 2085000 | マタタ | quest-data | 5 | script:Sky_Train | 240080000 天空の渡し場 |
| 2085001 | 天空の扉 | none |  | script:SkyGate | 240080000 天空の渡し場 他1 |
| 2085002 | 天空の扉 | none |  | script:skyquest | 240030102 消えた森 他1 |
| 2090000 | パンちゃん | script |  |  | 250000000 武陵 |
| 2090001 | コウコウ | shop |  |  | 250000000 武陵 |
| 2090002 | ビディ | shop |  |  | 250000000 武陵 |
| 2090003 | ダルちゃん | shop |  |  | 250000002 武陵雑貨店 |
| 2090004 | チエル | quest-data | 6 | script:make_murueng | 250000000 武陵 |
| 2090005 | ツノーレ | script | 1 | script:crane | 200000141 ステーション通路&lt;武陵行き&gt; 他2 |
| 2090006 | ラヤ | none |  |  | 250000000 武陵 |
| 2090100 | ルオさん | script |  | script:hair_mureung1 | 250000003 武陵美容室 |
| 2090101 | リシュ | script |  | script:hair_mureung2 | 250000003 武陵美容室 |
| 2090102 | ナラン | script |  | script:skin_mureung1 | 250000000 武陵 |
| 2090103 | パター | script |  | script:face_mureung1 | 250000000 武陵 |
| 2090104 | ノマン | script |  | script:face_mureung2 | 250000000 武陵 |
| 2091000 | 老工 | quest-data | 16 |  | 250000100 武陵神社 |
| 2091001 | 道工 | quest-data | 25 |  | 250000100 武陵神社 |
| 2091002 | 太上 | quest-data | 16 |  | 250000001 太上の家 |
| 2091003 | ノル | quest-data | 5 |  | 250010500 天桃の果樹園1 |
| 2091004 | 神仙妖怪 | quest-data | 4 |  | 925000000 神仙妖怪の領地 |
| 2091005 | 素公パンダ | quest-data | 4 | script:dojang_enter | 925020001 武陵道場入口 他38 |
| 2091006 | 武陵道場掲示板 | none |  | script:dojang_move | 250000100 武陵神社 |
| 2091007 | 武功 | quest-data | 5 |  | 925040000 武陵道場裏道 |
| 2091008 | ジンジン | quest-data | 3 |  | 250000000 武陵 |
| 2091009 | 封印された社員入口 | none |  | script:enterShadow | 250020300 上級修練場 |
| 2092000 | クーおじいさん | quest-data | 6 |  | 251000000 白草村 |
| 2092001 | 黄船長 | script | 13 |  | 251000000 白草村 |
| 2092100 | ギオン | quest-data | 2 |  | 251000000 白草村 |
| 2092101 | ギオン | none |  | script:Pottery | 925110000 海賊の宝物倉庫 |
| 2093000 | ムタン | shop |  |  | 251000000 白草村 |
| 2093001 | ギタン | shop |  |  | 251000000 白草村 |
| 2093002 | ランミ | shop |  |  | 251000000 白草村 |
| 2093003 | 倉庫業者ゴールドマン | script |  |  | 251000000 白草村 |
| 2094000 | キキョウコライ | quest-data | 5 | script:davyJohn_enter | 251010404 海賊船の向こう |
| 2094001 | キキョウニジン | none |  | script:davy_clear | 925100600 キキョウニジンの感謝 |
| 2094002 | キキョウコライ | none |  | script:davyJohn_play | 925100000 海賊船への道 他10 |
| 2095000 | デリー | quest-data | 1 | script:s4mind | 925010200 デリーを探しに3 他1 |
| 2096000 | 練習記録帳 | none |  | script:sca_dollBear | 250020000 初級修練場 |
| 2100000 | アフマード | script |  |  | 260000000 アリアント |
| 2100001 | ムハマード | quest-data | 6 | script:make_ariant1 | 260000200 アリアント集落地 |
| 2100002 | ザーイド | shop |  |  | 260000000 アリアント |
| 2100003 | ヤスミン | shop |  |  | 260000000 アリアント |
| 2100004 | サガティ | shop |  |  | 260000000 アリアント |
| 2100005 | シャティ | script |  | script:hair_ariant2 | 260000000 アリアント |
| 2100006 | マズラ | script |  | script:hair_ariant1 | 260000000 アリアント |
| 2100007 | ライラ | script |  | script:skin_ariant1 | 260000000 アリアント |
| 2100008 | バドーロ | script |  | script:face_ariant1 | 260000000 アリアント |
| 2100009 | アルディン | script |  | script:face_ariant2 | 260000000 アリアント |
| 2101000 | シリン | quest-data | 15 |  | 260000200 アリアント集落地 |
| 2101001 | ジユル | quest-data | 6 |  | 260000200 アリアント集落地 |
| 2101002 | エレスカ | quest-data | 6 |  | 260000200 アリアント集落地 |
| 2101003 | アディン | quest-data | 6 | script:adin_enter | 260000200 アリアント集落地 |
| 2101004 | ティガン | quest-data | 7 |  | 260000300 アリアント宮殿 |
| 2101005 | バイラン | quest-data | 8 |  | 260000200 アリアント集落地 |
| 2101006 | プリンス | quest-data | 6 |  | 260010500 不毛の砂漠 |
| 2101007 | アレダ | quest-data | 6 |  | 260000303 アリアント宮殿&lt;王室&gt; |
| 2101008 | シェヘラザード | quest-data | 11 |  | 260000303 アリアント宮殿&lt;王室&gt; |
| 2101009 | アプドラ８世 | quest-data | 2 |  | 260000303 アリアント宮殿&lt;王室&gt; |
| 2101010 | ジャノ | quest-data | 9 |  | 260000201 古い空き家 |
| 2101011 | セザン | quest-data | 8 | script:cejan | 260000200 アリアント集落地 |
| 2101012 | アヤマード | quest-data | 7 |  | 260010400 灼熱の砂漠 |
| 2101013 | カルタサ | quest-data | 8 | script:karakasa | 260010600 流浪団のテント |
| 2101014 | セザール | quest-data | 2 | script:aMatchEnt | 980010000 闘技場の控え室 |
| 2101015 | アブドラ８世 | none |  | script:aMatchScore | 980010010 王の部屋 |
| 2101016 | アレダ | none |  | script:aMatchRwd | 980010010 王の部屋 |
| 2101017 | セザール | none |  | script:aMatchPlay | 980010100 一番目の闘技場&lt;控え室&gt; 他5 |
| 2101018 | セザール | quest-data | 2 | script:aMatchMove | 102000000 ぺリオン 他2 |
| 2102000 | アセソン | none |  | script:get_ticket | 260000100 アリアント乗降場 |
| 2102001 | シュリン | script |  | script:goOutWaitingRoom | 260000110 控え室&lt;オルビス行き&gt; |
| 2102002 | シラス | script |  | script:sell_ticket | 260000100 アリアント乗降場 |
| 2103000 | 王宮オアシス | none |  | script:ariant_oasis | 260000300 アリアント宮殿 |
| 2103001 | 秘密の壁 | none |  | script:secret_wall | 260000200 アリアント集落地 |
| 2103002 | 王妃の飾り棚 | none |  | script:ariant_ring | 260000303 アリアント宮殿&lt;王室&gt; |
| 2103003 | アリアント民家1 | none |  | script:ariant_house1 | 260000202 民家1 |
| 2103004 | アリアント民家2 | none |  | script:ariant_house2 | 260000203 民家2 |
| 2103005 | アリアント民家4 | none |  | script:ariant_house3 | 260000205 民家4 |
| 2103006 | アリアント民家6 | none |  | script:ariant_house4 | 260000207 民家6 |
| 2103007 | 宝箱 | quest-data | 2 |  | 260010402 盗賊の巣窟 |
| 2103008 | 奇妙な声 | none |  | script:thief_in2 | 260010401 岩坂 |
| 2103009 | 民家1収納場所(透明) | none |  | script:ariant_gold1 | 260000202 民家1 |
| 2103010 | 民家2収納場所(透明) | none |  | script:ariant_gold2 | 260000203 民家2 |
| 2103011 | 民家4収納場所(透明) | none |  | script:ariant_gold3 | 260000205 民家4 |
| 2103012 | 民家7収納場所(透明) | none |  | script:ariant_gold4 | 260000207 民家6 |
| 2103013 | デュアート | quest-data | 1 | script:dooat | 926010000 ピラミッドの丘 他42 |
| 2110000 | ロスン | script |  |  | 261000000 マガティア |
| 2110001 | ジェリ | shop |  |  | 261000000 マガティア |
| 2110002 | キオル | quest-data | 26 |  | 261000002 マレンの作業室 |
| 2110003 | ラマイン | quest-data | 3 |  | 261000002 マレンの作業室 |
| 2110004 | マレン | quest-data | 17 |  | 261000002 マレンの作業室 |
| 2110005 | ラクダ | quest-data | 13 | script:nihal_taxi | 260020000 アリアント北門の外 他1 |
| 2111000 | カソン | quest-data | 12 | script:jenu_homun | 261000010 ジェニミスト協会 |
| 2111001 | マッド | quest-data | 12 |  | 261000020 アルカドノ協会 |
| 2111002 | ドラン | quest-data | 4 |  | 926120200 ドランの研究室 |
| 2111003 | ヒュモノイドA | quest-data | 9 | script:snow_rose | 261000000 マガティア |
| 2111004 | フィリア | quest-data | 14 |  | 261000000 マガティア |
| 2111005 | キニ | quest-data | 12 |  | 261000000 マガティア |
| 2111006 | ファウェン | quest-data | 13 | script:drang_room1 | 261020401 関係者以外出入禁止区域 |
| 2111007 | ブローカーハン | quest-data | 15 |  | 261000000 マガティア |
| 2111008 | ベディン | quest-data | 10 |  | 261010000 研究所1階廊下 |
| 2111009 | ラセルロン | quest-data | 12 |  | 261020000 研究所中央ゲート |
| 2111010 | アルカドノの本棚 | none |  | script:magatia_dark1 | 926120000 光が消えた研究室 |
| 2111011 | 失踪した錬金術師の家の壁(透明) | none |  | script:absence_wall | 261000001 失踪した錬金術師の家 |
| 2111012 | 失踪した錬金術師の家の本棚(透明) | none |  | script:absence_box | 261000001 失踪した錬金術師の家 |
| 2111013 | 失踪した錬金術師の家の額縁(透明) | none |  | script:absence_frame | 261000001 失踪した錬金術師の家 |
| 2111014 | 失踪した錬金術師の家の机(透明) | none |  | script:absence_desk | 261000001 失踪した錬金術師の家 |
| 2111015 | ラセルロンの机(透明) | none |  | script:alcadno_potion | 261020200 研究所B-1区域 |
| 2111016 | ドランの秘密本 | quest-data | 3 |  | 261000001 失踪した錬金術師の家 |
| 2111017 | 一番目のパイプ取っ手(透明) | none |  | script:pipe1 | 261000001 失踪した錬金術師の家 |
| 2111018 | 二番目のパイプ取っ手(透明) | none |  | script:pipe2 | 261000001 失踪した錬金術師の家 |
| 2111019 | 三番目のパイプ取っ手(透明) | none |  | script:pipe3 | 261000001 失踪した錬金術師の家 |
| 2111020 | 一番目の魔法陣(透明) | none |  | script:alceCircle1 | 261040000 暗黒の魔法使いの研究室 |
| 2111021 | 二番目の魔法陣(透明) | none |  | script:alceCircle2 | 261040000 暗黒の魔法使いの研究室 |
| 2111022 | 三番目の魔法陣(透明) | none |  | script:alceCircle3 | 261040000 暗黒の魔法使いの研究室 |
| 2111023 | 魔法陣中央(透明) | none |  | script:alceCircle4 | 261040000 暗黒の魔法使いの研究室 |
| 2111024 | 秘密通路(透明) | none |  | script:secretNPC | 261010000 研究所1階廊下 他1 |
| 2111025 | 制御装置 | none |  | script:sca_auto | 261020401 関係者以外出入禁止区域 |
| 2111026 | 未完成魔法陣 | none |  | script:sca_DitRoi | 261010102 研究所202号 |
| 2112003 | ジュリエット | quest-data | 11 | script:juliet_start | 261000021 アルカドノ秘密の部屋 他5 |
| 2112004 | ロミオ | quest-data | 13 | script:romio_start | 261000011 ジェニミスト秘密の部屋 他5 |
| 2112005 | ジュリエット(進行) | none |  | script:juliet | 926110200 特殊な実験室 |
| 2112006 | ロミオ(進行) | none |  | script:romio | 926100200 特殊な実験室 |
| 2112007 | 調査結果 | none |  | script:rnj_look | 926100000 怪しい研究室 他1 |
| 2112013 | 調査結果 | none |  | script:jnr_look | 926110000 怪しい研究室 他2 |
| 2112014 | ユレテ | quest-data | 12 |  | 926130000 ユレテの研究室 |
| 2112016 | 隠された文書 | none |  | script:q3367npc | 926130102 ユレテの実験室2 |
| 2112017 | 落ちている紙くず | quest-data | 2 |  | 261000002 マレンの作業室 |
| 2120000 | 仮面紳士 | quest-data | 18 | script:maskScript | 229000000 中央ホール  |
| 2120001 | 門番 | none |  | script:gateKeeper | 229010000 庭園 |
| 2120002 | 執事 | none |  | script:halloweenpq | 229000000 中央ホール  他9 |
| 2120003 | メイド | quest-data | 8 | script:hwamber | 229000000 中央ホール  |
| 2120004 | ジョナス | quest-data | 16 | script:giveUpDoll | 229000310 人形工房 |
| 2120005 | ソフィリア | quest-data | 12 |  | 229000100 ソフィリアの部屋 |
| 2120006 | ルドミーラ | quest-data | 14 | script:rudeScript | 229000210 ジョナスの部屋 |
| 2120007 | ジョイ | quest-data | 6 |  | 229000212 隠された部屋 |
| 2120008 | 幽霊T | quest-data | 8 | script:ghostScript | 229000300 煙突 |
| 2120009 | 執事 | none |  | script:hwreward | 229030300 執事の部屋 |
| 2121000 | 名もなき猫 | quest-data | 4 |  | 229010000 庭園 |
| 2121001 | 碑石が倒れた墓 | none |  | script:tablet01 | 229010100 墓地 |
| 2121002 | 名のない墓 | none |  | script:tablet02 | 229010100 墓地 |
| 2121003 | 訪ねる者のない墓 | none |  | script:tablet03 | 229010100 墓地 |
| 2121004 | 誰かの墓 | none |  | script:tablet04 | 229010100 墓地 |
| 2121005 | ピアノ | none |  | script:musicNote | 229000000 中央ホール  |
| 2121006 | 誰かの額縁1 | none |  | script:picture1 | 229000211 額縁部屋 |
| 2121007 | 誰かの額縁2 | none |  | script:picture4 | 229000211 額縁部屋 |
| 2121008 | 誰かの額縁3 | none |  | script:picture5 | 229000211 額縁部屋 |
| 2121009 | 誰かの額縁4 | none |  | script:picture3 | 229000211 額縁部屋 |
| 2121010 | 誰かの額縁5 | none |  | script:picture2 | 229000211 額縁部屋 |
| 2121011 | ソフィリアの額縁 | none |  | script:hwpicture | 229000211 額縁部屋 |
| 2130000 | マヨルン | shop |  |  | 300000000 森のキャンプ |
| 2131000 | ヘレナ | quest-data | 14 |  | 300000010 キャンプ会議場 |
| 2131001 | ペルゼン | quest-data | 2 |  | 300000000 森のキャンプ |
| 2131002 | ユリス | quest-data | 3 |  | 300000000 森のキャンプ |
| 2131003 | ロハ | quest-data | 3 |  | 300000000 森のキャンプ |
| 2131004 | 寝ている赤ちゃん | quest-data | 2 |  | 300000002 テント2 |
| 2131005 | シオン | quest-data | 1 |  | 300000001 テント1 |
| 2131006 | ドル | quest-data | 2 |  | 300000000 森のキャンプ |
| 2131007 | テス | quest-data | 2 |  | 300000000 森のキャンプ |
| 2132000 | カンデルン | quest-data | 6 |  | 300010400 岩山入口 |
| 2132001 | ロード | quest-data | 4 |  | 300020200 キノコ丘入口 |
| 2132002 | リオス | quest-data | 2 |  | 300010300 苔の森一本道 |
| 2132003 | シャドリオン | quest-data | 2 |  | 300010200 苔の森西2 |
| 2133000 | エリン | quest-data | 1 | script:party6_entry | 300030100 深い妖精の森 |
| 2133001 | エリン | none |  | script:party6_elin | 930000000 森の前 他7 |
| 2133002 | エリン森道しるべ | none |  | script:party6_giveUp | 930000300 霧の森 |
| 2133004 | スプライト | none |  | script:party6_spra | 930000500 森の広場 |
| 2140000 | 神殿管理人 | quest-data | 58 |  | 270000000 ３つの門 |
| 2140001 | 観照者 | quest-data | 3 |  | 270010111 観照者の部屋 |
| 2140002 | 魔法製錬術師 | quest-data | 3 |  | 270020211 魔法製錬術師の部屋 |
| 2140003 | 記録者 | quest-data | 2 |  | 270030411 記録者の部屋 |
| 2141001 | 忘れられた神殿管理人 | none |  | script:PinkBeen_accept | 270050000 忘れられた黄昏 |
| 2141002 | 忘れられた神殿管理人 | none |  | script:PinkBeen_Out | 270050100 神々の黄昏 他1 |
| 9000000 | ポル | none |  | script:Event00 | 261000000 マガティア |
| 9000001 | ジャング | none |  | script:Event00 | 104000000 港口 |
| 9000002 | ピエトロ | none |  | script:Event02 | 109050000 商品交換所 |
| 9000003 | バイカン | none |  | script:Event03 | 109010000 宝を探せ！ |
| 9000004 | バイコン | none |  | script:Event03 | 109010100 東フィールド |
| 9000005 | バイケン | none |  | script:Event03 | 109010200 南フィールド |
| 9000006 | バイクン | none |  | script:Event03 | 109050001 イベント出口 |
| 9000007 | 天地 | none |  | script:Event04 | 103010000 工事現場 |
| 9000008 | 開け屋 | quest-data | 5 | script:Event05 | 103000000 カニングシティー |
| 9000009 | バイキン | quest-data | 11 | script:Event03_1 | 104000000 港口 |
| 9000010 | ピエトラ | none |  | script:Event06 | 109050001 イベント出口 |
| 9000011 | マティン | none |  | script:Event00 | 200000000 オルビス |
| 9000012 | ハーリー | none |  | script:Event09 | 109080000 ココナッツシーズン 他3 |
| 9000013 | トニ | none |  | script:Event00 | 220000000 ルディブリアム |
| 9000014 | ジニ | quest-data | 3 |  | 220000000 ルディブリアム |
| 9000015 | タミス | none |  |  | 109010000 宝を探せ！ |
| 9000018 | マチルダ | script |  | script:pc_weapon | 100000000 ヘネシス 他11 |
| 9000019 | ジャンケンマスター | script |  |  | 100000000 ヘネシス 他7 |
| 9000020 | スピネル | script |  | script:world_trip | 100000000 ヘネシス 他18 |
| 9000021 | ガガ | script | 124 | script:getRank | 100000000 ヘネシス 他19 |
| 9000031 | カサンドラ | none |  | script:out_jp7th | 805000100 地下監獄入口 |
| 9000033 | 要員C | quest-data | 2 |  | 910010100 近道 他7 |
| 9000039 | 要員W | none |  | script:watermelon_out | 922210300 スイカ畑出口 |
| 9000040 | ダリア | quest-data | 76 | script:medal_rank | 100000200 広場 他6 |
| 9000041 | 寄付 | none |  | script:Donation | 100000200 広場 他6 |
| 9000042 | ガガ | quest-data | 2 | script:babyBird | 910020000 ヒナの巣 |
| 9000043 | 迷子の渡り鳥 | quest-data | 2 | script:lostDoyo | 910020000 ヒナの巣 |
| 9000044 | 迷子の渡り鳥 | none |  | script:itemDoyo | 910020100 踏切板への棘の罠 他15 |
| 9000049 | 童話妖精クレコス | none |  | script:treasureHunter | 180000000 撮影現場 他1 |
| 9000055 | アルミ | quest-data | 1 | script:armi | 970010000 紅葉の木の庭園 |
| 9000059 | ジンジャーマン | quest-data | 2 | script:PB_bossOut | 980044200 魔女の塔最上階 |
| 9000060 | ジンジャーマン | none |  | script:PB_bossOut | 980041200 魔女の塔最上階 |
| 9000072 | ソムチャイ | quest-data | 6 |  | 809060000 金箔寺 他1 |
| 9000073 | ルン大寺 | quest-data | 4 |  | 809060000 金箔寺 他1 |
| 9000074 | ノイ | quest-data | 4 |  | 809060000 金箔寺 他1 |
| 9000075 | チャン | none |  | script:MD_goblin | 809060000 金箔寺 他1 |
| 9000076 | ターイ | quest-data | 4 |  | 809060000 金箔寺 他1 |
| 9000077 | トンジャン | quest-data | 4 |  | 809060000 金箔寺 他1 |
| 9000078 | ユース | quest-data | 7 | script:outGoldenTemple | 809060000 金箔寺 他1 |
| 9000079 | リタヤ | quest-data | 4 |  | 809060000 金箔寺 他1 |
| 9000080 | ダオ | none |  | script:MD_monkey | 809060000 金箔寺 他1 |
| 9000081 | タンタワン | shop |  |  | 809060000 金箔寺 他1 |
| 9000082 | ポン | none |  | script:Ravana_out | 809061010 悪霊の神殿 他3 |
| 9001004 | 北極熊のフープ | none |  | script:Event10 | 109080010  他2 |
| 9001101 | 達人うさぎ | quest-data | 1 |  | 922230000 月の国 |
| 9001102 | 月うさぎ | quest-data | 2 | script:giveupMoonPicture | 100000000 ヘネシス 他18 |
| 9001103 | 商人うさぎ | quest-data | 2 |  | 922230000 月の国 他1 |
| 9001104 | 学者うさぎ | quest-data | 1 |  | 922230000 月の国 他1 |
| 9001105 | 할아버지 월묘 | none |  | script:spaceGaGa_papa | 922231001 月うさぎの遊び場 他4 |
| 9001106 | 迷子のグレイ | none |  |  | 922230000 月の国 |
| 9001107 | 案内うさぎ | none |  | script:outRabbitJump | 922231000 月うさぎの遊び場 他1 |
| 9001108 | 案内うさぎ | none |  | script:moonFlower | 922230000 月の国 他21 |
| 9010000 | イベントガイド | script | 305 | script:gachaponStamp | 2000000 サウスペリ 他15 |
| 9010009 | ドイ | none |  | parcel | 100000000 ヘネシス 他15 |
| 9010010 | カサンドラ | quest-data | 471 | script:kasandra_7th | 100000000 ヘネシス 他18 |
| 9010014 | アルミ | quest-data | 2 |  | 100000200 広場 |
| 9010017 | 開発者の人形 | none |  | script:test | 180000000 撮影現場 |
| 9010018 | クリシャ | none |  | script:mapleTCG | 220000000 ルディブリアム |
| 9010020 | 魔女の墓 | quest-data | 6 |  | 980040010 魔女の墓 |
| 9010022 | 次元の鏡 | script |  | script:unityPortal | 100000000 ヘネシス 他17 |
| 9020000 | ラケリース | script | 2 | script:party1_enter | 103000000 カニングシティー |
| 9020001 | クロート | script |  | script:party1_play | 103000800 一つ目の同行&lt;1st&gt; 他10 |
| 9020002 | ネーラ | script |  | script:party1_out | 103000800 一つ目の同行&lt;1st&gt; 他14 |
| 9030000 | プレドリック | script |  | storebank | 910000000 フリーマーケット入口 |
| 9030100 | スクルジ | script |  |  | 910000000 フリーマーケット入口 |
| 9040000 | モニカ | quest-data | 10 | script:guildquest1_enter | 101030104 遺跡発掘ベースキャンプ |
| 9040001 | ヌリス | none |  | script:guildquest1_clear | 990001100 帰り道 |
| 9040002 | シャン | quest-data | 5 | script:guildquest1_comment | 101030104 遺跡発掘ベースキャンプ |
| 9040004 | 名誉の石碑 | none |  |  | 102000000 ぺリオン 他5 |
| 9040005 | 帰還碑 | none |  | script:guildquest1_out | 990000100 守護の谷 他8 |
| 9040006 | 正邪の彫刻 | none |  | script:guildquest1_baseball | 990000500 賢者の噴水 |
| 9040007 | シャレン3世の遺言書 | none |  | script:guildquest1_will | 990000600 地下水路 |
| 9040008 | ギルドランキング掲示板 | none |  |  | 101030104 遺跡発掘ベースキャンプ |
| 9040009 | ライオン像 | none |  | script:guildquest1_statue | 990000300 シャレニアン城門 |
| 9040010 | キメラ像 | none |  | script:guildquest1_bonus | 990000900 エレゴスの王子 |
| 9040011 | 掲示板 | none |  | script:guildquest1_board | 101030104 遺跡発掘ベースキャンプ 他1 |
| 9040012 | 騎士鎧 | none |  | script:guildquest1_knight | 990000400 騎士のホール |
| 9060000 | ケンタ | none |  | script:tamepig_out | 923010000 ケンタの飼育室 |
| 9100100 | ガシャポン | script |  | script:gachapon1 | 101000000 エリニア |
| 9100101 | ガシャポン | script |  | script:gachapon2 | 100000100 市場 |
| 9100102 | ガシャポン | script |  | script:gachapon3 | 102000000 ぺリオン |
| 9100103 | ガシャポン | script |  | script:gachapon4 | 103000000 カニングシティー |
| 9100104 | ガシャポン | script |  | script:gachapon5 | 105040300 スリーピーウッド |
| 9100105 | ガシャポン | script |  | script:gachapon6 | 211000100 市場 |
| 9100106 | ガシャポン | script |  | script:gachapon7 | 800000000 キノコ神社 |
| 9100107 | ガシャポン | script |  | script:gachapon8 | 120000200 中央廊下 他1 |
| 9100108 | ガシャポン | script |  | script:gachapon9 | 809000201 銭湯（女） |
| 9100109 | ガシャポン(パチ) | script |  | script:gachapon10 | 809030000 パチンコ屋 |
| 9100110 | ガシャポン(ネット) | script |  | script:gachapon11 | 193000000 ネットカフェ |
| 9100111 | ガシャポン(キャンペーン) | script |  | script:gachapon12 | 104000000 港口 |
| 9100112 | ガシャポン（兵法書） | script |  | script:gachapon13 | 104000000 港口 |
| 9100113 | ガシャポン(モバイル) | script |  | script:gachapon14 | 100000000 ヘネシス |
| 9100200 | パチンコ1 | none |  | script:Pachinko_machine0 | 809030000 パチンコ屋 |
| 9100201 | パチンコ2 | none |  | script:Pachinko_machine0 | 809030000 パチンコ屋 |
| 9100202 | パチンコ3 | none |  | script:Pachinko_machine1 | 809030000 パチンコ屋 |
| 9100203 | パチンコ4 | none |  | script:Pachinko_machine1 | 809030000 パチンコ屋 |
| 9100204 | パチンコ5 | none |  | script:Pachinko_machine2 | 809030000 パチンコ屋 |
| 9100205 | パチンコ6 | none |  | script:Pachinko_machine2 | 809030000 パチンコ屋 |
| 9102000 | スコーン | quest-data | 2 |  | 190000000 別の寺院 |
| 9102001 | ガルノックス | quest-data | 12 |  | 100000000 ヘネシス 他4 |
| 9102002 | オスト | quest-data | 3 | script:cashRiding | 100000000 ヘネシス 他17 |
| 9102100 | ? | none |  | script:multipet_success | 100000202 ペットの散歩路 |
| 9102101 | ? | none |  | script:multipet_fail | 100000202 ペットの散歩路 |
| 9103000 | ピエトル | none |  | script:party_ludimaze_goal | 809050015 迷路 |
| 9103001 | ガイドモモ | none |  | script:party_ludimaze_enter | 220000000 ルディブリアム |
| 9103002 | ガイドララ | none |  | script:party_ludimaze_success | 809050016 商品交換所 |
| 9103003 | ガイドルル | none |  | script:party_ludimaze_fail | 809050017 イベント出口 |
| 9104000 | アル | quest-data | 3 | script:Ani_questJP | 100000200 広場 |
| 9104001 | ニーナ | quest-data | 7 |  | 105040300 スリーピーウッド |
| 9105006 | 伝説のカリスマ美容師 | script |  | script:legend_hair | 801000001 美容院 |
| 9105009 | ナオミ | quest-data | 4 | script:levelContents | 100000000 ヘネシス 他20 |
| 9105011 | 堕落した力の戦士 | quest-data | 2 |  | 102000000 ぺリオン |
| 9105012 | 睦実 | quest-data | 1 |  | 220000400 エオス塔入口 |
| 9105017 | ボンちゃん | none |  | script:checkBlackDragon | 804000400 オルビス公園 他1 |
| 9105018 | デュアルブレイド | quest-data | 8 |  | 804000010 森の中 他1 |
| 9105019 | 助手みどり | none |  | script:hair_EVDB | 100000000 ヘネシス |
| 9105020 | 睦実 | quest-data | 1 |  | 804000610 秘桜蔭1階 |
| 9105021 | エヴァン | quest-data | 2 |  | 804000620 秘桜蔭1階 |
| 9110000 | ペリー | script |  | script:goKinoc | 103000000 カニングシティー |
| 9110001 | 雷霧侍 | shop |  |  | 800000000 キノコ神社 |
| 9110002 | 木野子のこ | quest-data | 21 | script:mushgirl | 800000000 キノコ神社 |
| 9110003 | ジャケン | shop |  |  | 800000000 キノコ神社 |
| 9110004 | タルー | shop |  |  | 800000000 キノコ神社 |
| 9110005 | ブロンズ | shop |  |  | 800000000 キノコ神社 |
| 9110006 | ジンジャー | shop |  |  | 800000000 キノコ神社 |
| 9110007 | ロボ | shop |  |  | 800000000 キノコ神社 |
| 9110008 | ペリー | none |  | script:goKerning | 800000000 キノコ神社 |
| 9110009 | 賽銭箱 | none |  | script:God2010 | 800000000 キノコ神社 |
| 9110010 | キノコの像 | quest-data | 2 | script:surfing | 800000000 キノコ神社 |
| 9110100 | ヨッコラ | shop |  |  | 800040000 楓城 天下泰平 |
| 9110101 | テヤンデ | shop |  |  | 800040000 楓城 天下泰平 |
| 9110102 | イシラズ | shop |  |  | 800040209 楓城 百間廊下 |
| 9110103 | 葵 | quest-data | 5 |  | 800040000 楓城 天下泰平 |
| 9110104 | ひょっとこ | quest-data | 11 |  | 800040100 楓城 城門内 |
| 9110105 | ナオスケ | none |  | script:ninja_maze | 800040211 楓城 百間廊下 |
| 9110106 | 杏姫 | quest-data | 10 |  | 800040100 楓城 城門内 |
| 9110108 | 鷹匠 | quest-data | 5 |  | 800040100 楓城 城門内 |
| 9110109 | ペッタン | quest-data | 1 | script:mission_9110109 | 800040100 楓城 城門内 |
| 9110110 | ウスケ | quest-data | 2 |  | 800040000 楓城 天下泰平 |
| 9110111 | サスケ | quest-data | 6 |  | 800040000 楓城 天下泰平 |
| 9110112 | カエデヤ | quest-data | 4 |  | 800040000 楓城 天下泰平 |
| 9110113 | ビュンビュン | quest-data | 2 | script:LuckyBag | 800040000 楓城 天下泰平 |
| 9110114 | ナガレ | quest-data | 2 |  | 800040100 楓城 城門内 |
| 9110115 | ナオスケ | none |  | script:JP_medal1 | 800040500 楓城 装備部屋 |
| 9110116 | イシラズ | none |  | script:JP_medal1_out | 800040500 楓城 装備部屋 |
| 9110200 | ドーク | none |  | script:Keconsiki | 889300201 ウェディングホール(セカンドウェディング) |
| 9110201 | イノン | none |  | script:Kecon | 680000000 ウェディングタウン 他1 |
| 9110202 | ギンコ | none |  | script:watingKecon | 889300200 ウェディングホール待機室(セカンドウェディング) 他1 |
| 9110203 | ノイン | none |  | script:beginCeremony3 | 889300201 ウェディングホール(セカンドウェディング) |
| 9110204 | チャヨ | none |  | script:KeconCoordinator | 680000000 ウェディングタウン |
| 9110205 | トーマス | none |  | script:Thomas2 | 889300600 出口1 他2 |
| 9120000 | シンタ | shop |  |  | 801000300 ショーワ町通り |
| 9120001 | ハナコ | shop |  |  | 801000300 ショーワ町通り |
| 9120002 | ドラン | shop |  |  | 801000300 ショーワ町通り |
| 9120003 | ヒカリ | quest-data | 8 | script:in_bath | 801000000 ショーワ町 |
| 9120004 | モモヨ | shop |  |  | 801000100 脱衣所（男） |
| 9120005 | うみい | quest-data | 19 |  | 801000300 ショーワ町通り |
| 9120006 | スカイ | quest-data | 6 | script:mapletour3 | 801000300 ショーワ町通り |
| 9120007 | ふらの | quest-data | 8 |  | 801000300 ショーワ町通り |
| 9120008 | ツーリ | quest-data | 12 | script:zakum_cap | 801000300 ショーワ町通り |
| 9120009 | 倉庫ユッセ | script |  |  | 801000000 ショーワ町 |
| 9120010 | ファイト | quest-data | 6 | script:whitto | 801000300 ショーワ町通り |
| 9120011 | さくら | quest-data | 23 |  | 801000000 ショーワ町 |
| 9120012 | 傷だらけの猫 | quest-data | 6 |  | 801000000 ショーワ町 |
| 9120013 | ボス猫 | quest-data | 6 | script:boss_cat | 801000000 ショーワ町 |
| 9120014 | ピポパ | quest-data | 5 | script:hina00 | 801000000 ショーワ町 |
| 9120015 | コンペイ | none |  | script:con1 | 801000000 ショーワ町 |
| 9120016 | マリワカ | quest-data | 18 |  | 801000300 ショーワ町通り |
| 9120017 | ポニチャイ | quest-data | 17 | script:mushroom_stamp | 801000000 ショーワ町 |
| 9120018 | グラコ | quest-data | 7 | script:hina01 | 801000000 ショーワ町 |
| 9120019 | モモヨ | shop |  |  | 801000200 脱衣所（女） |
| 9120020 | ミンシュタイン | none |  | script:zcap_out | 809020000 ジャクムの兜パワーアップ |
| 9120021 | 貝殻 | none |  | script:hina03 | 809010000 断崖絶壁 |
| 9120022 | ミンシュタイン | none |  | script:hina_out | 809010000 断崖絶壁 |
| 9120023 | ヨコヨコ | quest-data | 8 |  | 801000300 ショーワ町通り |
| 9120024 | ウエリバ | quest-data | 12 |  | 801000300 ショーワ町通り |
| 9120025 | アーシア | quest-data | 43 |  | 802000101 カムナ |
| 9120026 | クリスタル | script |  | script:tell_Tokyo | 800040000 楓城 天下泰平 他2 |
| 9120027 | ポニチャル | quest-data | 8 |  | 802000200 お台場 2100年 |
| 9120028 | シンマイ | none |  |  | 802000300 公園 2095年 |
| 9120029 | ディーダ | quest-data | 4 |  | 802000300 公園 2095年 |
| 9120030 | マール | none |  | script:Go_boss2_out | 802000300 公園 2095年 他2 |
| 9120031 | ガルーダ司令 | quest-data | 4 |  | 802000500 秋葉原司令室 2102年 |
| 9120032 | マリーシャス大尉 | quest-data | 2 |  | 802000500 秋葉原司令室 2102年 |
| 9120033 | ディーダ | quest-data | 4 |  | 802000600 旗艦ファイア・オールドフォックス 2102年 |
| 9120034 | ノラン | none |  | script:Make_Stone | 802000500 秋葉原司令室 2102年 |
| 9120035 | 旗艦ファイア・オールドフォックス支援AI | quest-data | 4 |  | 802000600 旗艦ファイア・オールドフォックス 2102年 |
| 9120036 | アーシア | none |  | script:Go_boss5 | 802000110 カムナ 他1 |
| 9120037 | ガルーダ司令 | none |  | script:Go_boss3 | 802000410 秋葉原 2102年 他1 |
| 9120038 | ディーダ | none |  | script:Go_boss2 | 802000310 公園 2095年 |
| 9120039 | 旗艦ファイア・オールドフォックス支援AI | none |  | script:Go_boss4 | 802000610 旗艦ファイア・オールドフォックス甲板 2102年 他1 |
| 9120040 | ポニチャル | none |  | script:Go_boss1 | 802000210 お台場 2100年 他1 |
| 9120041 | ポニチャル | quest-data | 4 |  | 802000500 秋葉原司令室 2102年 |
| 9120045 | ? | none |  | script:JP_medal5 | 105040402 回復サウナ室＜高級＞ |
| 9120046 | アシュレイ | quest-data | 2 |  | 802000825 六本木モール最上階2102年 |
| 9120047 | ディーダ | quest-data | 9 |  | 802000700 渋谷 2102年 |
| 9120049 | 倒れた少女 | quest-data | 1 |  | 802000823 六本木モール最上階2102年 |
| 9120050 | 入室制御装置 | none |  | script:Go_boss7 | 802000820 六本木モール最上階2102年 他1 |
| 9120052 | ディーダ | none |  | script:Go_boss6 | 802000710 渋谷2102年 他1 |
| 9120053 | 入室制御装置 | none |  | script:TokyoPQ | 802000800 六本木モール2102年 他4 |
| 9120054 | ジャック | none |  | script:CrimsonStoryL | 803000700 最奥への通路

 他6 |
| 9120055 | ジャック | none |  | script:CrimsonStoryH | 803010700 最奥への通路
 他6 |
| 9120056 | フィアンセ | quest-data | 14 |  | 103040000 カニングスクエアロビー |
| 9120057 | ダーリン | quest-data | 8 |  | 103040103 1階 2階 D区域 |
| 9120058 | ダーリン | quest-data | 1 |  | 103040000 カニングスクエアロビー |
| 9120059 | ブラザーバブルティー | quest-data | 2 |  | 103040101 1階 2階 B区域 |
| 9120100 | 美容師テッペイ | script |  | script:hair_shouwa1 | 801000001 美容院 |
| 9120101 | 助手みどり | none |  | script:hair_shouwa2 | 801000001 美容院 |
| 9120102 | ヒゲクロ先生 | none |  | script:face_shouwa1 | 801000002 整形外科 |
| 9120103 | 助手サエコ | none |  | script:face_shouwa2 | 801000002 整形外科 |
| 9120104 | ナオコ | none |  |  | 809030000 パチンコ屋 |
| 9120105 | キャサリン | none |  | script:pachinkoDungeonEnter | 100000000 ヘネシス 他14 |
| 9120106 | パチンコ玉交換機 | none |  | script:Pachinko_dama_machine | 809030000 パチンコ屋 |
| 9120107 | キャサリン | quest-data | 2 | script:pachinkoDungeonEnter | 809030000 パチンコ屋 |
| 9120108 | キャサリン | none |  | script:pachinkoDungeonEnter | 809030100 パチンコミニダンジョン 他11 |
| 9120109 | フミヤ | script |  | script:hair_Hagaren | 200000202 オルビスヘアーショップ |
| 9120200 | コンペイ | none |  | script:con2 | 801040000 アジト前 |
| 9120201 | コンペイ | none |  | script:s_dungeon | 801040004 武器庫 |
| 9120202 | コンペイ | none |  | script:con3 | 801040100 悪夢の果て |
| 9120203 | コンペイ | none |  | script:con4 | 801040101 アジト前(天晴れ) |
| 9201000 | ムーニ | quest-data | 2 | script:EngageRing | 680000000 ウェディングタウン |
| 9201002 | 教皇ジョン | none |  | script:HighPriest | 680000000 ウェディングタウン 他1 |
| 9201004 | 聖賢エームズ | none |  | script:wedding_Info | 680000000 ウェディングタウン |
| 9201005 | ニコル | none |  | script:cathedral | 680000000 ウェディングタウン 他1 |
| 9201006 | デビー | none |  | script:watingCathedral | 680000200 ウェディングホール待機室(大聖堂) 他1 |
| 9201007 | ナンシー | none |  | script:beginCeremony | 680000210 ウェディング(大聖堂) |
| 9201008 | ボニー | none |  | script:Chapel | 680000000 ウェディングタウン 他1 |
| 9201009 | ジャッキー | none |  | script:watingChapel | 889300100 ウェディングホール待機室(ハウスウェディング) 他1 |
| 9201010 | トラヴィス | none |  | script:beginCeremony2 | 889300101 ウェディング(ハウスウェディング) |
| 9201011 | ビバップ | none |  | script:Vibab | 889300101 ウェディング(ハウスウェディング) |
| 9201012 | ウェイン | none |  | script:ChapelCoordinator | 680000000 ウェディングタウン |
| 9201013 | ビクトリア | none |  | script:cathedralCoordinator | 680000000 ウェディングタウン |
| 9201014 | ピーラ | none |  | script:divorce | 680000000 ウェディングタウン |
| 9201015 | ジュリアス | none |  | script:hair_wedding1 | 680000002 美容院 |
| 9201016 | シェイマス | none |  | script:hair_wedding2 | 680000002 美容院 |
| 9201018 | アルバーツ | none |  | script:face_wedding1 | 680000003 整形外科 |
| 9201019 | シャキ | none |  | script:face_wedding2 | 680000003 整形外科 |
| 9201020 | ビビアン | shop |  | script:weddingFashion | 680000200 ウェディングホール待機室(大聖堂) |
| 9201021 | ロビン | none |  | script:weddingParty | 680000300 ウェディングフォトスタジオ 他7 |
| 9201022 | トーマス | none |  | script:Thomas | 680000500 出口 他2 |
| 9201023 | ナナ | none |  | script:amoria_enter | 100000200 広場 他1 |
| 9201035 | ジャコブ | none |  | script:ringChange | 680000000 ウェディングタウン |
| 9201036 | アンジェリーク | none |  | script:presentExchange | 680000000 ウェディングタウン 他3 |
| 9201037 | ガリ&amp;シャティマ | none |  | script:loveOath | 680000000 ウェディングタウン |
| 9201051 | ジョン・バリケード | quest-data | 2 | script:naomi | 104000000 港口 |
| 9201052 | フォックスウィット | quest-data | 3 | script:refine_TCG1 | 104000000 港口 |
| 9201082 | ペティト | none |  | script:naomi1 | 801000000 ショーワ町 |
| 9201083 | グリマー・マン | quest-data | 4 |  | 221000000 地球防衛本部 |
| 9201085 | ジャック | quest-data | 1 |  | 803000300 英雄の谷1
 |
| 9201086 | ジャック | quest-data | 1 |  | 803000304 危殆なる道
 |
| 9201094 | コリーン | none |  | script:TCG3 | 240000000 リプレ |
| 9201095 | フィオナ | quest-data | 2 |  | 803000203 捩れの樹
 |
| 9201096 | ジャック | quest-data | 5 | script:Jack_Crimson | 211000200 雪で覆われた丘 他1 |
| 9201097 | ジョコ | quest-data | 4 |  | 803000103 慙愧の洞窟
 |
| 9201098 | ルーカン
 | quest-data | 5 |  | 803000101 畏れの森
 |
| 9201099 | フォウ
 | none |  | script:MoStore | 803000205 侵食の沼
 |
| 9201100 | タッガーリン
 | quest-data | 2 |  | 803000202 重ねの道
 |
| 9201107 | マスターウォリアー | none |  | script:glpqstatue0 | 803001100 統一の試練
 他3 |
| 9201108 | マスターボウマン | none |  | script:glpqstatue1 | 803001100 統一の試練
 他3 |
| 9201109 | マスターメイジ | none |  | script:glpqstatue2 | 803001100 統一の試練
 他5 |
| 9201110 | マスターシーフ | none |  | script:glpqstatue3 | 803001100 統一の試練
 他3 |
| 9201111 | マスターパイレーツ | none |  | script:glpqstatue4 | 803001100 統一の試練
 他1 |
| 9201112 | ジャック | none |  | script:CrimsonpqEnter | 803000502 クリムゾン砦3

 他1 |
| 9201113 | ジャック | none |  | script:CpqStartL | 803000510 遠征隊(入場) ‐修練の道-

 |
| 9201114 | ジャック | none |  | script:CpqStartH | 803000520 遠征隊(入場) ‐挑戦者の道-

 |
| 9201115 | 戦女神の像 | none |  | script:CrimsonRaid | 803100000 支配者の秘密の間-孤高の戦場-
 |
| 9250000 | ポンラ | quest-data | 4 |  | 500000000 水上市場 |
| 9250001 | アパポン | quest-data | 4 |  | 500010100 ガマの池 |
| 9250003 | キッド | quest-data | 2 |  | 500000000 水上市場 |
| 9250004 | ジート | quest-data | 4 |  | 500020101 山門の入口 |
| 9250005 | ポン | quest-data | 2 |  | 500000000 水上市場 |
| 9250006 | ニード | none |  |  | 500000000 水上市場 |
| 9250007 | タアン | shop |  |  | 500000000 水上市場 |
| 9250008 | パン | shop |  |  | 500000000 水上市場 |
| 9250009 | ノイア | shop |  |  | 500000000 水上市場 |
| 9250010 | プンサク | none |  |  | 500000000 水上市場 |
| 9250011 | ウィンリエ | none |  |  | 500000000 水上市場 |
| 9250012 | チャイ | quest-data | 6 |  | 500000000 水上市場 |
| 9250013 | ルントップ | quest-data | 6 |  | 500000000 水上市場 |
| 9250014 | プヤイリー | quest-data | 6 |  | 500020100 一軒家 |
| 9250016 | ツン | none |  |  | 500000000 水上市場 |
| 9250022 | ブーア | none |  |  | 500000000 水上市場 |
| 9250023 | アクアリウム電光板 | none |  |  | 230000000 アクアリウム |
| 9250024 | エルナス電光板 | none |  |  | 211000000 エルナス |
| 9250026 | ルディブリアム電光板 | none |  |  | 220000000 ルディブリアム |
| 9250027 | コプ | none |  |  | 500000000 水上市場 |
| 9250028 | チャットライ | none |  |  | 500000000 水上市場 |
| 9250029 | タウィ | none |  |  | 500000000 水上市場 |
| 9250042 | ヘネシス電光板 | none |  |  | 100000000 ヘネシス |
| 9250043 | カニングシティー電光板 | none |  |  | 103000000 カニングシティー |
| 9250044 | エリニア電光板 | none |  |  | 101000000 エリニア |
| 9250045 | ぺリオン電光板 | none |  |  | 102000000 ぺリオン |
| 9250046 | オルビス電光板 | none |  |  | 200000000 オルビス |
| 9250072 | Gauss | none |  | script:start_punch | 501030106  |
| 9250073 | Checom | none |  | script:punchTicketEvent | 501030106  |
| 9250076 | Tomic | none |  | script:out_punch | 501030106  |
| 9250120 | 公衆電話 | quest-data | 2 | script:contactCoin_Refresh | 100000000 ヘネシス 他3 |
| 9250121 | ごみ箱おじさん | script | 2 | script:bingCall_Refresh | 100000000 ヘネシス 他3 |
| 9250122 | 研究員 H | quest-data | 1 |  | 502010010 地下道入口 |
| 9250123 | ??? | none |  |  | 100000000 ヘネシス 他3 |
| 9250124 | 未来のドクタービン | quest-data | 12 | script:bingcubeKey_Refresh | 502040000 ドクタービンのキューブ |
| 9250125 | ドクタービン | quest-data | 9 | script:BingMachine_Refresh | 502010030 OSSS秘密基地ドクタービンの部屋 |
| 9250126 | レンダル | quest-data | 10 |  | 502010000 OSSS秘密基地格納庫 |
| 9250127 | OS4シャトル | none |  | script:osssStation_check | 502010000 OSSS秘密基地格納庫 |
| 9250128 | OS4シャトル | none |  | script:return_osssStation | 502010200 墜落した宇宙船の深海 他4 |
| 9250129 | OS4シャトル | none |  | script:gooutside_npcPT | 502010000 OSSS秘密基地格納庫 |
| 9250130 | jinbee82 | quest-data | 2 |  | 502021010 未来のヘネシス外郭 |
| 9250131 | lilycity++ | quest-data | 3 |  | 502023000 エネルギー研究所 |
| 9250132 | クリスティナル | quest-data | 1 | script:visitor_future_crystal | 502023000 エネルギー研究所 |
| 9250133 | 主席研究員エン | quest-data | 11 |  | 502010040 OSSS秘密基地研究室 |
| 9250134 | 責任研究員ベス | quest-data | 5 | script:Visitor_ETCquest | 502010040 OSSS秘密基地研究室 |
| 9250135 | 選任研究員ゼット | none |  |  | 502010040 OSSS秘密基地研究室 |
| 9250136 | ビンポス | none |  | script:visitor_gogocube | 100000000 ヘネシス 他3 |
| 9250137 | ビンポス | none |  | script:visitor_gooutcube | 502029000 墜落した宇宙船入口 |
| 9250138 | ブラックホール生成器 | none |  | script:bingtimetravel_check | 502040000 ドクタービンのキューブ |
| 9250139 | クリスティナル合成機 | none |  |  | 502040000 ドクタービンのキューブ |
| 9250143 | 現場のドクタービン | none |  | script:visitorPT_In | 502029000 墜落した宇宙船入口 |
| 9250144 | タイムマシーン | none |  | script:visitor_timemachine_future | 502010030 OSSS秘密基地ドクタービンの部屋 |
| 9250146 | 現場のエン | quest-data | 3 |  | 502029000 墜落した宇宙船入口 |
| 9250147 | ドクタービン | quest-data | 1 |  | 221000100 本部 |
| 9250150 | 輸送路のレンダル | quest-data | 1 |  | 502010700 輸送路の果て |
| 9250151 | 防御船のレンダル | quest-data | 1 |  | 502010800 安全な防御船 |
| 9250152 | OS3Aマシーン | none |  | script:visitor_guardmap_transfer | 502010010 地下道入口 |
| 9250153 | 公衆電話 | none |  | script:goVisitorStartMap | 502050001  |
| 9250154 | OSSS主席研究員エン | quest-data | 2 | script:Stage0_visitorPT_In | 502029000 墜落した宇宙船入口 |
| 9250155 | OSSS研究員 | none |  | script:Stage0_visitor_gooutcube | 502029000 墜落した宇宙船入口 |
| 9250156 | OSSS研究員 | none |  | script:Stage0_visitor_gogocube | 100000000 ヘネシス 他3 |
| 9270000 | ウェディングタウン電光板 | none |  |  | 680000000 ウェディングタウン |
| 9270001 | リス港口電光板 | none |  |  | 104000000 港口 |
| 9270002 | スリーピーウッド電光板 | none |  |  | 105040300 スリーピーウッド |
| 9270003 | 地球防衛本部電光板 | none |  |  | 221000000 地球防衛本部 |
| 9270004 | 下町電光板 | none |  |  | 222000000 下町 |
| 9270005 | リプレ電光板 | none |  |  | 240000000 リプレ |
| 9270006 | 武陵電光板 | none |  |  | 250000000 武陵 |
| 9270007 | 白草村電光板 | none |  |  | 251000000 白草村 |
| 9270008 | 上海ワイタン電光板 | none |  |  | 701000000 上海ワイタン |
| 9270010 | 台湾西門町電光板 | none |  |  | 740000000 台湾西門町 |
| 9270011 | 夜市場電光板 | none |  |  | 741000000 夜市場 |
| 9270012 | キノコ神社電光板 | none |  |  | 800000000 キノコ神社 |
| 9270015 | 水上市場電光板 | none |  |  | 500000000 水上市場 |
| 9270046 | アリアント町電光板 | none |  |  | 260000000 アリアント |
| 9300012 | ? | none |  |  | 701010400 西州平原1 |
| 9310001 | シップおばさん | shop |  |  | 701000000 上海ワイタン |
| 9310002 | チョー社長 | shop |  |  | 701000000 上海ワイタン |
| 9310003 | リンねえさん | shop |  |  | 701000000 上海ワイタン |
| 9310004 | 婦人警官ポリン | none |  | script:shanghai001 | 701010320 中原山岳地帯2 |
| 9310005 | 警察官ミャオ | quest-data | 2 | script:shanghai002 | 701010321 ブラックシープンの領域 |
| 9310006 | 警察官ミカーファイ | none |  | script:shanghai003 | 701010322 抜け道 |
| 9310007 | 警察官ハーク | none |  | script:shanghai004 | 701010322 抜け道 他2 |
| 9310008 | 警察署長リジュ | quest-data | 5 |  | 701000000 上海ワイタン |
| 9310009 | レオライ | none |  |  | 701010600 西州平原3 |
| 9310010 | テイフ | quest-data | 9 |  | 701010300 西州分かれ道 |
| 9310011 | 屋台メラン | quest-data | 7 |  | 701000000 上海ワイタン |
| 9310012 | シャカメ | quest-data | 4 |  | 701000000 上海ワイタン |
| 9310013 | シャッペリー | script |  | script:goshanghai2 | 701000100 上海飛行場 |
| 9310030 | チェンチェン | none |  |  | 702000000 宋山里 |
| 9310031 | シャオイェズ | script |  | script:hair_shaolin2 | 702000000 宋山里 |
| 9310032 | チゥブラン | script |  | script:hair_shaolin1 | 702000000 宋山里 |
| 9310033 | ジンジュンチャオ | none |  |  | 702000000 宋山里 |
| 9310034 | タァパオ | script |  | script:skin_shaolin2 | 702100000 大雄宝殿 |
| 9310035 | リャオパン | script |  | script:hair_shaolin3 | 702100000 大雄宝殿 |
| 9310036 | ウォンピン | script |  | script:face_shaolin1 | 702000000 宋山里 |
| 9310037 | ルファ | script |  | script:face_shaolin2 | 702000000 宋山里 |
| 9310038 | シャポラン | none |  |  | 702000000 宋山里 |
| 9310039 | 掛け軸 | none |  | script:q8535s | 702070400 蔵経閣7階 |
| 9310040 | 国清 | quest-data | 2 |  | 702030000 不穏山腹 |
| 9310041 | 知的僧侶 | quest-data | 3 |  | 702050000 少林寺広場 |
| 9310042 | 清華居士 | quest-data | 2 |  | 702010000 山麓 |
| 9310043 | プゥズ | script |  | script:skin_shaolin1 | 702000000 宋山里 |
| 9310044 | 掛け軸 | none |  | script:outshaolinBoss | 702060000 修行の間 |
| 9310045 | Mureung Maple TV | none |  |  | 702000000 宋山里 |
| 9310046 | 僧観 | quest-data | 2 |  | 702070200 蔵経閣3,4階 |
| 9310047 | 澄心 | quest-data | 2 |  | 702070300 蔵経閣5,6階 |
| 9310048 | 掃除するお坊さん | quest-data | 4 |  | 702070100 蔵経閣1,2階 |
| 9310049 | ジョン長老 | quest-data | 2 |  | 702000000 宋山里 |
| 9310050 | ノマ | script |  |  | 702000000 宋山里 |
| 9310051 | 海峯法師 | quest-data | 2 |  | 702100000 大雄宝殿 |
| 9310052 | 胡竹 | quest-data | 2 |  | 702100000 大雄宝殿 |
| 9310053 | 賢者方丈 | quest-data | 1 |  | 702100000 大雄宝殿 |
| 9330000 | ラン | none |  |  | 740000000 台湾西門町 |
| 9330001 | ウエッポー | none |  |  | 740000000 台湾西門町 |
| 9330002 | ショカツ | none |  |  | 740000000 台湾西門町 |
| 9330003 | パーピン | quest-data | 8 |  | 740010200 西門町街3 |
| 9330004 | ニナリー | quest-data | 4 |  | 740000000 台湾西門町 |
| 9330005 | アンニン | quest-data | 4 |  | 740000000 台湾西門町 |
| 9330006 | イエリッキー | quest-data | 2 |  | 740000000 台湾西門町 |
| 9330014 | テツウン | script |  |  | 740000100 西門町電車駅 |
| 9330016 | 料理人アゾン | quest-data | 9 |  | 741000000 夜市場 |
| 9330017 | ガム売りの少女 | quest-data | 3 |  | 741000000 夜市場 |
| 9330018 | コロップ | quest-data | 4 |  | 741010300 夜市場街4 |
| 9330019 | ジュディ | quest-data | 2 |  | 741000000 夜市場 |
| 9330020 | クリスティン | quest-data | 2 |  | 741000000 夜市場 |
| 9330021 | ニキ | script |  | script:hair_taiwan1 | 741000000 夜市場 |
| 9330022 | ジュリ | script |  | script:hair_taiwan2 | 741000000 夜市場 |
| 9330023 | 院長ミワック | script |  | script:face_taiwan1 | 741000001 夜市場病院内部 |
| 9330024 | 助手クワン | script |  | script:face_taiwan2 | 741000001 夜市場病院内部 |
| 9330025 | カララン | script |  | script:skin_taiwan1 | 741000001 夜市場病院内部 |
| 9330026 | ルーロー | none |  |  | 741000000 夜市場 |
| 9330027 | ショエル | quest-data | 2 |  | 741000000 夜市場 |
| 9330028 | メロノン | quest-data | 2 | script:nightmarket01 | 741020100 夜市場分かれ道2 |
| 9330029 | ユユ | none |  |  | 741000000 夜市場 |
| 9330030 | 不良青年モヒゴル | none |  |  | 741000000 夜市場 |
| 9330031 | フルーツジュース屋スイスイ | none |  |  | 701000100 上海飛行場 |
| 9330032 | 果物屋トレホレ | none |  | script:nightmarket02 | 741020101 夜市場裏道1 他1 |
| 9330045 | 釣り場管理人 | quest-data | 4 | script:fishing | 100000000 ヘネシス 他12 |
| 9330046 | 釣り爺 | none |  | script:fishing | 741000200 釣り場 他8 |
| 9330073 | 海産物店チョム | none |  | script:q8704s | 741000000 夜市場 |
| 9330093 | ビッキィ＆ケッキー | none |  | script:enter4thEvent | 100000000 ヘネシス 他2 |
| 9330094 | パティ | none |  | script:PinkBeenEventPQ | 749050000 お菓子の部屋(入口)


 |
| 9330097 | ド | none |  | script:cakeEventHarp1 | 749050100 お菓子の部屋

 他9 |
| 9330098 | レ | none |  | script:cakeEventHarp2 | 749050100 お菓子の部屋

 他9 |
| 9330099 | ミ | none |  | script:cakeEventHarp3 | 749050100 お菓子の部屋

 他9 |
| 9330100 | ファ | none |  | script:cakeEventHarp4 | 749050100 お菓子の部屋

 他9 |
| 9330101 | ソ | none |  | script:cakeEventHarp5 | 749050100 お菓子の部屋

 他9 |
| 9330102 | ラ | none |  | script:cakeEventHarp6 | 749050100 お菓子の部屋

 他9 |
| 9330103 | シ | none |  | script:cakeEventHarp7 | 749050100 お菓子の部屋

 他9 |
| 9330104 | ピンクビーン | none |  | script:4thEventFinalStage | 749050100 お菓子の部屋

 他9 |
| 9330105 | パティ | none |  | script:PinkBeenEventReward | 749050200 お菓子の部屋(出口)


 |
| 9330106 | ショコラ | none |  | script:GuideMap | 749050100 お菓子の部屋

 他9 |
| 9900000 | KIN | quest-data | 4 | script:levelUP | 180000000 撮影現場 |
| 9900001 | NimaKIN | none |  | script:levelUP2 | 180000000 撮影現場 |
| 9901000 | ? | none |  | script:rank_user | 102000003 戦士の聖殿 |
| 9901001 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901002 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901003 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901004 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901005 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901006 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901007 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901008 | ? | none |  | script:rank_user | 102000004 戦士の殿堂 |
| 9901100 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901101 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901102 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901103 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901104 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901105 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901106 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901107 | ? | none |  | script:rank_user | 101000004 魔法使いの殿堂 |
| 9901200 | ? | none |  | script:rank_user | 100000204 弓使いの殿堂 |
| 9901300 | ? | none |  | script:rank_user | 103000008 盗賊の殿堂 |
| 9901301 | ? | none |  | script:rank_user | 103000008 盗賊の殿堂 |
| 9901500 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901501 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901502 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901503 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901504 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901505 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901506 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901507 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901508 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901509 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901510 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901511 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901512 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901513 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901514 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901515 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901516 | ? | none |  | script:rank_user | 130000100 騎士の殿堂 |
| 9901517 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901518 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901519 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901520 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901521 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901522 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901523 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901524 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901525 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901526 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901527 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901528 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901529 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901530 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901531 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901532 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901533 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901534 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901535 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901536 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901537 | ? | none |  | script:rank_user | 130000110 騎士の殿堂2階 |
| 9901538 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901539 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901540 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901541 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901542 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901543 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901544 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901545 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901546 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901547 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901548 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901549 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901550 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901551 | ? | none |  | script:rank_user | 130000120 騎士の殿堂3階 |
| 9901600 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901601 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901602 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901603 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901604 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901605 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901606 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901607 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901608 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901609 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901610 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901611 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901612 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901613 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901614 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901615 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901616 | ? | none |  | script:rank_user | 140010110 英雄の殿堂 |
| 9901700 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901701 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901702 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901703 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901704 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901705 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901706 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901707 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901708 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901709 | ? | none |  | script:rank_user | 102000005 戦士の殿堂 |
| 9901710 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901711 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901712 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901713 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901714 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901715 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901716 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901717 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901718 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901719 | ? | none |  | script:rank_user | 101000005 魔法使いの殿堂 |
| 9901720 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901721 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901722 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901723 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901724 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901725 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901726 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901727 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901728 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901729 | ? | none |  | script:rank_user | 100000205 弓使いの殿堂 |
| 9901730 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901731 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901732 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901733 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901734 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901735 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901736 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901737 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901738 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901739 | ? | none |  | script:rank_user | 103000009 盗賊の殿堂 |
| 9901740 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901741 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901742 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901743 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901744 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901745 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901746 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901747 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901748 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901749 | ? | none |  | script:rank_user | 120000105 訓練場 |
| 9901800 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901801 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901802 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901803 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901804 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901805 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901806 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901807 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901808 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901809 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901810 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901811 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901812 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901813 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901814 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901815 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901816 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901817 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901818 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901819 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901820 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901821 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901822 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901823 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901824 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901825 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901826 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901827 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901828 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901829 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901830 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901831 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901832 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901833 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901834 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901835 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901836 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901837 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901838 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901839 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901840 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901841 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901842 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901843 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901844 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901845 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901846 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901847 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901848 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901849 | ? | none |  | script:rank_user | 130000101 騎士の殿堂 |
| 9901900 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901901 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901902 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901903 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901904 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901905 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901906 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901907 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901908 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901909 | ? | none |  | script:rank_user | 140010111 英雄の殿堂 |
| 9901910 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901911 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901912 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901913 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901914 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901915 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901916 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901917 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901918 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
| 9901919 | ? | none |  | script:rank_user | 100030301 鬱蒼とした森 |
