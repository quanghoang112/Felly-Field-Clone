using System.Collections.Generic;

namespace JellyField.Core
{
    public class Board
    {
        public readonly int Width, Height;
        public readonly bool[] Mask;
        public readonly Piece[] Pieces;
        public Board(int width, int height, bool[] mask)
        {
            Width = width;
            Height = height;
            Mask = (bool[])mask.Clone();
            Pieces = new Piece[mask.Length];
        }

        public Board Clone()
        {
            var copy = new Board(Width, Height, Mask);
            for (int i = 0; i < Pieces.Length; i++)
                copy.Pieces[i] = Pieces[i]?.Clone();
            return copy;
        }

        public bool CanPlace(int index)
        {
            return index >= 0 && index < Mask.Length && Mask[index] && Pieces[index] == null;
        }

        public bool HasSpace
        {
            get
            {
                for (int i = 0; i < Mask.Length; i++)
                    if (CanPlace(i))
                        return true;
                return false;
            }
        }

        public bool TryPlace(int index, Piece piece)
        {
            if (!CanPlace(index) || piece == null || piece.Empty)
                return false;
            Pieces[index] = piece.Clone();
            return true;
        }

        public ClearWave FindMatches(ISet<int> excluded = null)
        {
            var wave = new ClearWave();
            for (int i = 0; i < Pieces.Length; i++)
            {
                if (Pieces[i] == null || (excluded != null && excluded.Contains(i)))
                    continue;
                if (i % Width + 1 < Width && (excluded == null || !excluded.Contains(i + 1)))
                {
                    Match(wave, i, 1, i + 1, 0);
                    Match(wave, i, 3, i + 1, 2);
                }

                if (i / Width + 1 < Height && (excluded == null || !excluded.Contains(i + Width)))
                {
                    Match(wave, i, 2, i + Width, 0);
                    Match(wave, i, 3, i + Width, 1);
                }
            }

            return wave;
        }

        public ClearWave PreviewPlacement(int index, Piece piece, ISet<int> excluded = null)
        {
            var preview = Clone();
            if (!preview.TryPlace(index, piece))
                return new ClearWave();
            // Preview only the first clear, before any colors expand.
            return preview.FindMatches(excluded);
        }

        private void Match(ClearWave wave, int a, int qa, int b, int qb)
        {
            if (Pieces[b] == null)
                return;
            var color = Pieces[a].Cells[qa];
            if (color == JellyColor.None || color != Pieces[b].Cells[qb])
                return;
            wave.Add(a, color);
            wave.Add(b, color);
        }

        public void Apply(ClearWave wave)
        {
            foreach (var pair in wave.Removed)
            {
                Pieces[pair.Key].RemoveAndExpand(pair.Value);
                if (Pieces[pair.Key].Empty)
                    Pieces[pair.Key] = null;
            }
        }

        public int[] Resolve()
        {
            var counts = new int[6];
            while (true)
            {
                var wave = FindMatches();
                if (wave.Total == 0)
                    return counts;
                Apply(wave);
                for (int i = 1; i < counts.Length; i++)
                    counts[i] += wave.Counts[i];
            }
        }
    }
}
