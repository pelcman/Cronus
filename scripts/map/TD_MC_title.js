// キノコ城 / キノコの森町角 106020000 の onUserEnter (JMSv186 MapScriptMethods の TD_MC_title): 入場のたびにテーマダンジョンの
// タイトル演出 FieldEffect_Screen "temaD/enter/mushCatle" を出す。JMS は直前に SetStandAloneMode/SetDirectionMode の解除も送るが、
// それはオープニング演出マップ 106020001 (TD_MC_Openning) から戻る場合の後始末で、Cronus はまだそこを経由しないので送らない。
function start() {
    player.showScreenEffect("temaD/enter/mushCatle");
}
