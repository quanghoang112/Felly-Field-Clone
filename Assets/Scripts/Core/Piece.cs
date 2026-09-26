using System;
using System.Collections.Generic;
using System.Linq;

namespace JellyField.Core
{
    // Four quadrants in reading order: top-left, top-right, bottom-left, bottom-right.
    // Repeated colors describe one elastic region, not additional scoring units.
    [Serializable]
    public class Piece
    {
        public JellyColor[] Cells;
        public Piece(params JellyColor[] cells)
        {
            if (cells.Length != 4)
                throw new ArgumentException("A piece needs four quadrants.");
            Cells = (JellyColor[])cells.Clone();
        }

        public Piece Clone()
        {
            return new Piece(Cells);
        }

        public bool Empty => Cells.All(c => c == JellyColor.None);

        public void RemoveAndExpand(HashSet<JellyColor> colors)
        {
            for (int i = 0; i < 4; i++)
                if (colors.Contains(Cells[i]))
                    Cells[i] = JellyColor.None;
            if (Empty)
                return;
            //fill màu vào các ô trống bằng màu của ô đối diện cùng hàng
            var before = (JellyColor[])Cells.Clone();
            for (int i = 0; i < 4; i++)
                if (Cells[i] == JellyColor.None)
                    Cells[i] = before[i ^ 1];
            //fill màu vào các ô trống bằng màu của ô đối diện cùng cột
            before = (JellyColor[])Cells.Clone();
            for (int i = 0; i < 4; i++)
                if (Cells[i] == JellyColor.None)
                    Cells[i] = before[i ^ 2];
        }

        public string Key => string.Concat(Cells.Select(c => ((int)c).ToString()));
    }
}
