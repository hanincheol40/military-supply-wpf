using MilitarySupply.Models;

namespace MilitarySupply.Services
{
    // 장비가 몸의 어느 위치에 들어가는지 정하는 표
    public static class EquipmentCatalog
    {
        public static readonly string[] Slots =
            ["Head", "Armor", "Vest", "Pack", "Boots", "Weapon", "Uniform", "Pouch"];

        public static string Label(string slot) => slot switch
        {
            "Head" => "머리 · 헬멧",
            "Armor" => "몸통 · 방탄복",
            "Vest" => "가슴 · 전투조끼",
            "Pack" => "등 · 군장",
            "Boots" => "발 · 전투화",
            "Weapon" => "총기",
            "Uniform" => "전투복",
            _ => "허리 · 탄알집"
        };

        // 품목 이름(무기는 구분)으로 장착 위치를 찾는다. 맞는 곳이 없으면 null
        public static string? SlotFor(StockItem item)
        {
            var n = item.Name;

            if (n.Contains("헬멧") || n.Contains("모자") || n.Contains("철모")) return "Head";
            if (n.Contains("방탄복")) return "Armor";
            if (n.Contains("조끼")) return "Vest";
            if (n.Contains("군장") || n.Contains("배낭")) return "Pack";
            if (n.Contains("전투화") || n.Contains("군화")) return "Boots";
            if (item.Category == "무기" || n.StartsWith("K2", StringComparison.OrdinalIgnoreCase) || n == "K1A") return "Weapon";
            if (n.Contains("전투복") || n.Contains("군복")) return "Uniform";
            if (n.Contains("탄알집") || n.Contains("파우치")) return "Pouch";
            return null;
        }
    }
}
