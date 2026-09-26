using System;
using System.Collections.Generic;
using System.Linq;
using JellyField.Core;
using UnityEngine;

namespace JellyField.Data
{
    [Serializable]
    public class JellyLayout
    {
        public JellyColor[] colors =
        {
            JellyColor.Green,
            JellyColor.Green,
            JellyColor.Green,
            JellyColor.Green
        };
        public Piece ToPiece()
        {
            return new Piece(colors);
        }

        public bool IsValid()
        {
            if (colors == null || colors.Length != 4 || colors.Any(c => c < JellyColor.Green || c > JellyColor.Yellow))
                return false;
            foreach (var color in colors.Distinct())
            {
                var corners = Enumerable.Range(0, 4).Where(i => colors[i] == color).ToArray();
                int width = corners.Max(i => i % 2) - corners.Min(i => i % 2) + 1;
                int height = corners.Max(i => i / 2) - corners.Min(i => i / 2) + 1;
                if (width * height != corners.Length)
                    return false;
            }

            return true;
        }
    }

    [Serializable]
    public class LevelCell
    {
        public bool enabled = true;
        public bool hasJelly;
        public JellyLayout jelly = new JellyLayout();
    }

    [CreateAssetMenu(fileName = "New Level", menuName = "Jelly Field/Level")]
    public class LevelAsset : ScriptableObject
    {
        public string levelName = "New level";
        public int width = 3, height = 3, traySize = 1;
        public LevelCell[] cells = Enumerable.Range(0, 9).Select(_ => new LevelCell()).ToArray();
        public List<JellyLayout> sequence = new List<JellyLayout>
        {
            new JellyLayout(),
            new JellyLayout(),
            new JellyLayout(),
            new JellyLayout()
        };
        public int[] goals =
        {
            0,
            4,
            0,
            0,
            0,
            0
        };
        public List<string> Errors(bool checkSequence = true)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(levelName))
                errors.Add("Nhập tên màn chơi.");
            if (width < 1 || width > 6 || height != width)
                errors.Add("Khung lưới phải là hình vuông, kích thước từ 1×1 đến 6×6.");
            if (traySize < 1 || traySize > 2)
                errors.Add("Khay phải có 1 hoặc 2 vị trí.");
            if (cells == null || cells.Length != width * height || cells.Any(c => c == null))
                errors.Add("Kích thước dữ liệu bàn không khớp. Hãy áp dụng lại kích thước.");
            else
            {
                if (!cells.Any(c => c.enabled && !c.hasJelly))
                    errors.Add("Cần ít nhất một ô trống để đặt jelly.");
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i].enabled && cells[i].hasJelly && (cells[i].jelly == null || !cells[i].jelly.IsValid()))
                        errors.Add("Jelly tại hàng " + (i / width + 1) + ", cột " + (i % width + 1) + ": mỗi màu phải tạo hình chữ nhật, không chéo hoặc chữ L.");
            }

            if (checkSequence && (sequence == null || sequence.Count == 0))
                errors.Add("Chuỗi jelly cần ít nhất một Piece; chuỗi sẽ lặp lại để bổ sung khay.");
            else if (checkSequence)
                for (int i = 0; i < sequence.Count; i++)
                    if (sequence[i] == null || !sequence[i].IsValid())
                        errors.Add("Jelly thứ " + (i + 1) + " trong chuỗi có vùng màu không hợp lệ.");
            if (goals == null || goals.Length != 6 || goals[0] != 0 || goals.Any(g => g < 0) || goals.Count(g => g > 0) < 1 || goals.Count(g => g > 0) > 2)
                errors.Add("Đặt mục tiêu dương cho 1–2 màu; các màu còn lại đặt 0.");
            return errors;
        }

        public LevelDefinition ToDefinition()
        {
            var errors = Errors();
            if (errors.Count > 0)
                throw new InvalidOperationException(name + ": " + string.Join("\n", errors));
            var level = new LevelDefinition
            {
                Name = levelName,
                Width = width,
                Height = height,
                TraySize = traySize,
                Mask = cells.Select(c => c.enabled).ToArray(),
                Goals = (int[])goals.Clone(),
                Sequence = sequence.Select(p => p.ToPiece()).ToArray()
            };
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].enabled && cells[i].hasJelly)
                    level.Initial.Add(i, cells[i].jelly.ToPiece());
            return level;
        }
    }
}
