using System.Collections.Generic;

namespace JellyField.Core
{
    public class LevelDefinition
    {
        public string Name;
        public int Width, Height, TraySize;
        public bool[] Mask;
        public readonly Dictionary<int, Piece> Initial = new Dictionary<int, Piece>();
        public Piece[] Sequence;
        public int[] Goals = new int[6];
        public Board CreateBoard()
        {
            var board = new Board(Width, Height, Mask);
            foreach (var pair in Initial)
                board.TryPlace(pair.Key, pair.Value);
            return board;
        }
    }
}
