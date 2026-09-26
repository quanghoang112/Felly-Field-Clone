using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JellyField.Core;

namespace JellyField.Editor
{
    public static class LevelSearch
    {
        private class Node
        {
            public GameSession game;
            public List<int[]> path;
        }

        public static string Find(LevelDefinition level)
        {
            var timer = Stopwatch.StartNew();
            var frontier = new List<Node>
            {
                new Node
                {
                    game = new GameSession(level),
                    path = new List<int[]>()
                }
            };
            var seen = new HashSet<string>();
            int expanded = 0;
            for (int depth = 0; depth < 30; depth++)
            {
                var next = new List<Node>();
                foreach (var node in frontier)
                    for (int slot = 0; slot < node.game.Tray.Length; slot++)
                        for (int cell = 0; cell < node.game.Board.Pieces.Length; cell++)
                        {
                            if (timer.Elapsed.TotalSeconds > 5 || expanded > 150000)
                                return "Chưa tìm thấy lời giải trong giới hạn 5 giây / 150.000 trạng thái. Hãy chơi thử; kết quả này không chứng minh màn vô nghiệm.";
                            if (node.game.Tray[slot] == null || !node.game.Board.CanPlace(cell))
                                continue;
                            var game = node.game.Clone();
                            game.Play(slot, cell);
                            expanded++;
                            var path = new List<int[]>(node.path)
                            {
                                new[]
                                {
                                    slot,
                                    cell
                                }
                            };
                            if (game.Won)
                                return "Tìm thấy cách thắng trong " + path.Count + " lượt (không đảm bảo ít lượt nhất).\n" + string.Join("\n", path.Select((move, i) => (i + 1) + ". Khay " + (move[0] + 1) + " → hàng " + (move[1] / level.Width + 1) + ", cột " + (move[1] % level.Width + 1)));
                            if (game.Lost)
                                continue;
                            string key = game.Cursor + ":" + string.Join(",", game.Remaining) + ":" + string.Join("/", game.Tray.Select(p => p?.Key ?? "-")) + ":" + string.Join("/", game.Board.Pieces.Select(p => p?.Key ?? "-"));
                            if (!seen.Add(key))
                                continue;
                            next.Add(new Node { game = game, path = path });
                        }

                frontier = next.OrderBy(n => n.game.Remaining.Sum() * 100 + n.game.Board.Pieces.Count(p => p != null) * 3).Take(600).ToList();
                if (frontier.Count == 0)
                    break;
            }

            return "Chưa tìm thấy lời giải với tìm kiếm giới hạn 30 lượt, giữ 600 nhánh mỗi bước. Hãy điều chỉnh hoặc chơi thử; đây không phải kết luận vô nghiệm.";
        }
    }
}
