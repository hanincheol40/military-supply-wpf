using MilitarySupply.Models;
using MilitarySupply.Services;

namespace MilitarySupply.ViewModels
{
    // 몸의 장착 위치 하나 (헬멧, 방탄복, 전투화 ...)
    public sealed class EquipmentSlotViewModel(string key) : Observable
    {
        private const string EmptyText = "장비를 끌어 놓으세요";

        public string Key { get; } = key;
        public string Label => EquipmentCatalog.Label(Key);

        public StockItem? Draft { get; private set; }   // 끌어 놓았지만 아직 확정 전
        public Issue? Active { get; private set; }      // 확정됐거나 지급 완료된 것
        public string ItemName { get; private set; } = EmptyText;
        public bool HasDraft => Draft != null;

        // 구성 중 → 확정(인도 대기) → 지급 완료 순서로 점점 진하게 보인다
        public string State =>
            Draft != null ? "구성 중"
            : Active == null ? "미장착"
            : Active.AwaitingDelivery ? "지급 확정 · 인도 대기"
            : "지급 완료 · 장착";

        public double Opacity =>
            Draft != null ? .30
            : Active == null ? .10
            : Active.AwaitingDelivery ? .48
            : 1;

        public string StateColor =>
            Draft != null ? "#7A4D00"
            : Active == null ? "#464C53"
            : Active.AwaitingDelivery ? "#7A4D00"
            : "#1A662B";

        private string serial = "";
        public string Serial
        {
            get => serial;
            set => Set(ref serial, value);
        }

        public void Update(StockItem? draft, Issue? active, string? activeName)
        {
            if (Draft?.Id != draft?.Id)
                Serial = "";

            Draft = draft;
            Active = active;
            ItemName = draft?.Name ?? activeName ?? EmptyText;

            foreach (var name in new[] { nameof(Draft), nameof(Active), nameof(ItemName), nameof(State), nameof(Opacity), nameof(StateColor), nameof(HasDraft) })
                Changed(name);
        }
    }
}
