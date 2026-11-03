# Experience System Guide

## Luồng runtime

1. `EnemyExperienceDropper` nhận sự kiện chết trước khi enemy được trả về pool.
2. `ExperiencePickupService` roll loại drop và lấy pickup từ pool.
3. Pickup tồn tại vô hạn trên map. Khi vào `EXP Pickup Range`, service kéo nó về player.
4. `PlayerExperience` nhận EXP, carry phần dư và có thể tăng nhiều level trong một lần nhặt.
5. Mỗi level chỉ phát `LevelGained(int newLevel)` để hệ thống chọn/nâng trang bị kết nối sau này. Player level **không tự cộng bất kỳ Character Stat nào**.

## Công thức level kiểu HoloCure

Level 1 lên 2 cần `79` EXP. Từ level 2 trở đi:

```text
round((4 × (currentLevel + 1)) ^ 2.1)
- round((4 × currentLevel) ^ 2.1)
```

Runtime dùng `MidpointRounding.ToEven` giống quy tắc làm tròn được mô tả bởi HoloCure Wiki. Các số `79`, `4`, `2.1` đều chỉnh được trong `Resources/ExperienceSystemConfig.asset`.

Nguồn tham khảo:

- https://holocure.wiki.gg/wiki/Level_up
- https://holocure.wiki.gg/wiki/XP

## Drop

| Loại | Enemy | Giá trị mặc định |
|---|---|---:|
| Normal | Normal enemy | 100% base EXP |
| Green | Normal enemy | 125% base EXP |
| Orange | Normal enemy | 150% base EXP |
| Red | Elite hoặc mỗi Boss phase | 200% base EXP |
| Rainbow Magnet | Roll độc lập trên mỗi reward | 0 EXP; hút toàn bộ EXP trên map |

Normal enemy luôn rơi đúng một EXP chính. Ratio mặc định của Normal/Green/Orange là `80/15/5`; hệ thống tự chuẩn hóa mọi giá trị về `100%`, vì vậy có thể cấu hình `8/1.5/0.5` hoặc `80/15/5` với cùng kết quả. Nếu cả ba cùng bằng `0`, hệ thống fallback sang Normal `100%` để vẫn đảm bảo có drop. Elite và Boss phase luôn rơi Red. Rainbow là phần thưởng phụ roll độc lập, mặc định `1/3000 = 0.033333%`. Tất cả ratio nằm trong `Resources/ExperienceSystemConfig.asset`.

## Debug EXP

Chọn Player có component `PlayerExperience` trong Play Mode để xem:

- `Current EXP / Required EXP`, phần trăm và thanh tiến trình.
- EXP còn thiếu để lên level.
- Lượng EXP vừa nhặt, số level vừa tăng và tổng EXP nhặt trong Play session.
- Các nút `+1`, `+10`, `+100 EXP` chỉ dùng trong Play Mode.

Chọn `Experience Pickup Service` trong Play Mode để xem số pickup đang tồn tại, bán kính hút hiện tại, tổng số lần roll và số lượng thực tế đã spawn theo từng màu. Chọn `ExperienceSystemConfig` để xem tổng ratio và tỉ lệ hiệu lực sau khi tự chuẩn hóa.

`EnemyExperienceDropper.Base Experience` là giá trị Normal của enemy đó. Nên dùng số chia hết cho 4 để Green `+25%` luôn là số nguyên chính xác; enemy mẫu dùng `4` EXP. Với Boss nhiều phase, gọi:

```csharp
dropper.DropPhaseExperience();
```

ở thời điểm phase kết thúc. Nếu final phase đã gọi API này, tắt `Drop Red On Boss Death` để không rơi hai lần.

## EXP Pickup Range

`CharacterStats.PUR` là phần trăm của `ExperienceSystemConfig.Base Pickup Radius`.

```text
Final Pickup Radius = Base Pickup Radius × EXP Pickup Range Percent / 100
```

Weapon/item/passive có thể cấp `ExperiencePickupRangePercent` bằng `Stat Bonuses` như các combat stat khác.

## Global stat restriction theo level trang bị

Mỗi `WeaponLevelData` có `Player Stat Rules Unlocked At This Level`. Rule cộng dồn với các level trước và tác động lên stat cuối của player, không phải chỉ riêng weapon đang sở hữu rule.

- `Block`: bỏ contribution từ các category trong `Affected Sources`.
- `Convert`: bỏ stat gốc và chuyển giá trị sang `Converted Stat × Conversion Ratio`.
- `Allow Owning Source`: bonus do chính trang bị sở hữu rule vẫn được nhận.
- `Priority`: rule cao hơn thắng khi nhiều rule cùng khóa một stat; cùng priority thì `Block` thắng `Convert`.

Ví dụ một weapon level 7 khóa Critical Chance từ mọi nguồn khác:

- Stat: `CriticalChancePercent`
- Affected Sources: `All`
- Mode: `Block`
- Allow Owning Source: bật

Bonus level và các stack do chính weapon đó tạo dùng chung owner key nên vẫn được nhận. Provider phụ đăng ký bằng overload:

```csharp
combatStats.SetStatBonuses(
    providerKey: killStackState,
    ownerKey: weaponController,
    source: CombatStatSource.WeaponLevel,
    bonuses: bonuses
);
```
