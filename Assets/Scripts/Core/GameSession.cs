using System;
using System.Linq;

namespace JellyField.Core
{
    public class GameSession
    {
        public readonly LevelDefinition Level;
        public Board Board;
        public Piece[] Tray;
        public int[] Remaining;
        public int Cursor;
        public bool Won => Remaining.All(v => v == 0);
        public bool Lost => !Won && !Board.HasSpace;

        public GameSession(LevelDefinition level)
        {
            Level = level;
            Board = level.CreateBoard();
            Remaining = (int[])level.Goals.Clone();
            Tray = new Piece[level.TraySize];
            for (int i = 0; i < Tray.Length; i++)
                Refill(i);
        }

        public GameSession Clone()
        {
            return new GameSession(Level)
            {
                Board = Board.Clone(),
                Tray = Tray.Select(p => p?.Clone()).ToArray(),
                Remaining = (int[])Remaining.Clone(),
                Cursor = Cursor
            };
        }

        public bool Place(int slot, int cell)
        {
            if (Won || Lost || slot < 0 || slot >= Tray.Length || !Board.TryPlace(cell, Tray[slot]))
                return false;
            Tray[slot] = null;
            return true;
        }

        public void Score(ClearWave wave)
        {
            for (int i = 1; i < Remaining.Length; i++)
                Remaining[i] = Math.Max(0, Remaining[i] - wave.Counts[i]);
        }

        public void Refill(int slot)
        {
            Tray[slot] = Level.Sequence[Cursor].Clone();
            Cursor = (Cursor + 1) % Level.Sequence.Length;
        }

        public bool Play(int slot, int cell)
        {
            if (!Place(slot, cell))
                return false;
            while (true)
            {
                var wave = Board.FindMatches();
                if (wave.Total == 0)
                    break;
                Board.Apply(wave);
                Score(wave);
            }

            Refill(slot);
            return true;
        }
    }
}
