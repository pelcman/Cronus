// ChannelHandler partial: the command catalogue that drives /help and the argument-error replies.
namespace Cronus.Server.Channel;

/// <summary>
/// One command's metadata. A single table describes every command so that <c>/help</c>, the
/// per-command detail view, and the "you typed the arguments wrong" reply all stay in sync with
/// the dispatcher automatically — adding a case without adding an entry here shows up as a missing
/// help line rather than as silently undocumented behaviour.
/// </summary>
internal sealed record CommandSpec(
    string Name,
    string Usage,
    string Summary,
    string Category,
    params string[] Aliases);

/// <summary>The command catalogue. Ordered: categories in menu order, commands within them.</summary>
internal static class CommandTable
{
    public const string CategoryMove = "移動";
    public const string CategoryCharacter = "キャラクター";
    public const string CategoryItem = "アイテム";
    public const string CategoryWorld = "ワールド";
    public const string CategorySystem = "システム";

    /// <summary>The stat fields <c>/status</c> accepts, in help order. Each doubles as a
    /// (hidden) top-level alias so the pre-consolidation spelling — <c>/hp 500</c> — still works.</summary>
    public static readonly string[] StatFields =
    {
        "level", "job", "exp", "hp", "maxhp", "mp", "maxmp",
        "str", "dex", "int", "luk", "ap", "sp", "fame", "meso",
    };

    public static readonly IReadOnlyList<CommandSpec> All = new CommandSpec[]
    {
        new("help", "/help [コマンド名]", "コマンド一覧、または 1 つのコマンドの詳しい使い方を表示します。", CategorySystem),

        new("warp", "/warp <マップID|プレイヤー名>", "マップIDを指定するとそのマップへ、名前を指定するとそのプレイヤーのいるマップへ移動します。", CategoryMove, "map"),
        new("dbgwarp", "/dbgwarp", "地域 → マップ を選んでワープするウィンドウを開きます（IDを覚える必要はありません）。", CategoryMove),
        new("pos", "/pos", "現在の座標とマップIDを表示します。", CategoryMove),

        new("status", "/status [項目 値]", "ステータスを表示 / 変更します。項目: " + "level job exp hp maxhp mp maxmp str dex int luk ap sp fame meso"
            + "。ap と sp は加算、その他は指定値になります。", CategoryCharacter),
        new("heal", "/heal", "HP と MP を全回復します。", CategoryCharacter),
        new("maxskills", "/maxskills", "現在の職業で覚えられるスキルをすべて最大レベルにします。", CategoryCharacter),
        new("gender", "/gender [m|f]", "性別を切り替えます（引数なしでトグル）。見た目を反映するため同じチャンネルへ入り直します。", CategoryCharacter),
        new("beauty", "/beauty", "髪型・髪色・顔・目の色・肌の色を選ぶ美容室ウィンドウを開きます。", CategoryCharacter),

        new("item", "/item <アイテムID> [個数]", "アイテムをインベントリに追加します。", CategoryItem),
        new("drop", "/drop <アイテムID|0> [個数]", "足元にドロップを湧かせます。ID に 0 を指定するとメル塊になります。", CategoryItem),
        new("shop", "/shop [ショップID]", "ショップを開きます。引数なしで全アイテム 1 メルのデバッグショップを開きます。", CategoryItem, "dbgshop"),
        new("storage", "/storage", "倉庫を開きます。", CategoryItem),
        new("clear", "/clear <inv [タブ]|quest <クエストID>|book>", "インベントリ / クエスト記録 / モンスターブックを消去します。", CategoryItem, "clearinv", "questreset"),

        new("notice", "/notice [all] <メッセージ>", "現在のマップに告知を流します。all を付けると全チャンネル全マップに流します。", CategoryWorld, "snotice"),
        new("players", "/players", "オンラインのプレイヤー名を一覧表示します。", CategoryWorld, "online"),
        new("guildcreate", "/guildcreate <ギルド名>", "ギルドを無料で作成します（本来必要な本部マップと 500 万メルを省略）。", CategoryWorld),

        new("save", "/save", "キャラクターを即座に保存します。", CategorySystem),
    };

    private static readonly Dictionary<string, CommandSpec> ByName = BuildIndex();

    private static Dictionary<string, CommandSpec> BuildIndex()
    {
        var index = new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (CommandSpec spec in All)
        {
            index[spec.Name] = spec;
            foreach (string alias in spec.Aliases)
            {
                index[alias] = spec;
            }
        }

        // Every stat field is also a legacy top-level alias of /status (e.g. /hp 500).
        CommandSpec status = index["status"];
        foreach (string field in StatFields)
        {
            index[field] = status;
        }

        return index;
    }

    /// <summary>Resolves a typed name (canonical, alias, or stat field) to its command.</summary>
    public static bool TryGet(string name, out CommandSpec spec) => ByName.TryGetValue(name, out spec!);

    /// <summary>Whether a typed name is a stat field usable as <c>/status &lt;field&gt;</c>.</summary>
    public static bool IsStatField(string name) => Array.Exists(StatFields, f => f.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The <c>/help</c> listing: a header, then one indented line per command under its
    /// category. One chat line per entry — the client renders each as its own row.</summary>
    public static IEnumerable<string> HelpLines()
    {
        yield return "──── Cronus コマンド一覧 ────";
        string? category = null;
        foreach (CommandSpec spec in All.OrderBy(s => CategoryOrder(s.Category)))
        {
            if (spec.Category != category)
            {
                category = spec.Category;
                yield return $"【{category}】";
            }

            yield return "  " + spec.Usage;
        }

        yield return "詳しい使い方: /help <コマンド名>";
    }

    /// <summary>The <c>/help &lt;command&gt;</c> detail: usage, what it does, and any aliases.</summary>
    public static IEnumerable<string> DetailLines(CommandSpec spec)
    {
        yield return $"【{spec.Category}】{spec.Usage}";
        yield return "  " + spec.Summary;
        var aliases = new List<string>(spec.Aliases);
        if (spec.Name == "status")
        {
            aliases.AddRange(StatFields);
        }

        if (aliases.Count > 0)
        {
            yield return "  別名: " + string.Join(" ", aliases.Select(a => "/" + a));
        }
    }

    /// <summary>The closest known command name to something the player mistyped, if any is close
    /// enough to be worth suggesting (prefix match first, then a one-edit-ish distance).</summary>
    public static string? Suggest(string typed)
    {
        string best = string.Empty;
        int bestScore = int.MaxValue;
        foreach (string name in ByName.Keys)
        {
            if (name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
                || typed.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            int score = Distance(typed.ToLowerInvariant(), name);
            if (score < bestScore)
            {
                bestScore = score;
                best = name;
            }
        }

        return bestScore <= Math.Max(1, typed.Length / 3) ? best : null;
    }

    private static int CategoryOrder(string category) => category switch
    {
        CategoryMove => 0,
        CategoryCharacter => 1,
        CategoryItem => 2,
        CategoryWorld => 3,
        _ => 4,
    };

    /// <summary>Levenshtein distance, used only to rank "did you mean" candidates.</summary>
    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
