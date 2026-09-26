using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JellyField.Core;

namespace JellyField.Editor
{
    public static class TraySequenceGenerator
    {
        public class Result
        {
            public Piece[] Sequence;
            public int[] Cells;
        }

        private class Node
        {
            public Board board;
            public int[] remaining;
            public Node parent;
            public Piece piece;
            public int cell;
            public int Score => remaining.Sum() * 100 + board.Pieces.Count(p => p != null) * 3;
        }

        public static Result Generate(LevelDefinition level)
        {
            var timer = Stopwatch.StartNew();
            var candidates = CreatePieces();
            var frontier = new List<Node> { new Node { board = level.CreateBoard(), remaining = (int[])level.Goals.Clone() } };
            var seen = new HashSet<string>();
            for (int depth = 0; depth < 60; depth++)
            {
                var next = new List<Node>();
                foreach (var node in frontier)
                    for (int cell = 0; cell < node.board.Pieces.Length; cell++)
                    {
                        if (!node.board.CanPlace(cell))
                            continue;
                        foreach (var piece in candidates)
                        {
                            if (timer.Elapsed.TotalSeconds > 5)
                                return null;
                            var board = node.board.Clone();
                            board.TryPlace(cell, piece);
                            int[] cleared = board.Resolve();
                            var remaining = new int[6];
                            for (int color = 1; color <= 5; color++)
                                remaining[color] = Math.Max(0, node.remaining[color] - cleared[color]);
                            var child = new Node { board = board, remaining = remaining, parent = node, piece = piece, cell = cell };
                            if (remaining.All(count => count == 0))
                                return Verify(level, child);
                            if (!board.HasSpace)
                                continue;
                            string key = string.Join(",", remaining) + ":" + string.Join("/", board.Pieces.Select(p => p?.Key ?? "-"));
                            if (seen.Add(key))
                                next.Add(child);
                        }
                    }
                frontier = next.OrderBy(node => node.Score).Take(24).ToList();
                if (frontier.Count == 0)
                    return null;
            }
            return null;
        }

        private static List<Piece> CreatePieces()
        {
            var pieces = new List<Piece>();
            for (int a = 1; a <= 5; a++)
                for (int b = 1; b <= 5; b++)
                {
                    var first = (JellyColor)a;
                    var second = (JellyColor)b;
                    pieces.Add(new Piece(first, first, second, second));
                    if (a != b)
                        pieces.Add(new Piece(first, second, first, second));
                }
            var random = new Random();
            for (int i = pieces.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var piece = pieces[i];
                pieces[i] = pieces[j];
                pieces[j] = piece;
            }
            return pieces;
        }

        private static Result Verify(LevelDefinition level, Node winner)
        {
            var path = new List<Node>();
            for (var node = winner; node.parent != null; node = node.parent)
                path.Add(node);
            path.Reverse();
            var sequence = path.Select(node => node.piece.Clone()).ToList();
            // The verified route uses slot 0; other slots keep their initial piece.
            for (int slot = 1; slot < level.TraySize; slot++)
                sequence.Insert(slot, path[0].piece.Clone());
            var testLevel = new LevelDefinition
            {
                Name = level.Name, Width = level.Width, Height = level.Height,
                TraySize = level.TraySize, Mask = (bool[])level.Mask.Clone(),
                Goals = (int[])level.Goals.Clone(), Sequence = sequence.ToArray()
            };
            foreach (var pair in level.Initial)
                testLevel.Initial.Add(pair.Key, pair.Value.Clone());
            var game = new GameSession(testLevel);
            foreach (var node in path)
                if (!game.Play(0, node.cell))
                    return null;
            return game.Won ? new Result { Sequence = testLevel.Sequence, Cells = path.Select(node => node.cell).ToArray() } : null;
        }
    }
}
