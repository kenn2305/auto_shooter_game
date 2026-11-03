# Level-Up Draft System

## Luồng hoạt động

1. `PlayerExperience` phát `LevelGained(newLevel)` khi thanh EXP đầy.
2. `LevelUpDraftController` xếp level đó vào hàng đợi, pause game và tạo đúng 3 lựa chọn.
3. Người chơi chọn một card. Chỉ lựa chọn đó được áp dụng; bản thân Player Level không tự cộng Stat.
4. Nếu một lần nhận EXP tăng nhiều level, các draft được mở tuần tự rồi game mới tiếp tục.

## Điều kiện hợp lệ

- Weapon/Item chưa sở hữu chỉ xuất hiện nếu còn slot tương ứng.
- Weapon/Item đã sở hữu chỉ xuất hiện dưới dạng nâng level.
- Weapon/Item max level bị loại hoàn toàn khỏi pool.
- Khi đạt giới hạn slot, trang bị mới không còn xuất hiện nhưng trang bị đang sở hữu vẫn được nâng đến max.
- Khi mọi trang bị hợp lệ đã max, cả ba lựa chọn đều là Stat package.
- Không có hai card trùng cùng Weapon, Item hoặc Stat trong một draft.

## Cân bằng mặc định

Mỗi phase có category weight riêng, sau khi lọc các lựa chọn không hợp lệ:

| Player Level | New Equipment | Owned Upgrade | Stat Package |
|---:|---:|---:|---:|
| 2–5 | 55 | 35 | 10 |
| 6–15 | 35 | 50 | 15 |
| 16+ | 20 | 55 | 25 |

Ngoài weight, mặc định luôn đảm bảo:

- Ít nhất một New Equipment nếu còn slot và còn trang bị chưa sở hữu.
- Ít nhất một Owned Upgrade nếu có Weapon/Item chưa max.
- Slot còn lại roll theo phase weight.

Thiết kế này cho người chơi sớm hoàn thiện bộ khung build nhưng dần chuyển trọng tâm sang nâng cấp đồ đã chọn, tránh việc level cao vẫn liên tục đưa ra trang bị mới.

Stat package mặc định tham chiếu HoloCure:

- ATK Up: `+8%`, weight `3`.
- Speed Up: `+12%`, weight `4`.
- Critical Up: `+3%`, weight `3`.
- Pickup Range Up: `+20%`, weight `4`.

## Cấu hình

Asset chính: `Resources/LevelUpDraftConfig.asset`.

- `Maximum Weapon Slots`: mặc định `2`.
- `Maximum Item Slots`: mặc định `2`.
- `Weapons`: gán `WeaponData`; có thể gán thêm prefab nếu Weapon cần hierarchy/visual riêng. Nếu prefab trống, runtime tự tạo `WeaponController` theo `weaponID` và data.
- `Items`: gán `LevelUpItemData`; prefab là tùy chọn.
- `Stat Packages`: ID, tên, mô tả, icon, weight, giới hạn số lần chọn và danh sách bonus.
- `Phases`: chỉnh toàn bộ weight và hai fairness guarantee.

Item level dùng `LevelUpItemData`. Bonus của các level cộng dồn và đăng ký với `CombatStatSource.Equipment`, vì vậy vẫn tuân thủ hệ thống Block/Convert Stat toàn cục.

## Nguồn thiết kế tham khảo

- HoloCure Level Up: https://holocure.wiki.gg/wiki/Level_up
- Vampire Survivors Level Up: https://vampire.survivors.wiki/w/Level_up
